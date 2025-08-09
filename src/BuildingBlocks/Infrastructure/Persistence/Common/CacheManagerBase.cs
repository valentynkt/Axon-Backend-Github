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
/// Generic version for any ID type (used by read repositories).
/// </summary>
public abstract class CacheManagerBase<TEntity, TId>
    where TEntity : class
    where TId : notnull
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly TimeSpan _cacheExpiration;
    private readonly string _entityTypeName;

    protected CacheManagerBase(
        IMemoryCache cache,
        ILogger logger,
        TimeSpan? cacheExpiration = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(15);
        _entityTypeName = typeof(TEntity).Name;
    }

    /// <summary>Generates a cache key for a specific entity ID.</summary>
    protected virtual string GetCacheKey(TId id) => $"{_entityTypeName}_{id}";

    /// <summary>Gets cache key for paginated results.</summary>
    protected string GetPagedCacheKey(int pageNumber, int pageSize) => 
        $"{_entityTypeName}_Paged_{pageNumber}_{pageSize}";

    /// <summary>Gets cache key for count operations.</summary>
    protected string GetCountCacheKey() => $"{_entityTypeName}_Count";

    /// <summary>Sets a value in the cache with the configured expiration.</summary>
    protected virtual void SetCache<T>(string key, T value)
    {
        _cache.Set(key, value!, _cacheExpiration);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cached {EntityType} with key {CacheKey} for {Expiration}",
                _entityTypeName, key, _cacheExpiration);
        }
    }

    /// <summary>Tries to get a value from the cache.</summary>
    protected virtual bool TryGetCache<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out value))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Cache hit for {EntityType} with key {CacheKey}", 
                    _entityTypeName, key);
            }
            return true;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cache miss for {EntityType} with key {CacheKey}", 
                _entityTypeName, key);
        }
        return false;
    }

    /// <summary>Invalidates cache for a specific entity ID.</summary>
    protected virtual void InvalidateCache(TId id)
    {
        var key = GetCacheKey(id);
        _cache.Remove(key);
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Invalidated cache for {EntityType} with key {CacheKey}", 
                _entityTypeName, key);
        }
    }

    /// <summary>Invalidates all related cache entries.</summary>
    protected virtual void InvalidateRelatedCache(TId id)
    {
        InvalidateCache(id);
        
        // Also invalidate collection-level caches
        _cache.Remove(GetCountCacheKey());
        
        // Remove all paged cache entries (simplified approach)
        // In production, consider using cache tags or a more sophisticated approach
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Invalidated related caches for {EntityType} with ID {Id}", 
                _entityTypeName, id);
        }
    }

    /// <summary>Invalidates cache for multiple entities.</summary>
    protected virtual void InvalidateRelatedCache(IEnumerable<TId> ids)
    {
        foreach (var id in ids)
        {
            InvalidateCache(id);
        }
        
        // Also invalidate collection-level caches
        _cache.Remove(GetCountCacheKey());
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Invalidated caches for {Count} {EntityType} entities", 
                ids.Count(), _entityTypeName);
        }
    }
}

/// <summary>
/// Specialized cache manager for entities with strong IDs (used by write repositories).
/// </summary>
public abstract class StrongIdCacheManagerBase<TEntity, TId> : CacheManagerBase<TEntity, TId>
    where TEntity : class, IAggregateRoot<TId>
    where TId : IStrongId
{
    protected StrongIdCacheManagerBase(
        IMemoryCache cache,
        ILogger logger,
        TimeSpan? cacheExpiration = null)
        : base(cache, logger, cacheExpiration)
    {
    }
}