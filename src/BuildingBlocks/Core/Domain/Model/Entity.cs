using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;

namespace BuildingBlocks.Core.Domain.Model;

/// <summary>
/// Base class for all domain entities following Epic 2 specifications.
/// Provides identity, audit trail, versioning, and soft delete capabilities.
/// Integrates with IStrongId system and Epic 1 functional foundation.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier implementing IStrongId</typeparam>
public abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : IStrongId
{
    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = false;
        Version = 1; // Initial version for new entities
    }
    
    protected Entity()
    {
        // For ORM frameworks that require parameterless constructor
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = false;
        Version = 1;
    }
    
    /// <summary>
    /// Entity identifier using strongly-typed ID
    /// </summary>
    public TId Id { get; protected set; } = default!;
    
    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; protected set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; protected set; }
    
    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// Soft delete timestamp
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }
    
    /// <summary>
    /// Version for optimistic locking - managed by ORM only
    /// Should be configured with [Timestamp] attribute or .IsRowVersion() in EF Core
    /// Manual increment is dangerous and leads to concurrency issues
    /// </summary>
    public long Version { get; set; }
    
    #region Identity and Equality
    
    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        
        return Id.Equals(other.Id);
    }
    
    public override bool Equals(object? obj)
    {
        return Equals(obj as Entity<TId>);
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }
    
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Update the entity's modification timestamp.
    /// Version is managed automatically by the ORM for optimistic concurrency.
    /// Manual version increment is dangerous and leads to state mismatch.
    /// </summary>
    protected void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        // Version is managed by ORM (e.g., [Timestamp] or .IsRowVersion())
        // Manual increment is dangerous and leads to state mismatch
    }
    
    /// <summary>
    /// Mark entity as deleted (soft delete).
    /// Returns Result to maintain functional programming patterns.
    /// </summary>
    protected virtual Result<Unit> MarkAsDeleted()
    {
        if (IsDeleted)
        {
            return Result<Unit>.Failure(
                Error.BusinessRule("Entity is already deleted", "ENTITY_ALREADY_DELETED"));
        }
        
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Touch();
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Restore a soft-deleted entity.
    /// Returns Result to maintain functional programming patterns.
    /// </summary>
    protected virtual Result<Unit> Restore()
    {
        if (!IsDeleted)
        {
            return Result<Unit>.Failure(
                Error.BusinessRule("Entity is not deleted", "ENTITY_NOT_DELETED"));
        }
        
        IsDeleted = false;
        DeletedAt = null;
        Touch();
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    #endregion
}