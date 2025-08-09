using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base entity that tracks creation and modification times.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
    where TId : notnull, IStrongId
{
    protected AuditableEntity(TId id) : base(id)
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Parameterless ctor for ORM materialization only.
    /// </summary>
    protected AuditableEntity() : base()
    {
    }

    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    /// <summary>
    /// Call when entity is updated to refresh UpdatedAt timestamp.
    /// Infrastructure typically overwrites this on save, but calling
    /// MarkUpdated keeps intent explicit.
    /// </summary>
    protected void MarkUpdated()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}