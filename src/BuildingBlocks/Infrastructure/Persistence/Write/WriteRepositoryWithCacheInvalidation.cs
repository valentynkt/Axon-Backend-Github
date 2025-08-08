using System.Linq.Expressions;
using BuildingBlocks.Infrastructure.Persistence.Common;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Decorator for IWriteRepository that adds cache invalidation functionality
/// Follows the Decorator pattern and maintains CQRS separation
/// Automatically invalidates cache entries after write operations
/// </summary>
public class WriteRepositoryWithCacheInvalidation<TEntity, TId> : CacheManagerBase<TEntity, TId>, IWriteRepository<TEntity, TId>
    where TEntity : class, IAggregate<TId>
    where TId : notnull
{
    private readonly IWriteRepository<TEntity, TId> _inner;

    public WriteRepositoryWithCacheInvalidation(
        IWriteRepository<TEntity, TId> inner,
        IMemoryCache cache,
        ILogger<WriteRepositoryWithCacheInvalidation<TEntity, TId>> logger,
        TimeSpan? cacheExpiration = null)
        : base(cache, logger, cacheExpiration)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        var result = await _inner.AddAsync(entity, cancellationToken);
        
        // Invalidate cache after successful addition
        if (entity.Id != null)
        {
            InvalidateRelatedCache(entity.Id);
        }
        
        return result;
    }

    public async Task<IReadOnlyList<TEntity>> AddRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        
        var result = await _inner.AddRangeAsync(entities, cancellationToken);
        
        // Invalidate cache for all affected entities
        var entityIds = entities.Where(e => e.Id != null).Select(e => e.Id!).ToList();
        if (entityIds.Count > 0)
        {
            InvalidateRelatedCache(entityIds);
        }
        
        return result;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        var result = await _inner.UpdateAsync(entity, cancellationToken);
        
        // Invalidate cache after successful update
        if (entity.Id != null)
        {
            InvalidateRelatedCache(entity.Id);
        }
        
        return result;
    }

    public async Task<IReadOnlyList<TEntity>> UpdateRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        
        var result = await _inner.UpdateRangeAsync(entities, cancellationToken);
        
        // Invalidate cache for all affected entities
        var entityIds = entities.Where(e => e.Id != null).Select(e => e.Id!).ToList();
        if (entityIds.Count > 0)
        {
            InvalidateRelatedCache(entityIds);
        }
        
        return result;
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        // This is a read operation in a write repository
        // We don't cache here as it's meant for write scenarios (like update operations)
        return await _inner.GetByIdAsync(id, cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(TId id, params Expression<Func<TEntity, object>>[] includes)
    {
        // This is a read operation in a write repository
        // We don't cache here as it's meant for write scenarios
        return await _inner.GetByIdAsync(id, includes);
    }

    public async Task DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        await _inner.DeleteAsync(id, cancellationToken);
        
        // Invalidate cache after successful deletion
        InvalidateRelatedCache(id);
    }

    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        await _inner.DeleteAsync(entity, cancellationToken);
        
        // Invalidate cache after successful deletion
        if (entity.Id != null)
        {
            InvalidateRelatedCache(entity.Id);
        }
    }

    // Note: IWriteRepository uses DeleteAsync(TId id, ...) not DeleteByIdAsync
    // This method is removed as it doesn't exist in the interface

    public async Task DeleteRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        
        await _inner.DeleteRangeAsync(entities, cancellationToken);
        
        // Invalidate cache for all affected entities
        var entityIds = entities.Where(e => e.Id != null).Select(e => e.Id!).ToList();
        if (entityIds.Count > 0)
        {
            InvalidateRelatedCache(entityIds);
        }
    }

    public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        // This is a read operation in a write repository
        // We don't cache here as it's typically used in write scenarios
        return await _inner.ExistsAsync(id, cancellationToken);
    }

    public async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        // This is a read operation in a write repository
        // We don't cache here as it's typically used in write scenarios
        return await _inner.AnyAsync(predicate, cancellationToken);
    }

    public override void Dispose()
    {
        if (_inner is IDisposable disposableInner)
        {
            disposableInner.Dispose();
        }
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Write repository with cache invalidation for entities with long ID
/// </summary>
public class WriteRepositoryWithCacheInvalidation<TEntity> : WriteRepositoryWithCacheInvalidation<TEntity, long>
    where TEntity : class, IAggregate<long>
{
    public WriteRepositoryWithCacheInvalidation(
        IWriteRepository<TEntity, long> inner,
        IMemoryCache cache,
        ILogger<WriteRepositoryWithCacheInvalidation<TEntity, long>> logger,
        TimeSpan? cacheExpiration = null)
        : base(inner, cache, logger, cacheExpiration)
    {
    }
}