namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Soft-deletion surface. Persistence and app layers decide how to filter.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Whether the entity is soft-deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>When the entity was soft-deleted (UTC), if applicable.</summary>
    DateTimeOffset? DeletedAt { get; }
}