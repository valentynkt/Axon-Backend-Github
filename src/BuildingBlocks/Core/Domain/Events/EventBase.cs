using BuildingBlocks.Core.Domain.Events;
using MassTransit;

namespace BuildingBlocks.Core.Event;

/// <summary>
/// Abstract base record for all events in the system.
/// Provides consistent implementation of event metadata and identification.
/// Uses record type for value equality and immutability benefits.
/// </summary>
public abstract record EventBase : IEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; } = NewId.NextGuid();
    
    /// <summary>
    /// When this event occurred in UTC.
    /// </summary>
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// The type name of this event for serialization and routing.
    /// </summary>
    public string EventType { get; } = "";
    
    /// <summary>
    /// Protected constructor that sets the event type based on the concrete implementation.
    /// </summary>
    protected EventBase()
    {
        EventType = GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;
    }
}

/// <summary>
/// Abstract base record for domain events.
/// Inherits all base event functionality while maintaining domain event semantics.
/// </summary>
public abstract record DomainEventBase : EventBase, IDomainEvent
{
}

/// <summary>
/// Abstract base record for integration events.
/// Inherits all base event functionality while maintaining integration event semantics.
/// </summary>
public abstract record IntegrationEventBase : EventBase, IIntegrationEvent
{
}

/// <summary>
/// Abstract base record for internal commands.
/// Inherits all base event functionality while maintaining internal command semantics.
/// </summary>
public abstract record InternalCommandBase : EventBase, IInternalCommand
{
}