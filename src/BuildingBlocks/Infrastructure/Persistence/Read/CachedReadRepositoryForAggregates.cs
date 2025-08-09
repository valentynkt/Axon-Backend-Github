using System.Linq.Expressions;
using BuildingBlocks.Core.Abstractions.Pagination;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Infrastructure.Persistence.Common;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Decorator for IReadRepository that adds caching functionality specifically for aggregates
/// This is the correct decorator for entities that implement IAggregateRoot
/// </summary>
public class CachedReadRepositoryForAggregates<TAggregate, TId> : CacheManagerBase<TAggregate, TId>, IReadRepository<TAggregate, TId>
    where TAggregate : class, IAggregateRoot<TId>
    where TId : IStrongId
{
    private readonly IReadRepository<TAggregate, TId> _inner;

    public CachedReadRepositoryForAggregates(
        IReadRepository<TAggregate, TId> inner,
        IMemoryCache cache,
        ILogger<CachedReadRepositoryForAggregates<TAggregate, TId>> logger,
        TimeSpan? cacheExpiration = null)
        : base(cache, logger, cacheExpiration)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<TAggregate?> FindByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(id);
        
        if (TryGetCache<TAggregate?>(cacheKey, out var cachedEntity))
        {
            return cachedEntity;
        }

        var entity = await _inner.FindByIdAsync(id, cancellationToken);
        
        if (entity != null)
        {
            SetCache(cacheKey, entity);
        }

        return entity;
    }

    public async Task<TAggregate?> FindOneAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // For complex predicates, we skip caching to avoid cache key complexity
        return await _inner.FindOneAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<TAggregate>> FindAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        // For complex queries, we skip caching
        return await _inner.FindAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<TAggregate>> RawQueryAsync(
        string query,
        CancellationToken cancellationToken = default,
        params object[] queryParams)
    {
        // Raw queries are not cached due to complexity
        return await _inner.RawQueryAsync(query, cancellationToken, queryParams);
    }

    public async Task<long> CountAsync(
        Expression<Func<TAggregate, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        // Only cache simple count operations (no predicate)
        if (predicate == null)
        {
            var cacheKey = GetCountCacheKey();
            
            if (TryGetCache<long>(cacheKey, out var cachedCount))
            {
                return cachedCount;
            }

            var count = await _inner.CountAsync(predicate, cancellationToken);
            SetCache(cacheKey, count);
            return count;
        }

        return await _inner.CountAsync(predicate, cancellationToken);
    }

    public async Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return await _inner.AnyAsync(predicate, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await _inner.AnyAsync(cancellationToken);
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _inner.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TAggregate>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TAggregate>();
        var uncachedIds = new List<TId>();

        // Check cache for each ID
        foreach (var id in ids)
        {
            var cacheKey = GetCacheKey(id);
            if (TryGetCache<TAggregate?>(cacheKey, out var cachedEntity) && cachedEntity != null)
            {
                results.Add(cachedEntity);
            }
            else
            {
                uncachedIds.Add(id);
            }
        }

        // Fetch uncached entities
        if (uncachedIds.Count > 0)
        {
            var uncachedEntities = await _inner.GetByIdsAsync(uncachedIds, cancellationToken);
            
            // Cache the newly fetched entities
            foreach (var entity in uncachedEntities)
            {
                if (entity.Id != null)
                {
                    var cacheKey = GetCacheKey(entity.Id);
                    SetCache(cacheKey, entity);
                }
            }

            results.AddRange(uncachedEntities);
        }

        return results;
    }

    public async Task<IPageList<TAggregate>> GetPagedAsync<TPageRequest>(
        TPageRequest request,
        CancellationToken cancellationToken = default)
        where TPageRequest : IPageRequest
    {
        return await _inner.GetPagedAsync(request, cancellationToken);
    }

    public async Task<IPageList<TAggregate>> GetPagedAsync(
        Expression<Func<TAggregate, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // For complex predicates, we skip caching
        if (predicate == null)
        {
            var cacheKey = GetPagedCacheKey(pageNumber, pageSize);
            
            if (TryGetCache<IPageList<TAggregate>>(cacheKey, out var cachedResult))
            {
                return cachedResult!;
            }

            var result = await _inner.GetPagedAsync(predicate, pageNumber, pageSize, cancellationToken);
            SetCache(cacheKey, result);
            return result;
        }

        return await _inner.GetPagedAsync(predicate, pageNumber, pageSize, cancellationToken);
    }

    public IQueryable<TAggregate> Query()
    {
        // IQueryable cannot be cached as it represents a query expression tree
        return _inner.Query();
    }

    public async Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IQueryable<TAggregate>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
    {
        return await _inner.ExecuteCompiledQueryAsync(compiledQuery, cancellationToken);
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