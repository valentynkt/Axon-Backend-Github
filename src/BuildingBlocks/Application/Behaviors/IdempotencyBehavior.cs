// /BuildingBlocks/Core/Idempotency/IdempotencyBehavior.cs
#nullable enable
using System.Reflection;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Core.Idempotency;

/// <summary>
/// Caches responses for idempotent commands:
/// - If cached → short-circuit and return cached response
/// - Else     → execute, cache, return
/// Uses IDistributedCache (Redis/memory etc.)
/// Supports CFE Result&lt;T, Error&gt; and UnitResult&lt;Error&gt;.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand, IRequest<TResponse>
{
    private readonly IdempotencyKeyResolver _keyResolver;
    private readonly IDistributedCache _cache;
    private readonly IOptions<IdempotencyOptions> _options;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public IdempotencyBehavior(
        IdempotencyKeyResolver keyResolver,
        IDistributedCache cache,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _keyResolver = keyResolver;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = _keyResolver.Resolve(request);
        var window = request.GetIdempotencyWindow() ?? _options.Value.DefaultWindow;

        var cached = await _cache.GetStringAsync(key, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Idempotency hit for {Key}", key);
            return IdempotencyBehavior<TRequest, TResponse>.DeserializeResponse(cached);
        }

        var response = await next();

        try
        {
            var payload = SerializeResponse(response);
            var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window };
            await _cache.SetStringAsync(key, payload, opts, ct);
            _logger.LogDebug("Idempotency store for {Key} (TTL: {Window})", key, window);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store idempotent response for {Key}", key);
        }

        return response;
    }

    // ---------- Serialization envelope ----------
    private sealed record Envelope(
        bool IsSuccess,
        string? ValueJson,
        Error? Error,
        string? ValueType // AssemblyQualifiedName for TValue (when IsSuccess=true on Result<T,Error>)
    );

    private static string SerializeResponse(TResponse response)
    {
        // Defensive: allow handlers to (wrongly) return null
        if (response is null)
        {
            var envNull = new Envelope(false, null, Error.Internal("Null response from handler", "NULL_RESPONSE"), null);
            return JsonSerializer.Serialize(envNull, JsonOptions);
        }

        // UnitResult<Error>
        if (IsUnitResultError(response, out var unit))
        {
            var env = new Envelope(unit.IsSuccess, null, unit.IsSuccess ? null : unit.Error, null);
            return JsonSerializer.Serialize(env, JsonOptions);
        }

        // Result<TValue, Error>
        if (IsResultWithError(response, out var isSuccess, out var value, out var error, out var valueType))
        {
            var valueJson = isSuccess && value is not null
                ? JsonSerializer.Serialize(value, valueType, JsonOptions)
                : null;

            var env = new Envelope(isSuccess, valueJson, isSuccess ? null : error, isSuccess ? valueType.AssemblyQualifiedName : null);
            return JsonSerializer.Serialize(env, JsonOptions);
        }

        // Fallback: serialize as-is
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private static TResponse DeserializeResponse(string json)
    {
        var env = JsonSerializer.Deserialize<Envelope>(json, JsonOptions);
        if (env is null)
            return JsonSerializer.Deserialize<TResponse>(json, JsonOptions)!;

        // UnitResult<Error>
        if (TryRehydrateUnitResult(env, out var unitResp))
            return unitResp;

        // Result<TValue, Error>
        if (TryRehydrateResult(env, out var resResp))
            return resResp;

        // Fallback: response was serialized directly
        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions)!;
    }

    // ---------- Type shape detection ----------

    private static bool IsUnitResultError(object? obj, out (bool IsSuccess, Error? Error) value)
    {
        value = default;
        if (obj is null) return false;

        var t = obj.GetType();
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(UnitResult<>))
            return false;

        var errorArg = t.GetGenericArguments()[0];
        if (errorArg != typeof(Error))
            return false;

        var isSuccess = (bool)t.GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance)!.GetValue(obj)!;
        Error? err = null;
        if (!isSuccess)
            err = (Error?)t.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance)!.GetValue(obj);

        value = (isSuccess, err);
        return true;
    }

    private static bool IsResultWithError(object? obj, out bool isSuccess, out object? value, out Error? error, out Type valueType)
    {
        isSuccess = default;
        value = null;
        error = null;
        valueType = typeof(object);

        if (obj is null) return false;

        var t = obj.GetType();
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Result<,>))
            return false;

        var args = t.GetGenericArguments();
        valueType = args[0];
        var errorType = args[1];
        if (errorType != typeof(Error))
            return false;

        isSuccess = (bool)t.GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance)!.GetValue(obj)!;

        if (isSuccess)
            value = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)!.GetValue(obj);
        else
            error = (Error?)t.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance)!.GetValue(obj);

        return true;
    }

    // ---------- Rehydration ----------

    private static bool TryRehydrateUnitResult(Envelope env, out TResponse response)
    {
        response = default!;
        var target = typeof(TResponse);

        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(UnitResult<>) &&
            target.GetGenericArguments()[0] == typeof(Error))
        {
            // CFE: UnitResult.Success<Error>() / UnitResult.Failure<Error>(error)
            var res = env.IsSuccess
                ? UnitResult.Success<Error>()
                : UnitResult.Failure(env.Error!);

            response = (TResponse)(object)res;
            return true;
        }

        return false;
    }

    private static bool TryRehydrateResult(Envelope env, out TResponse response)
    {
        response = default!;
        var target = typeof(TResponse);

        if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(Result<,>))
            return false;

        var args = target.GetGenericArguments();
        var valueType = args[0];
        var errorType = args[1];
        if (errorType != typeof(Error))
            return false;

        if (env.IsSuccess)
        {
            if (env.ValueJson is null || string.IsNullOrWhiteSpace(env.ValueType))
                return false;

            var vt = Type.GetType(env.ValueType, throwOnError: true)!;
            var value = JsonSerializer.Deserialize(env.ValueJson, vt, JsonOptions)!;

            // CFE: Result.Success<TValue, Error>(value)
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Success" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);
            var generic = method.MakeGenericMethod(valueType, typeof(Error));
            var res = generic.Invoke(null, new[] { value })!;
            response = (TResponse)res;
        }
        else
        {
            // CFE: Result.Failure<TValue, Error>(error)
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Failure" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            var generic = method.MakeGenericMethod(valueType, typeof(Error));
            var res = generic.Invoke(null, new object?[] { env.Error! })!;
            response = (TResponse)res;
        }

        return true;
    }
}
