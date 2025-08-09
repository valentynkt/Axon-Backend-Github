namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something important that happened in the domain.
/// Extends IEvent to ensure consistent event properties across the system.
/// </summary>
public interface IDomainEvent : IEvent
{
    /// <summary>
    /// Version of the event schema (for evolution and compatibility).
    /// </summary>
    int Version { get; }
}
