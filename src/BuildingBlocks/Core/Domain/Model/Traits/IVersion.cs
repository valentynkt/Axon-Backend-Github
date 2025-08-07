namespace BuildingBlocks.Core.Model;

// For handling optimistic concurrency
/// <summary>
/// Represents an entity that supports versioning for optimistic concurrency control.
/// Follows SRP by focusing solely on versioning concerns.
/// </summary>
public interface IVersioned
{
    /// <summary>
    /// Gets or sets the version number used for optimistic concurrency control.
    /// </summary>
    long Version { get; set; }
}