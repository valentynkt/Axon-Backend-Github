namespace BuildingBlocks.Core.Model;

/// <summary>
/// Marker/contract for strong-typed IDs (wrapping a primitive).
/// Entities keep their Id on Entity types; IDs expose Value.
/// </summary>
public interface IStrongId<TPrimitive> where TPrimitive : struct
{
    TPrimitive Value { get; }
}
