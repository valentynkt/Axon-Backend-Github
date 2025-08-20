// /BuildingBlocks/Application/Behaviors/QueryCachingBehavior.cs
#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Read-through caching for queries (Result&lt;T,Error&gt;) with explicit opt-in.
/// Stores ONLY successful value (TValue). On hit, returns Result.Success(TValue).
/// L1: IMemoryCache; optional L2: IDistributedCache. Skips caching of failures.
/// </summary>
public sealed class QueryCachingBehavior<TRequest, TValue>
    : IPipelineBehavior<TRequest, Result<TValue, Error>>
    where TRequest : IQuery<TValue>, ICacheableQuery
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache? _distributed;
    private readonly ILogger<QueryCachingBehavior<TRequest, TValue>> _logger;

    public QueryCachingBehavior(
        IMemoryCache memory,
        ILogger<QueryCachingBehavior<TRequest, TValue>> logger,
        IDistributedCache? distributed = null)
    { _memory = memory; _logger = logger; _distributed = distributed; }

    public async Task<Result<TValue, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue, Error>> next,
        CancellationToken ct)
    {
        if (!request.UseCache) return await next();
        var ttl = request.CacheDuration ?? TimeSpan.FromMinutes(5);
        if (ttl <= TimeSpan.Zero) return await next();

        var key = BuildKeySafe(request);

        if (_memory.TryGetValue(key, out TValue? l1) && l1 is not null)
        {
            _logger.LogDebug("Query cache L1 hit {Key}", key);
            return Result.Success<TValue, Error>(l1);
        }

        if (_distributed is not null)
        {
            var raw = await _distributed.GetStringAsync(key, ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var value = JsonSerializer.Deserialize<TValue>(raw, Json)!;
                _memory.Set(key, value, ttl);
                _logger.LogDebug("Query cache L2 hit {Key}", key);
                return Result.Success<TValue, Error>(value);
            }
        }

        var result = await next();
        if (result.IsFailure)
        {
            _logger.LogDebug("Skip cache store for failed {Query}", typeof(TRequest).Name);
            return result;
        }

        var valueToCache = result.Value;
        _memory.Set(key, valueToCache, ttl);
        if (_distributed is not null)
        {
            var payload = JsonSerializer.Serialize(valueToCache, Json);
            await _distributed.SetStringAsync(key, payload, new DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }

        _logger.LogDebug("Stored query cache {Key} (ttl {Ttl})", key, ttl);
        return result;
    }

    private static string BuildKeySafe(TRequest request)
    {
        var prefix = string.IsNullOrWhiteSpace(request.CacheKeyPrefix) ? typeof(TRequest).Name : request.CacheKeyPrefix!;
        try
        {
            var json = JsonSerializer.Serialize(request, Json);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
            return $"q:{prefix}:{hash[..16]}";
        }
        catch
        {
            // Fallback: type name + hash of ToString()
            var fallback = request.ToString() ?? typeof(TRequest).FullName ?? typeof(TRequest).Name;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fallback))).ToLowerInvariant();
            return $"q:{prefix}:{hash[..16]}";
        }
    }
}

/// <summary>Explicit opt-in contract for query caching.</summary>
public interface ICacheableQuery
{
    bool UseCache { get; }
    TimeSpan? CacheDuration { get; }
    string? CacheKeyPrefix { get; }
}
