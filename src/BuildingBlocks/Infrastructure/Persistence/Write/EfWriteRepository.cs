using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Exceptions;
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
    /// <summary>
    /// Updates an aggregate in the database with optimistic concurrency control.
    ///
    /// How PostgreSQL xmin concurrency works:
    /// 1. The Version property (mapped to xmin) contains the row's transaction ID when last updated
    /// 2. For detached entities: Attach preserves the original Version value from when entity was loaded
    /// 3. When SaveChanges executes, EF Core includes "WHERE xmin = @originalVersion" in the UPDATE
    /// 4. PostgreSQL automatically updates xmin to a new value on successful update
    /// 5. If another transaction modified the row, the WHERE clause won't match and DbUpdateConcurrencyException is thrown
    ///
    /// This provides automatic optimistic concurrency without manual version management.
    /// </summary>
    public virtual Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // Validate concurrency token for better error messages
        ValidateConcurrencyToken(aggregate);

        var entry = _context.Entry(aggregate);

        if (entry.State == EntityState.Detached)
        {
            // For detached entities: Attach first, then set to Modified
            // This preserves the original Version value for the WHERE clause
            // EF Core + Npgsql will automatically handle xmin concurrency
            _dbSet.Attach(aggregate);
            entry.State = EntityState.Modified;

            // CRITICAL: Set the original Version value for concurrency check
            // Without this, EF won't include "WHERE ... AND xmin = @originalVersion"
            entry.Property(e => e.Version).IsModified = false;
            entry.Property(e => e.Version).OriginalValue = aggregate.Version;
        }
        else if (entry.State == EntityState.Unchanged || entry.State == EntityState.Modified)
        {
            // CRITICAL: For tracked aggregates, ensure UpdatedAt is set to trigger UPDATE
            // This is essential for aggregates with owned entities in separate tables
            if (aggregate is BuildingBlocks.Core.Domain.Entities.Base.AuditableDeletableEntity<Guid> auditable)
            {
                // Set UpdatedAt to now - this ensures an UPDATE is generated even if only children changed
                auditable.SetUpdatedAtInternal(TimeProvider.System.GetUtcNow());

                // CRITICAL: Explicitly mark UpdatedAt property as modified in EF Core's change tracker
                // This guarantees EF Core will generate an UPDATE statement for this property
                entry.Property(nameof(BuildingBlocks.Core.Domain.Entities.Base.AuditableDeletableEntity<Guid>.UpdatedAt)).IsModified = true;
            }

            // Mark aggregate as Modified to generate UPDATE statement
            entry.State = EntityState.Modified;

            // CRITICAL: Ensure Version property is not modified and preserves its original value
            // This ensures EF Core includes the concurrency check in the WHERE clause
            entry.Property(e => e.Version).IsModified = false;
        }

        return Task.FromResult(aggregate);
    }

    public virtual Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(
        IReadOnlyList<TAggregate> aggregates,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        // Validate all aggregates have valid concurrency tokens
        foreach (var aggregate in aggregates)
        {
            ValidateConcurrencyToken(aggregate);
        }

        // Use the same Attach pattern as UpdateAsync to preserve concurrency tokens
        foreach (var aggregate in aggregates)
        {
            var entry = _context.Entry(aggregate);

            if (entry.State == EntityState.Detached)
            {
                // For detached entities: Attach first, then set to Modified
                // This preserves the original Version value for the WHERE clause
                _dbSet.Attach(aggregate);
                entry.State = EntityState.Modified;

                // CRITICAL: Preserve original Version for concurrency
                entry.Property(e => e.Version).IsModified = false;
                entry.Property(e => e.Version).OriginalValue = aggregate.Version;
            }
            // For already tracked entities, EF Core's change tracking handles everything
        }

        return Task.FromResult(aggregates);
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
    /// Safely executes a save operation with proper concurrency exception handling.
    /// Wraps EF Core's DbUpdateConcurrencyException in our custom ConcurrencyException.
    /// </summary>
    protected virtual async Task<T> ExecuteWithConcurrencyHandlingAsync<T>(Func<Task<T>> operation, string operationName = "Database operation")
    {
        try
        {
            return await operation();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Convert EF Core concurrency exception to our domain exception
            throw new ConcurrencyException(
                $"{operationName} failed due to a concurrency conflict. The entity may have been modified by another process. Original error: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely executes a save operation with proper concurrency exception handling.
    /// Wraps EF Core's DbUpdateConcurrencyException in our custom ConcurrencyException.
    /// </summary>
    protected virtual async Task ExecuteWithConcurrencyHandlingAsync(Func<Task> operation, string operationName = "Database operation")
    {
        try
        {
            await operation();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Convert EF Core concurrency exception to our domain exception
            throw new ConcurrencyException(
                $"{operationName} failed due to a concurrency conflict. The entity may have been modified by another process. Original error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that an aggregate has the required concurrency token before update operations.
    /// </summary>
    protected virtual void ValidateConcurrencyToken(TAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // For PostgreSQL xmin, the version should never be 0 for existing entities
        // New entities will have Version = 0, but we don't update new entities
        if (aggregate.Version == 0)
        {
            throw new InvalidOperationException(
                $"Aggregate {typeof(TAggregate).Name} has an invalid concurrency token (Version = 0). " +
                "This may indicate the entity was not properly loaded from the database or is a new entity being incorrectly updated.");
        }
    }

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
