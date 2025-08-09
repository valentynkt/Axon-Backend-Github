using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Convenience base for entities that need audit timestamps.
/// Infra (e.g., EF SaveChanges interceptor) should set/populate these fields.
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
    where TId : IStrongId
{
    protected AuditableEntity(TId id) : base(id) { }
    protected AuditableEntity() { }

    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>
    /// Signal a mutation in domain logic. Infra typically overwrites UpdatedAt on save,
    /// but calling Touch keeps intent explicit and aids in non-EF persistence paths.
    /// </summary>
    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}