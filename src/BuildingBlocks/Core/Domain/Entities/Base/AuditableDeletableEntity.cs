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
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Parameterless ctor for ORM materialization only.
    /// </summary>
    protected AuditableDeletableEntity() : base() { }

    // IAuditable
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }

    // ISoftDeletable
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }

    /// <summary>Mark entity as updated (infra can also overwrite on save).</summary>
    protected void MarkUpdated() => UpdatedAt = DateTimeOffset.UtcNow;
    
    protected void MarkCreated() => CreatedAt = DateTimeOffset.UtcNow;

    /// <summary>Soft delete the entity.</summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
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