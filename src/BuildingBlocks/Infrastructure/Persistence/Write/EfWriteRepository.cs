using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Write;

/// <summary>
/// Generic Entity Framework write repository implementation
/// Handles aggregates with domain events and transactional consistency
/// </summary>
public class EfWriteRepository<TAggregate, TId> : IWriteRepository<TAggregate, TId>
    where TAggregate : class, IAggregate<TId>
    where TId : notnull
{
    protected DbContext Context { get; }
    protected DbSet<TAggregate> DbSet { get; }

    public EfWriteRepository(DbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = Context.Set<TAggregate>();
    }

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<TAggregate?> GetByIdAsync(
        TId id,
        params Expression<Func<TAggregate, object>>[] includes)
    {
        var query = DbSet.AsQueryable();
        
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        
        return await query.FirstOrDefaultAsync(CreateIdPredicate(id));
    }

    public virtual async Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        var entry = await DbSet.AddAsync(aggregate, cancellationToken);
        return entry.Entity;
    }

    public virtual async Task<IReadOnlyList<TAggregate>> AddRangeAsync(
        IReadOnlyList<TAggregate> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        var aggregatesList = aggregates.ToList();
        await DbSet.AddRangeAsync(aggregatesList, cancellationToken);
        return aggregatesList.AsReadOnly();
    }

    public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        var entry = DbSet.Update(aggregate);
        return Task.FromResult(entry.Entity);
    }

    public virtual Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(
        IReadOnlyList<TAggregate> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        var aggregatesList = aggregates.ToList();
        DbSet.UpdateRange(aggregatesList);
        return Task.FromResult<IReadOnlyList<TAggregate>>(aggregatesList.AsReadOnly());
    }

    public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var aggregate = await GetByIdAsync(id, cancellationToken);
        if (aggregate != null)
        {
            DbSet.Remove(aggregate);
        }
    }

    public virtual Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        DbSet.Remove(aggregate);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(
        IReadOnlyList<TAggregate> aggregates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        DbSet.RemoveRange(aggregates);
        return Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(CreateIdPredicate(id), cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// Creates a predicate expression for finding aggregate by ID
    /// </summary>
    protected virtual Expression<Func<TAggregate, bool>> CreateIdPredicate(TId id)
    {
        var parameter = Expression.Parameter(typeof(TAggregate), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);
        var idConstant = Expression.Constant(id, typeof(TId));
        var equality = Expression.Equal(idPropertyAccess, idConstant);
        
        return Expression.Lambda<Func<TAggregate, bool>>(equality, parameter);
    }

    /// <summary>
    /// Gets the ID property of the aggregate
    /// </summary>
    protected virtual System.Reflection.PropertyInfo GetIdProperty()
    {
        // First try to find "Id" property
        var idProperty = typeof(TAggregate).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(TId))
        {
            return idProperty;
        }
        
        // If not found, look for properties ending with "Id"
        var properties = typeof(TAggregate).GetProperties()
            .Where(p => p.Name.EndsWith("Id") && p.PropertyType == typeof(TId))
            .ToList();
            
        if (properties.Count == 1)
        {
            return properties[0];
        }
        
        throw new InvalidOperationException(
            $"Unable to determine ID property for type {typeof(TAggregate).Name}. " +
            "Please ensure the aggregate has a property named 'Id' or override CreateIdPredicate method.");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Context?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}