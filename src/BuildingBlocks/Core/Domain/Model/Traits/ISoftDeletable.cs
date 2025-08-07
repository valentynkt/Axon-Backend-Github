namespace BuildingBlocks.Core.Model;

/// <summary>
/// Represents an entity that supports soft deletion.
/// Follows SRP by focusing solely on soft deletion concerns.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets or sets a value indicating whether this entity is logically deleted.
    /// When true, the entity should be treated as deleted without being physically removed from storage.
    /// </summary>
    bool IsDeleted { get; set; }
}