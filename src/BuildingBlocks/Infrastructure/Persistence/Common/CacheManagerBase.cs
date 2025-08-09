using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Configuration for cache settings to improve maintainability and testability.
/// Follows the Configuration pattern for better separation of concerns.
/// </summary>
public sealed record CacheConfiguration
{
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromMinutes(15);
    public string KeyPrefix { get; init; } = string.Empty;

    public static CacheConfiguration ForEntity<TEntity>() => new()
    {
        KeyPrefix = $"{typeof(TEntity).Name}_",
        DefaultExpiration = TimeSpan.FromMinutes(15)
    };
}

/// <summary>
/// Abstract base class providing shared caching functionality for repository decorators.
/// Refactored to follow SOLID principles with better separation of concerns and improved testability.
/// </summary>
public abstract class CacheManagerBase<TEntity, TId>
    where TEntity : class, IIdentifiable<TId>
    where TId : IStrongId
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly CacheConfiguration _configuration;

    protected CacheManagerBase(
        IMemoryCache cache,
        ILogger logger,
        CacheConfiguration? configuration = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? CacheConfiguration.ForEntity<TEntity>();
    }

    /// <summary>Generates a cache key for a specific entity ID using the configured prefix.</summary>
    protected virtual string CreateCacheKey(TId id) => $"{_configuration.KeyPrefix}{id}";

    /// <summary>Generates a cache key for collection operations.</summary>
    protected virtual string CreateCollectionCacheKey(string suffix) => $"{_configuration.KeyPrefix}{suffix}";

    /// <summary>Gets cache key for "get all" operations.</summary>
    protected string GetAllCacheKey() => CreateCollectionCacheKey("All");

    /// <summary>Gets cache key for paginated results.</summary>
    protected string GetPagedCacheKey(int pageNumber, int pageSize) => CreateCollectionCacheKey($"Paged_{pageNumber}_{pageSize}");

    /// <summary>Gets cache key for count operations.</summary>
    protected string GetCountCacheKey() => CreateCollectionCacheKey("Count");

    /// <summary>Sets a value in the cache with the configured expiration.</summary>
    protected virtual void SetCache<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheExpiration = expiration ?? _configuration.DefaultExpiration;
        _cache.Set(key, value!, cacheExpiration);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cached {EntityType} with key {CacheKey} for {Expiration}",
                typeof(TEntity).Name, key, cacheExpiration);
        }
    }

    /// <summary>Tries to get a value from the cache.</summary>
    protected virtual bool TryGetFromCache<T>(string key, out T? value)
    {
        var found = _cache.TryGetValue(key, out value);

        if (found && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cache hit for {EntityType} with key {CacheKey}",
                typeof(TEntity).Name, key);
        }

        return found;
    }

    /// <summary>Removes a specific cache entry.</summary>
    protected virtual void RemoveFromCache(string key)
    {
        _cache.Remove(key);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Invalidated cache for {EntityType} with key {CacheKey}",
                typeof(TEntity).Name, key);
        }
    }

    /// <summary>Invalidates cache for a specific entity ID.</summary>
    protected void InvalidateCacheForEntity(TId id) => RemoveFromCache(CreateCacheKey(id));

    /// <summary>
    /// Invalidates collection-related cache entries.
    /// This includes "get all", paged results, and counts.
    /// </summary>
    protected void InvalidateCollectionCaches()
    {
        RemoveFromCache(GetAllCacheKey());
        RemoveFromCache(GetCountCacheKey());

        // Note: In a production environment with high cache volume,
        // consider implementing a cache tag-based invalidation strategy
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Invalidated collection cache entries for {EntityType}", typeof(TEntity).Name);
    }

    /// <summary>
    /// Invalidates cache entries related to a specific entity:
    /// the entity itself and common aggregate cache entries.
    /// </summary>
    protected void InvalidateRelatedCaches(TId id)
    {
        InvalidateCacheForEntity(id);
        InvalidateCollectionCaches();
    }

    /// <summary>Invalidates cache entries for multiple entities.</summary>
    protected void InvalidateRelatedCaches(IEnumerable<TId> ids)
    {
        foreach (var id in ids)
        {
            InvalidateCacheForEntity(id);
        }
        InvalidateCollectionCaches();
    }

    /// <summary>
    /// Gets the value from cache if available; otherwise uses <paramref name="factory"/>
    /// to create it, stores it, and returns it.
    /// </summary>
    protected virtual T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null)
    {
        if (TryGetFromCache<T>(key, out var existing) && existing is not null)
            return existing;

        var value = factory();
        SetCache(key, value, expiration);
        return value;
    }

    /// <summary>
    /// Async variant of <see cref="GetOrCreate{T}"/> with cancellation support.
    /// </summary>
    protected virtual async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        if (TryGetFromCache<T>(key, out var existing) && existing is not null)
            return existing;

        var value = await factory(ct).ConfigureAwait(false);
        SetCache(key, value, expiration);
        return value;
    }
}
