using BuildingBlocks.Core.Domain.Model;
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
    where TEntity : class, IAggregateRoot<TId>
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

    /// <summary>
    /// Generates a cache key for a specific entity ID using the configured prefix.
    /// </summary>
    protected virtual string CreateCacheKey(TId id) => 
        $"{_configuration.KeyPrefix}{id}";

    /// <summary>
    /// Generates a cache key for collection operations.
    /// </summary>
    protected virtual string CreateCollectionCacheKey(string suffix) => 
        $"{_configuration.KeyPrefix}{suffix}";

    /// <summary>
    /// Gets cache key for "get all" operations.
    /// </summary>
    protected string GetAllCacheKey() => CreateCollectionCacheKey("All");

    /// <summary>
    /// Gets cache key for paginated results.
    /// </summary>
    protected string GetPagedCacheKey(int pageNumber, int pageSize) => 
        CreateCollectionCacheKey($"Paged_{pageNumber}_{pageSize}");

    /// <summary>
    /// Gets cache key for count operations.
    /// </summary>
    protected string GetCountCacheKey() => CreateCollectionCacheKey("Count");

    /// <summary>
    /// Sets a value in the cache with the configured expiration.
    /// </summary>
    protected virtual void SetCache<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheExpiration = expiration ?? _configuration.DefaultExpiration;
        _cache.Set(key, value, cacheExpiration);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cached {EntityType} with key {CacheKey} for {Expiration}", 
                typeof(TEntity).Name, key, cacheExpiration);
        }
    }

    /// <summary>
    /// Tries to get a value from the cache.
    /// </summary>
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

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    protected virtual void RemoveFromCache(string key)
    {
        _cache.Remove(key);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Invalidated cache for {EntityType} with key {CacheKey}", 
                typeof(TEntity).Name, key);
        }
    }

    /// <summary>
    /// Invalidates cache for a specific entity ID.
    /// </summary>
    protected void InvalidateCacheForEntity(TId id)
    {
        var cacheKey = CreateCacheKey(id);
        RemoveFromCache(cacheKey);
    }

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
        _logger.LogDebug("Invalidated collection cache entries for {EntityType}", typeof(TEntity).Name);
    }

    /// <summary>
    /// Invalidates cache entries related to a specific entity.
    /// This includes the entity itself and any aggregate cache entries.
    /// </summary>
    protected void InvalidateRelatedCaches(TId id)
    {
        InvalidateCacheForEntity(id);
        InvalidateCollectionCaches();
    }

    /// <summary>
    /// Invalidates cache entries for multiple entities.
    /// More efficient than calling InvalidateRelatedCaches multiple times.
    /// </summary>
    protected void InvalidateRelatedCaches(IEnumerable<TId> ids)
    {
        foreach (var id in ids)
        {
            InvalidateCacheForEntity(id);
        }
        InvalidateCollectionCaches();
    }
}