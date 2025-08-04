using MediatR;

namespace Axon.Shared.Domain;

/// <summary>
/// Marker interface for domain events with enhanced event sourcing support
/// Extends MediatR.INotification for event publishing integration
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Gets the date and time when the domain event occurred
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier of the domain event
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the version of the event schema for evolution support
    /// </summary>
    int EventVersion { get; }

    /// <summary>
    /// Gets the event type name for serialization and routing
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the unique identifier of the domain event (alias for EventId for compatibility)
    /// </summary>
    Guid Id => EventId;
}