namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for domain entities with strongly-typed identifiers.
/// Implements core entity concerns: identity, versioning, soft deletion.
/// Uses record type for value equality and immutability benefits.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public abstract record BaseEntity<T> : IEntity<T>
{
    // IIdentifiable<T> implementation
    public T? Id { get; set; }
    
    // ISoftDeletable implementation
    public bool IsDeleted { get; set; }
    
    // IVersioned implementation  
    public long Version { get; set; }
}

/// <summary>
/// Base implementation for auditable domain entities.
/// Extends BaseEntity with audit trail capabilities.
/// Use this for entities that require creation/modification tracking.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public abstract record BaseAuditableEntity<T> : BaseEntity<T>, IAuditableEntity<T>
{
    // IAuditable implementation
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public long? LastModifiedBy { get; set; }
}

/// <summary>
/// Legacy alias for backward compatibility.
/// Use BaseEntity<T> or BaseAuditableEntity<T> in new code.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
[Obsolete("Use BaseEntity<T> or BaseAuditableEntity<T> instead")]
public abstract record Entity<T> : BaseAuditableEntity<T>
{
}