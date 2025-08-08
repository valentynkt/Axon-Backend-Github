using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Diagnostics.Metrics;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Application.Caching;

namespace BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Caching pipeline behavior for Epic 04 Story 02 - Declarative Query Caching.
/// Integrates with IQuery declarative properties for simplified, performant caching.
/// Supports both memory and distributed caching with W3C TraceContext integration.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : class
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly CacheOptions _options;

    // OpenTelemetry metrics
    private static readonly Counter<long> CacheHitCounter = Metrics.CreateCounter<long>(
        "axon.query.cache.hits",
        description: "Query cache hit count");
    private static readonly Counter<long> CacheMissCounter = Metrics.CreateCounter<long>(
        "axon.query.cache.misses", 
        description: "Query cache miss count");
    private static readonly Histogram<double> CacheLatency = Metrics.CreateHistogram<double>(
        "axon.query.cache.latency",
        unit: "ms",
        description: "Query cache operation latency");

    public CachingBehavior(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ICacheKeyGenerator keyGenerator,
        ILogger<CachingBehavior<TRequest, TResponse>> logger,
        IOptions<CacheOptions> options)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Skip caching if disabled for this query
        if (!request.UseCache)
        {
            _logger.LogDebug("Caching disabled for {QueryType}", typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cacheKey = _keyGenerator.GenerateKey(request);
        
        _logger.LogDebug("Attempting cache lookup for {QueryType} with key: {CacheKey}",
            typeof(TRequest).Name, cacheKey);

        try
        {
            // Try memory cache first
            var cachedResponse = await TryGetFromCache(cacheKey);
            if (cachedResponse != null)
            {
                stopwatch.Stop();
                RecordCacheHit(stopwatch.ElapsedMilliseconds);
                return cachedResponse;
            }

            // Cache miss - execute handler
            stopwatch.Stop();
            RecordCacheMiss(stopwatch.ElapsedMilliseconds);
            
            _logger.LogDebug("Cache miss for {QueryType}, executing handler", typeof(TRequest).Name);
            
            var response = await next(cancellationToken);

            // Cache successful results only
            if (ShouldCacheResponse(response))
            {
                var duration = GetCacheDuration(request);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SetCache(cacheKey, response, duration, cancellationToken);
                        _logger.LogDebug("Cached response for {QueryType} with duration {Duration}",
                            typeof(TRequest).Name, duration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache response for {QueryType}", typeof(TRequest).Name);
                    }
                }, cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache operation failed for {QueryType}, proceeding without cache",
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }
    }

    private async Task<TResponse?> TryGetFromCache(string cacheKey)
    {
        // Try memory cache first (fast L1)
        if (_memoryCache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is TResponse memoryResult)
        {
            _logger.LogDebug("Memory cache hit for key: {CacheKey}", cacheKey);
            return memoryResult;
        }

        // Try distributed cache (L2)
        var cachedBytes = await _distributedCache.GetAsync(cacheKey);
        if (cachedBytes != null)
        {
            try
            {
                var distributedResult = JsonSerializer.Deserialize<TResponse>(cachedBytes);
                if (distributedResult != null)
                {
                    // Populate memory cache from distributed cache hit
                    var memoryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), // Short L1 duration
                        Size = cachedBytes.Length
                    };
                    _memoryCache.Set(cacheKey, distributedResult, memoryOptions);
                    
                    _logger.LogDebug("Distributed cache hit for key: {CacheKey}", cacheKey);
                    return distributedResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached response for key: {CacheKey}", cacheKey);
                // Remove corrupted cache entry
                await _distributedCache.RemoveAsync(cacheKey);
            }
        }

        return null;
    }

    private async Task SetCache(string cacheKey, TResponse response, TimeSpan duration, CancellationToken cancellationToken)
    {
        // Set memory cache (L1)
        var memoryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Min(duration.TotalMinutes, 30)), // Max 30min for L1
            Priority = CacheItemPriority.Normal
        };
        _memoryCache.Set(cacheKey, response, memoryOptions);

        // Set distributed cache (L2)
        var serializedResponse = JsonSerializer.SerializeToUtf8Bytes(response);
        var distributedOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };

        await _distributedCache.SetAsync(cacheKey, serializedResponse, distributedOptions, cancellationToken);
    }

    private static bool ShouldCacheResponse(TResponse response)
    {
        // Only cache successful results
        return response is IResult result && result.IsSuccess;
    }

    private TimeSpan GetCacheDuration(TRequest request)
    {
        return request.CacheDuration ?? _options.DefaultDuration;
    }

    private void RecordCacheHit(long elapsedMs)
    {
        var tags = new TagList
        {
            { "query.type", typeof(TRequest).Name },
            { "outcome", "hit" }
        };

        CacheHitCounter.Add(1, tags);
        CacheLatency.Record(elapsedMs, tags);

        _logger.LogDebug("Cache hit for {QueryType} in {ElapsedMs}ms",
            typeof(TRequest).Name, elapsedMs);
    }

    private void RecordCacheMiss(long elapsedMs)
    {
        var tags = new TagList
        {
            { "query.type", typeof(TRequest).Name },
            { "outcome", "miss" }
        };

        CacheMissCounter.Add(1, tags);
        CacheLatency.Record(elapsedMs, tags);

        _logger.LogDebug("Cache miss for {QueryType} in {ElapsedMs}ms",
            typeof(TRequest).Name, elapsedMs);
    }
}

