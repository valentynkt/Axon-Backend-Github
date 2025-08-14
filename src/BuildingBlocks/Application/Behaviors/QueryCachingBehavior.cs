using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Core.Abstractions.CQRS;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Minimal query caching via HybridCache (L1 IMemoryCache + L2 IDistributedCache).
/// Uses IQuery.UseCache and IQuery.CacheDuration; no tags, no extras.
/// </summary>
public sealed class QueryCachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : notnull
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _distributed;
    private readonly ILogger<QueryCachingBehavior<TRequest, TResponse>> _logger;

    public QueryCachingBehavior(
        IMemoryCache memory,
        IDistributedCache distributed,
        ILogger<QueryCachingBehavior<TRequest, TResponse>> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _distributed = distributed ?? throw new ArgumentNullException(nameof(distributed));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!request.UseCache)
            return await next();

        var ttl = request.CacheDuration ?? TimeSpan.FromMinutes(5);
        if (ttl <= TimeSpan.Zero)
            return await next();

        var key = BuildKey(request);

        // Let HybridCache do the L1/L2 dance; if miss, it runs the handler.
        return await HybridCache.GetOrCreateAsync(
            _memory,
            _distributed,
            key,
            async _ =>
            {
                var value = await next();
                // cache only non-null / successful results
                if (value is IResult r && r.IsFailure)
                    _logger.LogDebug("Skipping cache for failed result: {Query}", typeof(TRequest).Name);
                return value;
            },
            ttl,
            ct);
    }

    private static string BuildKey(TRequest request)
    {
        var prefix = string.IsNullOrWhiteSpace(request.CacheKeyPrefix)
            ? typeof(TRequest).Name
            : request.CacheKeyPrefix!;

        // Stable hash of the serialized request to keep keys short.
        var json = JsonSerializer.Serialize(request, Json);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

        return $"q:{prefix}:{hash[..16]}"; // truncate for readability
    }
}
