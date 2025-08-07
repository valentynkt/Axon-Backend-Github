namespace BuildingBlocks.Core.Model;

/// <summary>
/// Marker interface for domain events following Epic 2 specifications.
/// Domain events represent something important that happened in the domain.
/// Processed without event sourcing - events are for integration, not state reconstruction.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this domain event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTime OccurredAt { get; }
    
    /// <summary>
    /// Version of the event schema (for evolution)
    /// </summary>
    int Version { get; }
}