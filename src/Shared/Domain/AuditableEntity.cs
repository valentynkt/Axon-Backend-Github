using BuildingBlocks.Core.Model;

namespace Axon.Shared.Domain;

/// <summary>
/// Marker interface for auditable entities that use backing fields pattern per SPARC architecture
/// </summary>
public interface IAuditable { }

/// <summary>
/// Base class for auditable domain entities using backing fields pattern from SPARC architecture
/// Audit properties are managed through backing fields accessed directly by audit interceptor
/// EF Core uses .HasField() configuration to map backing fields for optimal performance
/// Compatible with both Shared.Domain and BuildingBlocks.Core patterns
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable, IEntity<TId>
    where TId : notnull
{
    // Backing fields for EF Core direct access via audit interceptor (SPARC performance optimization)
    // These fields are accessed directly by the audit interceptor without going through properties
#pragma warning disable CS0649 // Field is never assigned to (assigned by EF Core audit interceptor)
    private DateTime _createdAtUtc;
    private string _createdBy = default!;
    private DateTime _updatedAtUtc;
    private string _updatedBy = default!;
    private long _version;
    private bool _isDeleted;
#pragma warning restore CS0649

    // Public read-only properties - no setters as audit interceptor accesses backing fields directly
    public DateTime CreatedAtUtc => _createdAtUtc;
    public string CreatedBy => _createdBy;
    public DateTime UpdatedAtUtc => _updatedAtUtc;
    public string UpdatedBy => _updatedBy;

    // Backward compatibility properties for application layer
    public DateTime CreatedAt => _createdAtUtc;
    public DateTime UpdatedAt => _updatedAtUtc;
    
    // IEntity interface compatibility (BuildingBlocks.Core.Model)
    DateTime? IEntity.CreatedAt 
    { 
        get => _createdAtUtc == default ? null : _createdAtUtc;
        set => _createdAtUtc = value ?? DateTime.UtcNow;
    }
    
    long? IEntity.CreatedBy 
    { 
        get => long.TryParse(_createdBy, out var id) ? id : null;
        set => _createdBy = value?.ToString() ?? string.Empty;
    }
    
    DateTime? IEntity.LastModified 
    { 
        get => _updatedAtUtc == default ? null : _updatedAtUtc;
        set => _updatedAtUtc = value ?? DateTime.UtcNow;
    }
    
    long? IEntity.LastModifiedBy 
    { 
        get => long.TryParse(_updatedBy, out var id) ? id : null;
        set => _updatedBy = value?.ToString() ?? string.Empty;
    }
    
    bool IEntity.IsDeleted 
    { 
        get => _isDeleted;
        set => _isDeleted = value;
    }
    
    long IVersion.Version 
    { 
        get => _version;
        set => _version = value;
    }

    protected AuditableEntity(TId id) : base(id)
    {
    }

    /// <summary>
    /// Gets the creation timestamp in UTC (domain accessor pattern)
    /// </summary>
    public DateTime GetCreatedAt() => _createdAtUtc;

    /// <summary>
    /// Gets the user who created this entity (domain accessor pattern)
    /// </summary>
    public string GetCreatedBy() => _createdBy;

    /// <summary>
    /// Gets the last update timestamp in UTC (domain accessor pattern)
    /// </summary>
    public DateTime GetUpdatedAt() => _updatedAtUtc;

    /// <summary>
    /// Gets the user who last updated this entity (domain accessor pattern)
    /// </summary>
    public string GetUpdatedBy() => _updatedBy;

    /// <summary>
    /// Checks if this entity was created by the specified user
    /// </summary>
    public bool WasCreatedBy(string userId) => 
        string.Equals(_createdBy, userId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if this entity was last updated by the specified user
    /// </summary>
    public bool WasLastUpdatedBy(string userId) => 
        string.Equals(_updatedBy, userId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the age of this entity since creation
    /// </summary>
    public TimeSpan GetAge() => DateTime.UtcNow - _createdAtUtc;

    /// <summary>
    /// Gets the time since last update
    /// </summary>
    public TimeSpan GetTimeSinceLastUpdate() => DateTime.UtcNow - _updatedAtUtc;

    /// <summary>
    /// Marks this entity as modified for domain logic.
    /// EF Core change tracking automatically detects entity modifications and the audit interceptor
    /// handles timestamp updates by directly accessing backing fields for optimal performance.
    /// This method exists as a domain signal and placeholder for future domain-specific logic.
    /// </summary>
    protected void MarkAsModified()
    {
        // EF Core change tracking handles modification detection automatically
        // Audit interceptor handles timestamp updates via direct backing field access
        // This method serves as a domain signal and placeholder for future domain logic
    }
}