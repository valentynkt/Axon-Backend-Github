using BuildingBlocks.Core.Abstractions.Time;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base for entities that require audit timestamps and soft deletion.
/// Kept separate from AggregateRoot; use composition in concrete aggregates if needed.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class AuditableDeletableEntity<TId> : Entity<TId>, IAuditable, ISoftDeletable
    where TId : notnull, IStrongId
{
    protected AuditableDeletableEntity(TId id) : base(id)
    {
        var now = Clock.Instance.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Parameterless ctor for ORM materialization only.
    /// </summary>
    protected AuditableDeletableEntity() : base() { }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Mark entity as updated (infra can also overwrite on save).</summary>
    protected void MarkUpdated() => UpdatedAt = Clock.Instance.UtcNow;

    protected void MarkCreated() => CreatedAt = Clock.Instance.UtcNow;

    /// <summary>Soft delete the entity.</summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = Clock.Instance.UtcNow;
        MarkUpdated();
    }

    /// <summary>Restore a soft-deleted entity.</summary>
    public virtual void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        DeletedAt = null;
        MarkUpdated();
    }
}