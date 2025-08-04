namespace Axon.Shared.Domain;

/// <summary>
/// Marker interface for auditable entities that use backing fields pattern per SPARC architecture
/// </summary>
public interface IAuditable { }

/// <summary>
/// Base class for auditable domain entities using backing fields pattern from SPARC architecture
/// Audit properties are managed through backing fields with internal setters for infrastructure layer
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable
    where TId : notnull
{
    // Backing fields for EF Core direct access (performance optimization per SPARC)
    private DateTime _createdAtUtc;
    private string _createdBy = default!;
    private DateTime _updatedAtUtc;
    private string _updatedBy = default!;

    // Public read-only properties following SPARC pattern
    public DateTime CreatedAtUtc => _createdAtUtc;
    public string CreatedBy => _createdBy;
    public DateTime UpdatedAtUtc => _updatedAtUtc;
    public string UpdatedBy => _updatedBy;

    protected AuditableEntity(TId id) : base(id)
    {
    }

    // Internal setters for infrastructure layer only (SPARC pattern)
    internal void SetCreated(DateTime atUtc, string by)
    {
        _createdAtUtc = atUtc;
        _createdBy = by;
    }

    internal void SetUpdated(DateTime atUtc, string by)
    {
        _updatedAtUtc = atUtc;
        _updatedBy = by;
    }

    /// <summary>
    /// Marks this entity as modified for domain logic
    /// </summary>
    protected void MarkAsModified()
    {
        // Domain signal for modification - audit interceptor will handle timestamps
    }
}