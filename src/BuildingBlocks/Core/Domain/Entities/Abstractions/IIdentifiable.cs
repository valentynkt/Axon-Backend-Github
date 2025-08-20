namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Marks an entity as having a strongly-typed identifier.
/// Useful for generic repositories and specifications.
/// </summary>
public interface IIdentifiable<out TId> where TId : notnull
{
    /// <summary>Entity unique identifier.</summary>
    TId Id { get; }
}