using MediatR;

namespace BuildingBlocks.Core.Event;

using global::MassTransit;

/// <summary>
/// Base interface for all events in the system.
/// Events represent something that has happened in the domain.
/// Follows SRP by focusing solely on event identification and metadata.
/// </summary>
public interface IEvent : INotification
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Should be set once during event creation.
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// When this event occurred in UTC.
    /// Should be set once during event creation.
    /// </summary>
    DateTime OccurredOn { get; }
    
    /// <summary>
    /// The type name of this event for serialization and routing.
    /// Should be set once during event creation.
    /// </summary>
    string EventType { get; }
}