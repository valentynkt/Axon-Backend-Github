namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for all domain entities.
/// Provides identity, audit trail, and soft delete capabilities.
/// </summary>
public abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : IStrongId
{
    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }
    
    protected Entity()
    {
        // For ORM frameworks that require parameterless constructor
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 1;
    }
    
    /// <summary>
    /// Entity identifier
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
    public bool IsDeleted { get; protected set; }
    
    /// <summary>
    /// Soft delete timestamp
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }
    
    /// <summary>
    /// Version for optimistic locking - managed by ORM only
    /// Should be configured with [Timestamp] attribute or .IsRowVersion() in EF Core
    /// </summary>
    public uint Version { get; protected set; }
    
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
    /// Update the entity's modification timestamp
    /// Version is managed automatically by the ORM for optimistic concurrency
    /// </summary>
    protected void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        // Version is managed by ORM (e.g., [Timestamp] or .IsRowVersion())
        // Manual increment is dangerous and leads to state mismatch
    }
    
    /// <summary>
    /// Mark entity as deleted (soft delete)
    /// </summary>
    protected virtual void MarkAsDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Touch();
    }
    
    /// <summary>
    /// Restore a soft-deleted entity
    /// </summary>
    protected virtual void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        Touch();
    }
    
    #endregion
}

/// <summary>
/// Interface for entities with strongly-typed identifiers
/// </summary>
public interface IEntity<TId> : IEntity
    where TId : IStrongId
{
    TId Id { get; }
}

/// <summary>
/// Non-generic entity interface
/// </summary>
public interface IEntity : IAuditable
{
    uint Version { get; }
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
}

/// <summary>
/// Interface for auditable entities
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
}
