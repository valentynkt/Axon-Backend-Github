using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base entity that supports soft deletion.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class SoftDeletableEntity<TId> : Entity<TId>, ISoftDeletable
    where TId : notnull
{
    protected SoftDeletableEntity(TId id) : base(id) { }

    /// <summary>Parameterless ctor for ORM materialization only.</summary>
    protected SoftDeletableEntity() : base() { }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Soft delete the entity.</summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = TimeProvider.System.GetUtcNow();
    }

    /// <summary>Restore a soft-deleted entity.</summary>
    public virtual void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        DeletedAt = null;
    }
}