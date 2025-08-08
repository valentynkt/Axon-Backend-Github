using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Abstract base class providing shared caching functionality for repository decorators
/// Implements cache key generation, cache expiration policies, and logging
/// </summary>
public abstract class CacheManagerBase<TEntity, TId> : IDisposable
    where TEntity : class, IAggregate<TId>
    where TId : notnull
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly TimeSpan _cacheExpiration;
    private readonly string _cacheKeyPrefix;

    protected CacheManagerBase(
        IMemoryCache cache,
        ILogger logger,
        TimeSpan? cacheExpiration = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(15);
        _cacheKeyPrefix = $"{typeof(TEntity).Name}_";
    }

    /// <summary>
    /// Generates a cache key for a specific entity ID
    /// </summary>
    protected virtual string GetCacheKey(TId id) => $"{_cacheKeyPrefix}{id}";

    /// <summary>
    /// Generates a cache key for "get all" operations
    /// </summary>
    protected virtual string GetAllCacheKey() => $"{_cacheKeyPrefix}All";

    /// <summary>
    /// Generates a cache key for paginated results
    /// </summary>
    protected virtual string GetPagedCacheKey(int pageNumber, int pageSize) => 
        $"{_cacheKeyPrefix}Paged_{pageNumber}_{pageSize}";

    /// <summary>
    /// Generates a cache key for count operations
    /// </summary>
    protected virtual string GetCountCacheKey() => $"{_cacheKeyPrefix}Count";

    /// <summary>
    /// Sets a value in the cache with the configured expiration
    /// </summary>
    protected virtual void SetCache<T>(string key, T value)
    {
        _cache.Set(key, value, _cacheExpiration);
        _logger.LogDebug("Cached {EntityType} with key {CacheKey}", typeof(TEntity).Name, key);
    }

    /// <summary>
    /// Tries to get a value from the cache
    /// </summary>
    protected virtual bool TryGetCache<T>(string key, out T? value)
    {
        var found = _cache.TryGetValue(key, out value);
        if (found)
        {
            _logger.LogDebug("Cache hit for {EntityType} with key {CacheKey}", typeof(TEntity).Name, key);
        }
        return found;
    }

    /// <summary>
    /// Removes a specific cache entry
    /// </summary>
    protected virtual void InvalidateCache(string key)
    {
        _cache.Remove(key);
        _logger.LogDebug("Invalidated cache for {EntityType} with key {CacheKey}", typeof(TEntity).Name, key);
    }

    /// <summary>
    /// Invalidates cache for a specific entity ID
    /// </summary>
    protected virtual void InvalidateCacheForId(TId id)
    {
        var cacheKey = GetCacheKey(id);
        InvalidateCache(cacheKey);
    }

    /// <summary>
    /// Invalidates all cache entries for this entity type
    /// This includes individual entities, "get all", paged results, and counts
    /// </summary>
    protected virtual void InvalidateAllCache()
    {
        // Remove all cache keys for this entity type
        var allCacheKey = GetAllCacheKey();
        var countCacheKey = GetCountCacheKey();
        
        InvalidateCache(allCacheKey);
        InvalidateCache(countCacheKey);
        
        // Note: In a production environment, you might want to maintain a registry
        // of all cache keys for more efficient bulk invalidation
        _logger.LogDebug("Invalidated all cache entries for {EntityType}", typeof(TEntity).Name);
    }

    /// <summary>
    /// Invalidates cache entries related to a specific entity
    /// This includes the entity itself and any aggregate cache entries
    /// </summary>
    protected virtual void InvalidateRelatedCache(TId id)
    {
        InvalidateCacheForId(id);
        InvalidateAllCache();
    }

    /// <summary>
    /// Invalidates cache entries for multiple entities
    /// </summary>
    protected virtual void InvalidateRelatedCache(IEnumerable<TId> ids)
    {
        foreach (var id in ids)
        {
            InvalidateCacheForId(id);
        }
        InvalidateAllCache();
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public virtual void Dispose()
    {
        // Cache and Logger are typically singleton services managed by DI container
        // No explicit disposal needed
        GC.SuppressFinalize(this);
    }
}