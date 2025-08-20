using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Minimal base type for domain entities.
/// Holds identity and value-based equality only.
/// No events and no concurrency here by design (aggregate concern).
/// </summary>
/// <typeparam name="TId">Strongly-typed ID (e.g., ConversationId)</typeparam>
public abstract class Entity<TId> : IEntity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        if (EqualityComparer<TId>.Default.Equals(id, default!))
            throw new ArgumentException("Entity id cannot be default(TId).", nameof(id));

        Id = id;
    }

    /// <summary>
    /// Parameterless ctor for ORM materialization only.
    /// Keep protected to avoid accidental public use.
    /// </summary>
    protected Entity() { }

    /// <summary>Entity unique identifier.</summary>
    public TId Id { get; protected set; } = default!;

    #region Equality

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        // Transient entities are never equal (no stable identity yet)
        if (IsTransient(this) || IsTransient(other))
            return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);

    public override int GetHashCode()
    {
        // For transient entities, use reference-based hash to avoid hash changes
        if (IsTransient(this))
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        return HashCode.Combine(GetType(), Id);
    }

    private static bool IsTransient(Entity<TId> entity)
        => EqualityComparer<TId>.Default.Equals(entity.Id, default!);

    #endregion
}
