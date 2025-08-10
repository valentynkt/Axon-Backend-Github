namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Base marker interface for all events in the system.
/// Represents the fundamental contract for event-driven communication.
/// </summary>
public interface IEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Event schema/version for evolution compatibility.</summary>
    int Version { get; }

    /// <summary>Stable event name for routing and diagnostics.</summary>
    string Name { get; }
}