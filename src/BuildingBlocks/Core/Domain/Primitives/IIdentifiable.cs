namespace BuildingBlocks.Core.Model;

/// <summary>
/// Represents an entity that has a unique identifier.
/// Follows SRP by focusing solely on identity concerns.
/// </summary>
/// <typeparam name="TValue">The type of the identifier</typeparam>
public interface IIdentifiable<TValue> where TValue : struct, IEquatable<TValue>
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    TValue? Id { get; }
}