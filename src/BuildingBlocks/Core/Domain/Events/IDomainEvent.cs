namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker interface for domain events
/// Domain events represent something important that happened in the domain
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
