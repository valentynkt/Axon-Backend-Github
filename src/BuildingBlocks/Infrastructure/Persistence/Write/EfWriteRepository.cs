using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Generic Entity Framework write repository implementation
/// Handles aggregates with domain events and transactional consistency
/// </summary>
public class EfWriteRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TAggregate, TId> : IWriteRepository<TAggregate, TId>
    where TAggregate : class, IAggregateRoot<TId>
    where TId : notnull
{
    private readonly DbContext _context;
    private readonly DbSet<TAggregate> _dbSet;

    protected DbContext DbContext => _context;
    protected DbSet<TAggregate> DbSet => _dbSet;

    public EfWriteRepository(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<TAggregate>();
    }

    // ——— C R E A T E ———
    public virtual async Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        
        var entry = await _dbSet.AddAsync(aggregate, ct);
        return entry.Entity;
    }

    public virtual async Task<IReadOnlyList<TAggregate>> AddRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        var aggregatesList = aggregates.ToList();
        await _dbSet.AddRangeAsync(aggregatesList, ct);
        return aggregatesList.AsReadOnly();
    }

    // ——— U P D A T E ———
    public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var entry = _context.Entry(aggregate);

        if (entry.State == EntityState.Detached)
        {
            // For detached entities, Update() handles the entire graph correctly
            _dbSet.Update(aggregate);
        }
        else
        {
            // For tracked entities, use Update() which handles the entire object graph correctly
            // This is actually more reliable than trying to manually track navigation changes
            _dbSet.Update(aggregate);
        }

        return Task.FromResult(aggregate);
    }

    public virtual Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        
        var aggregatesList = aggregates.ToList();
        _dbSet.UpdateRange(aggregatesList);
        return Task.FromResult<IReadOnlyList<TAggregate>>(aggregatesList.AsReadOnly());
    }

    // ——— R E A D (for modification) ———
    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync([id], ct);
    }

    // ——— D E L E T E ———
    public virtual async Task DeleteAsync(TId id, CancellationToken ct = default)
    {
        var aggregate = await GetByIdAsync(id, ct);
        if (aggregate != null)
        {
            _dbSet.Remove(aggregate);
        }
    }

    public virtual Task DeleteAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        _dbSet.Remove(aggregate);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        _dbSet.RemoveRange(aggregates);
        return Task.CompletedTask;
    }

    // ——— E x i s t e n c e / A n y ———
    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(CreateIdPredicate(id), ct);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return await _dbSet.AnyAsync(predicate, ct);
    }

    public virtual async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(ct);
    }

    // ——— H e l p e r  M e t h o d s ———

    /// <summary>
    /// Determines if an entity is new (not persisted to database yet).
    /// Checks if the entity has a default ID value or if it's been explicitly marked as new.
    /// </summary>
    private static bool IsNewEntity(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Use reflection to get the Id property
        var entityType = entity.GetType();
        var idProperty = entityType.GetProperty("Id");

        if (idProperty == null)
            return false;

        var idValue = idProperty.GetValue(entity);

        // Check if the ID is default/empty (indicates new entity)
        if (idValue == null)
            return true;

        // Handle Guid IDs (most common case)
        if (idValue is Guid guidId)
            return guidId == Guid.Empty;

        // Handle integer IDs
        if (idValue is int intId)
            return intId == 0;

        // Handle long IDs
        if (idValue is long longId)
            return longId == 0;

        // Handle StrongId types that might have a Value property
        var valueProperty = idValue.GetType().GetProperty("Value");
        if (valueProperty != null)
        {
            var actualValue = valueProperty.GetValue(idValue);
            if (actualValue is Guid strongGuidId)
                return strongGuidId == Guid.Empty;
        }

        // If we can't determine, assume it's not new
        return false;
    }

    protected virtual Expression<Func<TAggregate, bool>> CreateIdPredicate(TId id)
    {
        var parameter = Expression.Parameter(typeof(TAggregate), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);
        var idConstant = Expression.Constant(id, typeof(TId));
        var equality = Expression.Equal(idPropertyAccess, idConstant);
        
        return Expression.Lambda<Func<TAggregate, bool>>(equality, parameter);
    }

    protected virtual System.Reflection.PropertyInfo GetIdProperty()
    {
        var idProperty = typeof(TAggregate).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(TId))
        {
            return idProperty;
        }
        
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Repository doesn't own the DbContext, so we don't dispose it
    }
}

[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2091",
    Justification = "Entity Framework repository pattern requires reflection for entity operations")]
public class EfWriteRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TAggregate> : EfWriteRepository<TAggregate, Guid>, IWriteRepository<TAggregate>
    where TAggregate : class, IAggregateRoot<Guid>
{
    public EfWriteRepository(DbContext context) : base(context) { }
}
