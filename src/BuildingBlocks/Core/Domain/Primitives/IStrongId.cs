namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Marker interface for strongly-typed identifiers
/// </summary>
public interface IStrongId
{
    /// <summary>
    /// Get the underlying primitive value
    /// </summary>
    object GetValue();
    
    /// <summary>
    /// Get the type of the underlying primitive
    /// </summary>
    Type GetValueType();
}

/// <summary>
/// Generic strongly-typed identifier interface
/// </summary>
public interface IStrongId<TPrimitive> : IStrongId
    where TPrimitive : struct
{
    TPrimitive Value { get; }
}
