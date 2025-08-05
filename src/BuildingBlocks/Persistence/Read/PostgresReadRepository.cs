using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Core.Pagination;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Read;

/// <summary>
/// PostgreSQL implementation of read repository for CQRS query operations
/// Optimized for read models with no tracking and query performance
/// </summary>
public class PostgresReadRepository<TReadModel, TId> : IReadRepository<TReadModel, TId>
    where TReadModel : class
    where TId : notnull
{
    private readonly IReadDbContext<object> _context;
    private readonly DbSet<TReadModel> _dbSet;
    private readonly ILogger<PostgresReadRepository<TReadModel, TId>> _logger;
    private bool _disposed;

    public PostgresReadRepository(
        IReadDbContext<object> context,
        ILogger<PostgresReadRepository<TReadModel, TId>>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<TReadModel>();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresReadRepository<TReadModel, TId>>.Instance;
    }

    public virtual async Task<TReadModel?> FindByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        
        _logger.LogDebug("Finding read model {ReadModelType} by ID {Id}", typeof(TReadModel).Name, id);
        
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(entity => entity.GetType().GetProperty("Id")!.GetValue(entity)!.Equals(id), cancellationToken);
    }    public virtual async Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _logger.LogDebug("Finding single read model {ReadModelType} with predicate", typeof(TReadModel).Name);
        
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _logger.LogDebug("Finding read models {ReadModelType} with predicate", typeof(TReadModel).Name);
        
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<IPagedResult<TReadModel>> GetPagedAsync<TPageRequest>(
        TPageRequest request, 
        CancellationToken cancellationToken = default) 
        where TPageRequest : IPageRequest
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        
        _logger.LogDebug("Getting paged read models {ReadModelType} - Page {Page}, Size {PageSize}", 
            typeof(TReadModel).Name, request.PageNumber, request.PageSize);
        
        return await _dbSet.AsNoTracking().ToPagedResultAsync(request.PageNumber, request.PageSize, cancellationToken);
    }

    public virtual async Task<IPagedResult<TReadModel>> GetPagedAsync(
        Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting paged read models {ReadModelType} with custom predicate - Page {Page}, Size {PageSize}", 
            typeof(TReadModel).Name, pageNumber, pageSize);
        
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        return await query.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }    public virtual async Task<IReadOnlyList<TReadModel>> RawQueryAsync(
        string sql, 
        CancellationToken cancellationToken = default, 
        params object[] parameters)
    {
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be null or empty", nameof(sql));
        ArgumentNullException.ThrowIfNull(parameters);

        _logger.LogDebug("Executing raw query for read model {ReadModelType}: {Sql}", typeof(TReadModel).Name, sql);
        
        return await _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync(cancellationToken);
    }

    public virtual async Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting read models {ReadModelType}", typeof(TReadModel).Name);
        
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        return await query.LongCountAsync(cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if any read models {ReadModelType} exist", typeof(TReadModel).Name);
        
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        return await query.AnyAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        _logger.LogDebug("Getting read models {ReadModelType} by {Count} IDs", typeof(TReadModel).Name, ids.Count);
        
        // This is a simplified approach - in a real scenario, you'd need to handle the ID property dynamically
        return await _dbSet.AsNoTracking().Where(entity => ids.Contains((TId)entity.GetType().GetProperty("Id")!.GetValue(entity)!)).ToListAsync(cancellationToken);
    }    public virtual IQueryable<TReadModel> Query()
    {
        _logger.LogDebug("Getting queryable for read model {ReadModelType}", typeof(TReadModel).Name);
        
        return _dbSet.AsNoTracking();
    }

    public virtual async Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IQueryable<TReadModel>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiledQuery);

        _logger.LogDebug("Executing compiled query for read model {ReadModelType}", typeof(TReadModel).Name);
        
        try
        {
            return await compiledQuery(_dbSet.AsNoTracking());
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to execute compiled query for read model {ReadModelType}", typeof(TReadModel).Name);
            throw;
        }
    }

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
}

/// <summary>
/// Simplified PostgreSQL read repository for read models with Guid IDs
/// </summary>
public class PostgresReadRepository<TReadModel> : PostgresReadRepository<TReadModel, Guid>, IReadRepository<TReadModel>
    where TReadModel : class
{
    public PostgresReadRepository(
        IReadDbContext<object> context,
        ILogger<PostgresReadRepository<TReadModel, Guid>>? logger = null)
        : base(context, logger)
    {
    }
}