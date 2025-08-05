using System.Linq.Expressions;
using BuildingBlocks.Core.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.EFCore;

/// <summary>
/// Cached repository decorator for PostgreSQL repositories
/// Implements caching layer while maintaining Clean Architecture principles
/// </summary>
public interface ICachedRepository<TEntity, in TId> : IEfRepository<TEntity, TId>
    where TEntity : class, IAggregate<TId>
{
    Task InvalidateCacheAsync(TId id);
    Task InvalidateAllCacheAsync();
}

/// <summary>
/// Cached repository implementation using decorator pattern
/// </summary>
public class CachedRepository<TEntity, TId> : ICachedRepository<TEntity, TId>
    where TEntity : class, IAggregate<TId>
{
    private readonly IEfRepository<TEntity, TId> _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedRepository<TEntity, TId>> _logger;
    private readonly TimeSpan _cacheExpiration;
    private readonly string _cacheKeyPrefix;

    public CachedRepository(
        IEfRepository<TEntity, TId> repository,
        IMemoryCache cache,
        ILogger<CachedRepository<TEntity, TId>> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheExpiration = TimeSpan.FromMinutes(15); // Default cache expiration
        _cacheKeyPrefix = $"{typeof(TEntity).Name}_";
    }

    public async Task<TEntity?> FindByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(id);
        
        if (_cache.TryGetValue(cacheKey, out TEntity? cachedEntity))
        {
            _logger.LogDebug("Cache hit for {EntityType} with ID {Id}", typeof(TEntity).Name, id);
            return cachedEntity;
        }

        var entity = await _repository.FindByIdAsync(id, cancellationToken);
        
        if (entity != null)
        {
            _cache.Set(cacheKey, entity, _cacheExpiration);
            _logger.LogDebug("Cached {EntityType} with ID {Id}", typeof(TEntity).Name, id);
        }

        return entity;
    }

    public async Task<TEntity?> FindOneAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // For complex predicates, we skip caching to avoid cache key complexity
        return await _repository.FindOneAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // For complex queries, we skip caching
        return await _repository.FindAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = GetAllCacheKey();
        
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<TEntity>? cachedEntities))
        {
            _logger.LogDebug("Cache hit for all {EntityType} entities", typeof(TEntity).Name);
            return cachedEntities!;
        }

        var entities = await _repository.GetAllAsync(cancellationToken);
        _cache.Set(cacheKey, entities, _cacheExpiration);
        _logger.LogDebug("Cached all {EntityType} entities", typeof(TEntity).Name);

        return entities;
    }

    public async Task<IReadOnlyList<TEntity>> RawQuery(
        string query,
        CancellationToken cancellationToken = default,
        params object[] queryParams)
    {
        // Raw queries are not cached due to complexity
        return await _repository.RawQuery(query, cancellationToken, queryParams);
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await _repository.AddAsync(entity, cancellationToken);
        
        // Invalidate related cache entries
        if (entity.Id != null)
            await InvalidateRelatedCacheAsync(entity.Id);
        
        return result;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await _repository.UpdateAsync(entity, cancellationToken);
        
        // Invalidate cache for this entity
        if (entity.Id != null)
            await InvalidateCacheAsync(entity.Id);
        
        return result;
    }

    public async Task DeleteRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteRangeAsync(entities, cancellationToken);
        
        // Invalidate cache for all affected entities
        foreach (var entity in entities)
        {
            if (entity.Id != null)
            await InvalidateCacheAsync(entity.Id);
        }
    }

    public async Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(predicate, cancellationToken);
        
        // Invalidate all cache since we don't know which entities were affected
        await InvalidateAllCacheAsync();
    }

    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(entity, cancellationToken);
        if (entity.Id != null)
            await InvalidateCacheAsync(entity.Id);
    }

    public async Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteByIdAsync(id, cancellationToken);
        await InvalidateCacheAsync(id);
    }

    public async Task InvalidateCacheAsync(TId id)
    {
        var cacheKey = GetCacheKey(id);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for {EntityType} with ID {Id}", typeof(TEntity).Name, id);
        await Task.CompletedTask;
    }

    public async Task InvalidateAllCacheAsync()
    {
        var allCacheKey = GetAllCacheKey();
        _cache.Remove(allCacheKey);
        _logger.LogDebug("Invalidated all cache for {EntityType}", typeof(TEntity).Name);
        await Task.CompletedTask;
    }

    private async Task InvalidateRelatedCacheAsync(TId id)
    {
        await InvalidateCacheAsync(id);
        await InvalidateAllCacheAsync();
    }

    private string GetCacheKey(TId id) => $"{_cacheKeyPrefix}{id}";
    private string GetAllCacheKey() => $"{_cacheKeyPrefix}All";

    public void Dispose()
    {
        _repository?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Cached repository for entities with long ID
/// </summary>
public class CachedRepository<TEntity> : CachedRepository<TEntity, long>, ICachedRepository<TEntity, long>
    where TEntity : class, IAggregate<long>
{
    public CachedRepository(
        IEfRepository<TEntity, long> repository,
        IMemoryCache cache,
        ILogger<CachedRepository<TEntity, long>> logger)
        : base(repository, cache, logger)
    {
    }
}