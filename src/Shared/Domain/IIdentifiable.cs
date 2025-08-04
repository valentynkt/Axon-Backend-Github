namespace Axon.Shared.Domain;

/// <summary>
/// Defines a contract for entities that have a strongly-typed identifier
/// </summary>
/// <typeparam name="TId">The type of the identifier</typeparam>
public interface IIdentifiable<TId> where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity
    /// </summary>
    TId Id { get; }
}