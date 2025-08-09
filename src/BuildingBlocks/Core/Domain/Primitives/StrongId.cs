using System.Diagnostics.CodeAnalysis;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base implementation for strongly-typed identifiers.
/// - Immutable, record-based
/// - Guards against default(TPrimitive)
/// - Implements value/equality semantics and conversions
/// </summary>
public abstract record StrongId<TPrimitive> : IStrongId<TPrimitive>, IComparable<StrongId<TPrimitive>>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    /// <summary>The underlying primitive value.</summary>
    public TPrimitive Value { get; }

    protected StrongId(TPrimitive value)
    {
        if (EqualityComparer<TPrimitive>.Default.Equals(value, default))
            throw new ArgumentException($"StrongId value cannot be default({typeof(TPrimitive).Name}).", nameof(value));

        Value = value;
    }

    #region IStrongId
    public object GetValue() => Value;
    public Type GetValueType() => typeof(TPrimitive);
    #endregion

    #region Comparison & Equality
    public int CompareTo(StrongId<TPrimitive>? other)
        => other is null ? 1 : Value.CompareTo(other.Value);

    public static bool operator <(StrongId<TPrimitive> left, StrongId<TPrimitive> right) => left.CompareTo(right) < 0;
    public static bool operator >(StrongId<TPrimitive> left, StrongId<TPrimitive> right) => left.CompareTo(right) > 0;
    public static bool operator <=(StrongId<TPrimitive> left, StrongId<TPrimitive> right) => left.CompareTo(right) <= 0;
    public static bool operator >=(StrongId<TPrimitive> left, StrongId<TPrimitive> right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString() ?? string.Empty;
    #endregion

    #region Conversions
    public static implicit operator TPrimitive(StrongId<TPrimitive> strongId) => strongId.Value;
    #endregion

    #region Protected helpers for derived factories
    /// <summary>
    /// Validates that a parsed primitive is non-default before constructing a derived StrongId.
    /// </summary>
    protected static void EnsureNotDefault([DoesNotReturnIf(true)] bool isDefault, string name)
    {
        if (isDefault)
            throw new ArgumentException($"StrongId value cannot be default({name}).", nameof(name));
    }
    #endregion
}
