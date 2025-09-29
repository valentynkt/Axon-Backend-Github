using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base class for owned entities that need audit and soft-delete capabilities.
/// Unlike regular entities, owned entities don't have independent identity
/// and are protected by their aggregate root's concurrency control.
/// </summary>
public abstract class OwnedAuditableEntity : IAuditable, ISoftDeletable
{
    /// <summary>
    /// Parameterless ctor for ORM materialization and derived classes.
    /// </summary>
    protected OwnedAuditableEntity()
    {
        var now = TimeProvider.System.GetUtcNow();
        CreatedAt = now;
        UpdatedAt = now;
    }

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