using System.Diagnostics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Minimal entity base:
/// - Strongly-typed Id
/// - Identity-based equality (safe for transient instances)
/// </summary>
[DebuggerDisplay("{GetType().Name,nq}({Id})")]
public abstract class Entity<TId> : IEntity<TId>, IEquatable<Entity<TId>>
    where TId : IStrongId
{
    /// <summary>Create an entity with a valid Id.</summary>
    protected Entity(TId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    /// <summary>Parameterless ctor for ORM materialization only.</summary>
    protected Entity() { }

    public TId Id { get; protected set; } = default!;

    private static bool IsTransient(Entity<TId> e)
    {
        if (e.Id is null) return true;
        var value = e.Id.GetValue();
        if (value is null) return true;

        // Compare to default of underlying primitive type
        var type = e.Id.GetValueType();
        var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
        return Equals(value, defaultValue);
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (IsTransient(this) || IsTransient(other)) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() =>
        IsTransient(this)
            ? RuntimeHelpers.GetHashCode(this) // stable within process, avoids changing hash across states
            : HashCode.Combine(GetType(), Id);
}