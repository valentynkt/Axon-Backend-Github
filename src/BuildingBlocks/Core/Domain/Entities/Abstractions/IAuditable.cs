namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Auditing surface: creation and modification timestamps.
/// Do not include actor (CreatedBy/UpdatedBy) here to avoid coupling;
/// add that in your app layer or a separate interface if needed.
/// </summary>
public interface IAuditable
{
    /// <summary>When the entity was created (UTC).</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>When the entity was last modified (UTC), if ever.</summary>
    DateTimeOffset? UpdatedAt { get; }
}