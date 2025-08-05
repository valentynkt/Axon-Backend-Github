namespace BuildingBlocks.Core.Model;

/// <summary>
/// Represents an entity that has a unique identifier.
/// Follows SRP by focusing solely on identity concerns.
/// </summary>
/// <typeparam name="T">The type of the identifier</typeparam>
public interface IIdentifiable<T>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    T? Id { get; set; }
}