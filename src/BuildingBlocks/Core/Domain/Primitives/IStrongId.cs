namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Marker + accessors for strongly-typed identifiers.
/// Keeps ID semantics explicit and avoids primitive obsession.
/// </summary>
public interface IStrongId
{
    /// <summary>Gets the underlying primitive value (Guid, int, etc.).</summary>
    object GetValue();

    /// <summary>Gets the underlying primitive type.</summary>
    Type GetValueType();
}

/// <summary>
/// Strongly-typed identifier contract with the underlying primitive type known at compile-time.
/// </summary>
public interface IStrongId<out TPrimitive> : IStrongId
    where TPrimitive : struct
{
    /// <summary>The underlying primitive value.</summary>
    TPrimitive Value { get; }
}