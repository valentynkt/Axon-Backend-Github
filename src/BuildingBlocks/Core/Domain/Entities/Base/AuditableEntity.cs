using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base entity that tracks creation and modification times.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable
    where TId : notnull
{
    protected AuditableEntity(TId id) : base(id)
    {
        var now = TimeProvider.System.GetUtcNow();
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Parameterless ctor for ORM materialization only.</summary>
    protected AuditableEntity() : base() { }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Mark entity as updated (infra can also overwrite on save).</summary>
    protected void MarkUpdated() => UpdatedAt = TimeProvider.System.GetUtcNow();

    /// <summary>Mark entity as updated with a specific TimeProvider.</summary>
    protected void MarkUpdated(TimeProvider timeProvider) => UpdatedAt = timeProvider.GetUtcNow();

    protected void MarkCreated() => CreatedAt = TimeProvider.System.GetUtcNow();
}