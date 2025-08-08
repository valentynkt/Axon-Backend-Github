using System.Runtime.CompilerServices;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for all domain entities.
/// Provides identity, audit trail, and soft delete.
/// Equality is identity-based; transients (default Id) are never equal.
/// </summary>
public abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : IStrongId
{
    protected Entity(TId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    protected Entity() // for ORMs
    {
        // Audit fields are set again by SaveChanges interceptor.
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Entity identifier.</summary>
    public TId Id { get; protected set; } = default!; // default => transient

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>Soft delete timestamp.</summary>
    public DateTimeOffset? DeletedAt { get; protected set; }

    /// <summary>
    /// Concurrency token (provider-specific mapping).
    /// Use EF Core concurrency token (.IsRowVersion() / xmin etc.).
    /// </summary>
    public byte[]? ConcurrencyToken { get; protected set; }

    #region Identity & Equality

    private static bool IsTransient(Entity<TId> e) =>
        e.Id is null || e.Id.GetValue() is null ||
        Equals(e.Id.GetValue(), default);

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        // Transients are never equal to anything except reference-equal
        if (IsTransient(this) || IsTransient(other)) return false;

        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode()
    {
        // For transients, fall back to runtime hash to avoid collisions.
        if (IsTransient(this)) return RuntimeHelpers.GetHashCode(this);

        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
        !Equals(left, right);

    #endregion

    #region State Management

    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    protected virtual void MarkAsDeleted()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    protected virtual void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        DeletedAt = null;
        Touch();
    }

    #endregion
}

public interface IEntity<TId> : IEntity where TId : IStrongId
{
    TId Id { get; }
}

public interface IEntity : IAuditable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    byte[]? ConcurrencyToken { get; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}
