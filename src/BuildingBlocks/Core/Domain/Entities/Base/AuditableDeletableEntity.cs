using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base for entities that require audit timestamps and soft deletion.
/// Kept separate from AggregateRoot; use composition in concrete aggregates if needed.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class AuditableDeletableEntity<TId> : Entity<TId>, IAuditable, ISoftDeletable
    where TId : notnull
{
    protected AuditableDeletableEntity(TId id) : base(id)
    {
        var now = TimeProvider.System.GetUtcNow();
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Parameterless ctor for ORM materialization only.</summary>
    protected AuditableDeletableEntity() : base() { }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Mark entity as updated (infra can also overwrite on save).</summary>
    protected void MarkUpdated() => UpdatedAt = TimeProvider.System.GetUtcNow();

    /// <summary>Mark entity as updated with a specific TimeProvider.</summary>
    protected void MarkUpdated(TimeProvider timeProvider) => UpdatedAt = timeProvider.GetUtcNow();

    protected void MarkCreated() => CreatedAt = TimeProvider.System.GetUtcNow();

    /// <summary>Internal method for infrastructure to set CreatedAt with custom TimeProvider.</summary>
    protected internal void SetCreatedAtInternal(DateTimeOffset createdAt) => CreatedAt = createdAt;

    /// <summary>Internal method for infrastructure to set UpdatedAt with custom TimeProvider.</summary>
    protected internal void SetUpdatedAtInternal(DateTimeOffset updatedAt) => UpdatedAt = updatedAt;

    /// <summary>Soft delete the entity.</summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = TimeProvider.System.GetUtcNow();
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