using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Core.Model;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Write;

/// <summary>
/// PostgreSQL implementation of write repository for CQRS command operations
/// Handles aggregates with domain events and change tracking
/// </summary>
public class PostgresWriteRepository<TAggregate, TId> : IWriteRepository<TAggregate, TId>
    where TAggregate : class, IAggregate<TId>
    where TId : notnull
{
    private readonly IWriteDbContext<object> _context;
    private readonly DbSet<TAggregate> _dbSet;
    private readonly ILogger<PostgresWriteRepository<TAggregate, TId>> _logger;
    private bool _disposed;

    public PostgresWriteRepository(
        IWriteDbContext<object> context,
        ILogger<PostgresWriteRepository<TAggregate, TId>>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<TAggregate>();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresWriteRepository<TAggregate, TId>>.Instance;
    }

    public virtual async Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        _logger.LogDebug("Adding aggregate {AggregateType} with ID {Id}", typeof(TAggregate).Name, aggregate.Id);
        
        var entry = await _dbSet.AddAsync(aggregate, cancellationToken);
        return entry.Entity;
    }    public virtual async Task<IReadOnlyList<TAggregate>> AddRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        _logger.LogDebug("Adding {Count} aggregates of type {AggregateType}", aggregates.Count, typeof(TAggregate).Name);
        
        await _dbSet.AddRangeAsync(aggregates, cancellationToken);
        return aggregates;
    }

    public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        _logger.LogDebug("Updating aggregate {AggregateType} with ID {Id}", typeof(TAggregate).Name, aggregate.Id);
        
        var entry = _dbSet.Update(aggregate);
        return Task.FromResult(entry.Entity);
    }

    public virtual Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        _logger.LogDebug("Updating {Count} aggregates of type {AggregateType}", aggregates.Count, typeof(TAggregate).Name);
        
        _dbSet.UpdateRange(aggregates);
        return Task.FromResult(aggregates);
    }

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        
        _logger.LogDebug("Getting aggregate {AggregateType} by ID {Id}", typeof(TAggregate).Name, id);
        
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }    public virtual async Task<TAggregate?> GetByIdAsync(
        TId id, 
        params Expression<Func<TAggregate, object>>[] includes)
    {
        ArgumentNullException.ThrowIfNull(id);
        
        _logger.LogDebug("Getting aggregate {AggregateType} by ID {Id} with {IncludeCount} includes", 
            typeof(TAggregate).Name, id, includes?.Length ?? 0);
        
        var query = _dbSet.AsQueryable();
        
        if (includes != null && includes.Length > 0)
        {
            query = includes.Aggregate(query, (current, include) => current.Include(include));
        }
        
        return await query.FirstOrDefaultAsync(a => a.Id!.Equals(id));
    }

    public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        
        _logger.LogDebug("Deleting aggregate {AggregateType} by ID {Id}", typeof(TAggregate).Name, id);
        
        var aggregate = await GetByIdAsync(id, cancellationToken);
        if (aggregate != null)
        {
            _dbSet.Remove(aggregate);
        }
    }

    public virtual Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        _logger.LogDebug("Deleting aggregate {AggregateType} with ID {Id}", typeof(TAggregate).Name, aggregate.Id);
        
        _dbSet.Remove(aggregate);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        _logger.LogDebug("Deleting {Count} aggregates of type {AggregateType}", aggregates.Count, typeof(TAggregate).Name);
        
        _dbSet.RemoveRange(aggregates);
        return Task.CompletedTask;
    }    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        
        _logger.LogDebug("Checking if aggregate {AggregateType} exists with ID {Id}", typeof(TAggregate).Name, id);
        
        return await _dbSet.AnyAsync(a => a.Id!.Equals(id), cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        _logger.LogDebug("Checking if any aggregate {AggregateType} matches predicate", typeof(TAggregate).Name);
        
        return await _dbSet.AnyAsync(predicate, cancellationToken);
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
/// Simplified PostgreSQL write repository for aggregates with Guid IDs
/// </summary>
public class PostgresWriteRepository<TAggregate> : PostgresWriteRepository<TAggregate, Guid>, IWriteRepository<TAggregate>
    where TAggregate : class, IAggregate<Guid>
{
    public PostgresWriteRepository(
        IWriteDbContext<object> context,
        ILogger<PostgresWriteRepository<TAggregate, Guid>>? logger = null)
        : base(context, logger)
    {
    }
}