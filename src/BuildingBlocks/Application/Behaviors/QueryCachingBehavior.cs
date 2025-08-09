using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Declarative query caching (L1 IMemoryCache + L2 IDistributedCache).
/// - Uses IQuery declarative flags (UseCache, CacheDuration, CacheKeyPrefix).
/// - Optional tag index for later invalidation by commands.
/// - Low-cardinality metrics, W3C-friendly (no custom correlation in here).
/// </summary>
public sealed class QueryCachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : notnull
{
    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _distributed;
    private readonly ICacheKeyGenerator _keys;
    private readonly ILogger<QueryCachingBehavior<TRequest, TResponse>> _logger;
    private readonly CacheOptions _opts;
    private readonly JsonSerializerOptions _json;
    private readonly ICacheTagIndex? _tagIndex; // optional, enables true tag invalidation

    public QueryCachingBehavior(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ICacheKeyGenerator keyGenerator,
        ILogger<QueryCachingBehavior<TRequest, TResponse>> logger,
        IOptions<CacheOptions> options,
        IOptions<JsonSerializerOptions>? jsonOptions = null,
        ICacheTagIndex? tagIndex = null)
    {
        _memory = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributed = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _keys = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _json = jsonOptions?.Value ?? new JsonSerializerOptions();
        _tagIndex = tagIndex;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!request.UseCache)
            return await next(); // MediatR delegate has no token param

        var key = _keys.GenerateKey(request);
        var sw = Stopwatch.StartNew();

        // L1
        if (_memory.TryGetValue(key, out var l1) && l1 is TResponse hit1)
        {
            sw.Stop();
            CacheInstrumentation.Hits.Add(1, Tags("hit"));
            CacheInstrumentation.Latency.Record(sw.ElapsedMilliseconds, Tags("hit"));
            _logger.LogDebug("L1 cache hit: {Key}", key);
            return hit1;
        }

        // L2
        var bytes = await _distributed.GetAsync(key, cancellationToken);
        if (bytes is not null && bytes.Length > 0)
        {
            try
            {
                var obj = JsonSerializer.Deserialize<TResponse>(bytes, _json);
                if (obj is not null)
                {
                    // promote to L1 with short TTL
                    var l1Ttl = TimeSpan.FromMinutes(Math.Min(5, Math.Max(1, GetCacheDuration(request).TotalMinutes)));
                    _memory.Set(key, obj, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = l1Ttl
                    });

                    sw.Stop();
                    CacheInstrumentation.Hits.Add(1, Tags("hit"));
                    CacheInstrumentation.Latency.Record(sw.ElapsedMilliseconds, Tags("hit"));
                    _logger.LogDebug("L2 cache hit (promoted to L1): {Key}", key);
                    return obj;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize L2 cache entry for {Key}. Removing.", key);
                await _distributed.RemoveAsync(key, cancellationToken);
            }
        }

        // MISS → execute
        sw.Stop();
        CacheInstrumentation.Misses.Add(1, Tags("miss"));
        CacheInstrumentation.Latency.Record(sw.ElapsedMilliseconds, Tags("miss"));
        _logger.LogDebug("Cache miss, executing handler: {Query}", typeof(TRequest).Name);

        var response = await next();

        if (!ShouldCache(response))
            return response;

        var ttl = GetCacheDuration(request);
        if (ttl <= TimeSpan.Zero)
            return response;

        try
        {
            // store L1 first (shorter)
            var l1Ttl = TimeSpan.FromMinutes(Math.Min(30, Math.Max(1, ttl.TotalMinutes)));
            _memory.Set(key, response, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = l1Ttl
            });

            // store L2
            var payload = JsonSerializer.SerializeToUtf8Bytes(response, _json);
            await _distributed.SetAsync(key,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);

            // optional tag indexing for later invalidation
            var tags = ResolveTags(request);
            if (tags.Length > 0 && _tagIndex is not null)
                await _tagIndex.IndexAsync(tags, key, ttl, cancellationToken);

            _logger.LogDebug("Cached {Query} for {Ttl}", typeof(TRequest).Name, ttl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // let caller cancel; we already returned the response
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cache for {Key}", key);
        }

        return response;
    }

    private static bool ShouldCache(TResponse response) =>
        response switch
        {
            IResult r => r.IsSuccess,   // cache only successful Results
            _        => response is not null // cache DTOs if non-null
        };

    private static TagList Tags(string outcome) => new()
    {
        { "query.type", typeof(TRequest).Name },
        { "outcome", outcome }
    };

    private static TimeSpan GetCacheDuration(IQuery<TResponse> request) =>
        request.CacheDuration ?? TimeSpan.FromMinutes(5);

    private static string[] ResolveTags(IQuery<TResponse> request)
    {
        // Priority: explicit interface; then attribute; finally prefix (coarse)
        if (request is ICacheTaggable taggable && taggable.CacheTags is { Length: >0 })
            return taggable.CacheTags;

        var attr = request.GetType().GetCustomAttributes(typeof(CacheTagsAttribute), inherit: true)
            .OfType<CacheTagsAttribute>()
            .FirstOrDefault();

        if (attr is not null && attr.Tags.Length > 0)
            return attr.Tags;

        // fallback: use prefix if present to enable coarse invalidation
        return string.IsNullOrWhiteSpace(request.CacheKeyPrefix)
            ? Array.Empty<string>()
            : new[] { request.CacheKeyPrefix! };
    }

    private static class CacheInstrumentation
    {
        private static readonly Meter Meter = new(TelemetryTags.Metrics.Application.AppService);
        public static readonly Counter<long> Hits   = Meter.CreateCounter<long>("axon.query.cache.hits",   description: "Query cache hits");
        public static readonly Counter<long> Misses = Meter.CreateCounter<long>("axon.query.cache.misses", description: "Query cache misses");
        public static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("axon.query.cache.latency", unit: "ms", description: "Cache lookup latency");
    }
}
