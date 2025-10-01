using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Idempotency;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Handles command idempotency by caching responses.
/// Only processes commands that implement IIdempotentCommand.
/// By default caches only successes; failures cached only when CacheFailures = true.
/// 
/// ARCHITECTURAL NOTE: While this behavior accepts any IRequest&lt;TResponse&gt; for MediatR compatibility,
/// it works with IIdempotentCommand implementations which should inherit from RequestBase to provide
/// IAxonRequest features (RequestId, RequestedAt, Metadata) for proper observability integration.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
        ArgumentNullException.ThrowIfNull(next);

        // Only process idempotent commands
        if (request is not IIdempotentCommand idempotentCommand)
        {
            return await next();
        }

        var key = _keyResolver.Resolve(idempotentCommand);
        var window = idempotentCommand.GetIdempotencyWindow() ?? _options.Value.DefaultWindow;

        // Try to get cached response
        var cached = await _cache.GetStringAsync(key, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Idempotency hit for {Key}. Cached payload: {CachedPayload}", key, cached);
            var cachedResponse = DeserializeResponse(cached);
            if (cachedResponse is not null)
            {
                _logger.LogDebug("Successfully deserialized cached response for {Key}. Type: {Type}", key, typeof(TResponse).Name);
                return cachedResponse;
            }
            _logger.LogWarning("Failed to deserialize cached response for {Key}. Type: {Type}. Proceeding with handler. Cached payload: {CachedPayload}",
                key, typeof(TResponse).Name, cached);
        }

        // Execute handler
        var response = await next();

        // Determine whether to cache based on success/failure and CacheFailures setting
        var shouldCache = ShouldCacheResponse(response, idempotentCommand.CacheFailures);
        
        if (shouldCache)
        {
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
        }
        else
        {
            _logger.LogDebug("Skipping cache storage for {Key} (failure, CacheFailures=false)", key);
        }

        return response;
    }

    private static bool ShouldCacheResponse(TResponse response, bool cacheFailures)
    {
        // If CacheFailures is true, always cache
        if (cacheFailures)
            return true;

        // Otherwise, only cache successes
        return IsSuccessResponse(response);
    }

    private static bool IsSuccessResponse(TResponse response)
    {
        if (response is null)
            return false;

        // Check for UnitResult<Error>
        if (IsUnitResultError(response, out var unitResult))
            return unitResult.IsSuccess;

        // Check for Result<TValue, Error>
        if (IsResultWithError(response, out var isSuccess, out _, out _, out _))
            return isSuccess;

        // If not a known Result type, assume success
        return true;
    }

    // ---------- Envelope v2 Serialization ----------
    
    private sealed record EnvelopeV2(
        int V,
        bool Ok,
        string? Type = null,
        string? Val = null,
        ErrorDto? Err = null
    );

    private sealed record ErrorDto(
        string Code,
        string Msg,
        string Type,
        string Sev,
        object? Meta = null
    );

    private static string SerializeResponse(TResponse response)
    {
        // Handle null response
        if (response is null)
        {
            var errorEnv = new EnvelopeV2(
                V: 2,
                Ok: false,
                Err: new ErrorDto("NULL_RESPONSE", "Null response from handler", "Internal", "Error")
            );
            return JsonSerializer.Serialize(errorEnv, JsonOptions);
        }

        // UnitResult<Error>
        if (IsUnitResultError(response, out var unit))
        {
            if (unit.IsSuccess)
            {
                var successEnv = new EnvelopeV2(V: 2, Ok: true);
                return JsonSerializer.Serialize(successEnv, JsonOptions);
            }
            else
            {
                var errorDto = CreateErrorDto(unit.Error);
                var failureEnv = new EnvelopeV2(V: 2, Ok: false, Err: errorDto);
                return JsonSerializer.Serialize(failureEnv, JsonOptions);
            }
        }

        // Result<TValue, Error>
        if (IsResultWithError(response, out var isSuccess, out var value, out var error, out var valueType))
        {
            if (isSuccess && value is not null)
            {
                var valueJson = JsonSerializer.Serialize(value, valueType, JsonOptions);
                var successEnv = new EnvelopeV2(
                    V: 2,
                    Ok: true,
                    Type: valueType.AssemblyQualifiedName, // Use assembly-qualified name for proper type resolution
                    Val: valueJson
                );
                return JsonSerializer.Serialize(successEnv, JsonOptions);
            }
            else
            {
                var errorDto = CreateErrorDto(error);
                var failureEnv = new EnvelopeV2(V: 2, Ok: false, Err: errorDto);
                return JsonSerializer.Serialize(failureEnv, JsonOptions);
            }
        }

        // Fallback: serialize response as-is (assume success)
        var fallbackJson = JsonSerializer.Serialize(response, JsonOptions);
        var fallbackEnv = new EnvelopeV2(
            V: 2,
            Ok: true,
            Type: typeof(TResponse).AssemblyQualifiedName, // Use assembly-qualified name for proper type resolution
            Val: fallbackJson
        );
        return JsonSerializer.Serialize(fallbackEnv, JsonOptions);
    }

    private static ErrorDto CreateErrorDto(Error? error)
    {
        if (error is null)
        {
            return new ErrorDto("UNKNOWN", "Unknown error", "Unknown", "Error");
        }

        return new ErrorDto(
            Code: error.Code,
            Msg: error.Message,
            Type: error.Type.ToString(),
            Sev: error.Severity.ToString(),
            Meta: error.Metadata
        );
    }

    private TResponse? DeserializeResponse(string json)
    {
        try
        {
            var env = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);
            if (env is null || env.V != 2)
            {
                _logger.LogWarning("Invalid envelope version or null envelope. Attempting direct deserialization. Envelope: {Envelope}", env);
                // Try fallback to direct deserialization
                return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
            }

            _logger.LogDebug("Envelope parsed: V={Version}, Ok={Ok}, Type={Type}, HasValue={HasValue}, HasError={HasError}",
                env.V, env.Ok, env.Type, env.Val != null, env.Err != null);

            // UnitResult<Error>
            if (TryRehydrateUnitResult(env, out var unitResp))
            {
                _logger.LogDebug("Successfully rehydrated UnitResult<Error>");
                return unitResp;
            }

            // Result<TValue, Error>
            if (TryRehydrateResult(env, out var resResp))
            {
                _logger.LogDebug("Successfully rehydrated Result<{ValueType}, Error>", typeof(TResponse).GenericTypeArguments.FirstOrDefault()?.Name ?? "Unknown");
                return resResp;
            }

            // Fallback for direct serialized responses
            if (env.Ok && env.Val is not null)
            {
                _logger.LogDebug("Attempting direct deserialization of envelope value. Type: {Type}", typeof(TResponse).Name);
                return JsonSerializer.Deserialize<TResponse>(env.Val, JsonOptions);
            }

            _logger.LogWarning("Could not deserialize response. Envelope.Ok={Ok}, HasValue={HasValue}", env.Ok, env.Val != null);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during deserialization of cached response. Type: {Type}", typeof(TResponse).Name);
            // If deserialization fails, return null to force re-execution
            return default;
        }
    }

    // ---------- Type shape detection ----------

    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern", Justification = "The properties IsSuccess and Error are guaranteed to exist on UnitResult<Error> type")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern", Justification = "The properties IsSuccess, Value, and Error are guaranteed to exist on Result<T,Error> type")]
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

    private static bool TryRehydrateUnitResult(EnvelopeV2 env, out TResponse response)
    {
        response = default!;
        var target = typeof(TResponse);

        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(UnitResult<>) &&
            target.GetGenericArguments()[0] == typeof(Error))
        {
            object res;
            if (env.Ok)
            {
                res = UnitResult.Success<Error>();
            }
            else
            {
                var error = RehydrateError(env.Err);
                res = UnitResult.Failure(error);
            }

            response = (TResponse)res;
            return true;
        }

        return false;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2060:MakeGenericMethod",
        Justification = "Result pattern rehydration requires reflection for generic method construction")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2057:GetType",
        Justification = "Type resolution for deserialization is intentional and safe")]
    private bool TryRehydrateResult(EnvelopeV2 env, out TResponse response)
    {
        response = default!;
        var target = typeof(TResponse);

        if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(Result<,>))
        {
            _logger.LogDebug("TResponse is not a Result<,> type. Type: {Type}", target.Name);
            return false;
        }

        var args = target.GetGenericArguments();
        var valueType = args[0];
        var errorType = args[1];
        if (errorType != typeof(Error))
        {
            _logger.LogDebug("Error type is not BuildingBlocks Error. ErrorType: {ErrorType}", errorType.Name);
            return false;
        }

        if (env.Ok)
        {
            if (env.Val is null || string.IsNullOrWhiteSpace(env.Type))
            {
                _logger.LogWarning("Envelope is Ok but Val is null or Type is empty. Val: {Val}, Type: {Type}", env.Val, env.Type);
                return false;
            }

            _logger.LogDebug("Attempting to rehydrate success result. ValueType: {ValueType}, EnvelopeType: {EnvelopeType}",
                valueType.Name, env.Type);

            var vt = Type.GetType(env.Type, throwOnError: false);
            if (vt is null || !valueType.IsAssignableFrom(vt))
            {
                _logger.LogWarning("Type mismatch or type not found. EnvelopeType: {EnvelopeType}, ExpectedType: {ExpectedType}, TypeFound: {TypeFound}",
                    env.Type, valueType.FullName, vt?.FullName ?? "NULL");
                return false;
            }

            _logger.LogDebug("Deserializing value. Type: {Type}, JSON: {Json}", vt.Name, env.Val);
            var value = JsonSerializer.Deserialize(env.Val, vt, JsonOptions);
            if (value is null)
            {
                _logger.LogWarning("Deserialization returned null. Type: {Type}, JSON: {Json}", vt.Name, env.Val);
                return false;
            }

            _logger.LogDebug("Successfully deserialized value. Creating Result.Success");

            // Result.Success<TValue, Error>(value)
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Success" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);
            var generic = method.MakeGenericMethod(valueType, typeof(Error));
            var res = generic.Invoke(null, new[] { value })!;
            response = (TResponse)res;
            _logger.LogDebug("Successfully created Result.Success<{ValueType}, Error>", valueType.Name);
        }
        else
        {
            _logger.LogDebug("Rehydrating failure result");
            var error = RehydrateError(env.Err);
            // Result.Failure<TValue, Error>(error)
            var method = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Failure" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            var generic = method.MakeGenericMethod(valueType, typeof(Error));
            var res = generic.Invoke(null, new object?[] { error })!;
            response = (TResponse)res;
        }

        return true;
    }

    private static Error RehydrateError(ErrorDto? errorDto)
    {
        if (errorDto is null)
        {
            return Error.Internal("Unknown error during deserialization", "DESERIALIZATION_ERROR");
        }

        if (!Enum.TryParse<ErrorType>(errorDto.Type, out var errorType))
            errorType = ErrorType.Internal;

        if (!Enum.TryParse<ErrorSeverity>(errorDto.Sev, out var severity))
            severity = ErrorSeverity.Error;

        // Use appropriate factory method based on error type
        var error = errorType switch
        {
            ErrorType.Validation => Error.Validation(errorDto.Msg, errorDto.Code),
            ErrorType.NotFound => Error.NotFound(errorDto.Msg, errorDto.Code),
            ErrorType.Conflict => Error.Conflict(errorDto.Msg, errorDto.Code),
            ErrorType.Unauthorized => Error.Unauthorized(errorDto.Msg, errorDto.Code),
            ErrorType.Forbidden => Error.Forbidden(errorDto.Msg, errorDto.Code),
            ErrorType.BusinessRule => Error.BusinessRule(errorDto.Msg, errorDto.Code),
            ErrorType.External => Error.External(errorDto.Msg, errorDto.Code),
            ErrorType.Network => Error.Network(errorDto.Msg, errorDto.Code),
            ErrorType.Timeout => Error.Timeout(errorDto.Msg, errorDto.Code),
            ErrorType.Unavailable => Error.Unavailable(errorDto.Msg, errorDto.Code),
            ErrorType.Persistence => Error.Persistence(errorDto.Msg, errorDto.Code),
            ErrorType.Configuration => Error.Configuration(errorDto.Msg, errorDto.Code),
            ErrorType.Security => Error.Security(errorDto.Msg, errorDto.Code),
            ErrorType.RateLimit => Error.RateLimit(errorDto.Msg, errorDto.Code),
            ErrorType.Cancelled => Error.Cancelled(errorDto.Msg, errorDto.Code),
            _ => Error.Internal(errorDto.Msg, errorDto.Code)
        };

        // Apply metadata if available
        if (errorDto.Meta is Dictionary<string, object> meta && meta.Count > 0)
        {
            error = error.WithMetadata(meta);
        }

        // Apply severity if different from default
        if (error.Severity != severity)
        {
            error = error.WithSeverity(severity);
        }

        return error;
    }
}
