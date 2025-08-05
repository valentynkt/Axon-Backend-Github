namespace Axon.Shared.Domain;

/// <summary>
/// Base class for domain entities with proper equality semantics and audit capabilities
/// Implements IIdentifiable for consistent entity identification patterns
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier</typeparam>
public abstract class BaseEntity<TId> : IIdentifiable<TId>, IEquatable<BaseEntity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity
    /// </summary>
    public TId Id { get; set; }

    protected BaseEntity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Determines whether two entities are equal based on their identifiers and types
    /// </summary>
    public bool Equals(BaseEntity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as BaseEntity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right) => !(left == right);

    public override string ToString() => $"{GetType().Name} [Id={Id}]";
}