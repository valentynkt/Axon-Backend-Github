using System.Linq.Expressions;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Core.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL repository implementation
/// Comprehensive implementation supporting read/write separation and all CRUD operations
/// </summary>
public class PostgresRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    protected readonly IPostgresDbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    protected readonly ILogger<PostgresRepository<TEntity, TId>> _logger;
    private bool _disposed;

    public PostgresRepository(
        IPostgresDbContext context,
        ILogger<PostgresRepository<TEntity, TId>>? logger = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = Context.GetCollection<TEntity>();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresRepository<TEntity, TId>>.Instance;
    }

    #region Read Operations (IReadRepository)

    public virtual async Task<TEntity?> FindByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding entity by ID: {Id}", id);
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id!.Equals(id), cancellationToken);
    }

    public virtual async Task<TEntity?> FindOneAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding single entity with predicate");
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding entities with predicate");
        return await DbSet
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all entities");
        return await DbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if entity exists with predicate");
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting entities");
        if (predicate == null)
        {
            return await DbSet.LongCountAsync(cancellationToken);
        }
        return await DbSet.LongCountAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if any entities exist");
        if (predicate == null)
        {
            return await DbSet.AnyAsync(cancellationToken);
        }
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting entities by IDs: {Count}", ids.Count);
        return await DbSet
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllPaginatedAsync(
        IPageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting paginated entities: Page {Page}, Size {PageSize}", pageRequest.PageNumber, pageRequest.PageSize);
        var skip = (pageRequest.PageNumber - 1) * pageRequest.PageSize;
        return await DbSet
            .AsNoTracking()
            .Skip(skip)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IPagedList<TEntity>> GetByPageFilter<TPageRequest>(
        TPageRequest request,
        CancellationToken cancellationToken = default)
        where TPageRequest : IPageRequest
    {
        _logger.LogDebug("Getting filtered paged entities");
        var skip = (request.PageNumber - 1) * request.PageSize;
        
        var totalCount = await DbSet.CountAsync(cancellationToken);
        var items = await DbSet
            .AsNoTracking()
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<TEntity>(items, request.PageNumber, request.PageSize, totalCount);
    }

    public virtual async Task<IReadOnlyList<TEntity>> RawQuery(
        string query,
        CancellationToken cancellationToken = default,
        params object[] queryParams)
    {
        _logger.LogDebug("Executing raw query: {Query}", query);
        return await DbSet
            .FromSqlRaw(query, queryParams)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Write Operations (IWriteRepository)

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _logger.LogDebug("Adding entity");
        
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    public virtual async Task<IReadOnlyList<TEntity>> AddRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _logger.LogDebug("Adding {Count} entities", entities.Count);
        
        await DbSet.AddRangeAsync(entities, cancellationToken);
        return entities;
    }

    public virtual Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _logger.LogDebug("Updating entity");
        
        var entry = DbSet.Update(entity);
        return Task.FromResult(entry.Entity);
    }

    public virtual Task<IReadOnlyList<TEntity>> UpdateRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _logger.LogDebug("Updating {Count} entities", entities.Count);
        
        DbSet.UpdateRange(entities);
        return Task.FromResult(entities);
    }

    public virtual async Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting entities with predicate");
        await DbSet.Where(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _logger.LogDebug("Deleting entity");
        
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting entity by ID: {Id}", id);
        await DbSet
            .Where(e => e.Id!.Equals(id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public virtual Task DeleteRangeAsync(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _logger.LogDebug("Deleting {Count} entities", entities.Count);
        
        DbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public virtual async Task<int> BulkUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, TEntity>> updateExpression,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Performing bulk update with predicate");
        return await DbSet
            .Where(predicate)
            .ExecuteUpdateAsync(updateExpression, cancellationToken);
    }

    public virtual async Task<int> BulkDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Performing bulk delete with predicate");
        return await DbSet
            .Where(predicate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // DbContext disposal is handled by DI container
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// PostgreSQL repository implementation for entities with Guid primary key
/// </summary>
public class PostgresRepository<TEntity> : PostgresRepository<TEntity, Guid>, IRepository<TEntity>
    where TEntity : class, IEntity<Guid>
{
    public PostgresRepository(
        IPostgresDbContext context,
        ILogger<PostgresRepository<TEntity, Guid>>? logger = null)
        : base(context, logger)
    {
    }
}