using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// Registry for resolving integration event handlers based on event types.
/// Implementations should integrate with the DI container to locate registered handlers.
/// </summary>
public interface IIntegrationEventHandlerRegistry
{
    /// <summary>
    /// Get all registered handlers for a specific integration event type.
    /// Returns empty collection if no handlers are registered.
    /// </summary>
    /// <param name="eventType">The integration event type</param>
    /// <returns>Collection of handlers for the event type</returns>
    IEnumerable<IIntegrationEventHandler<IIntegrationEvent>> GetHandlers(Type eventType);

    /// <summary>
    /// Get all registered handlers for a specific integration event instance.
    /// Convenience method that calls GetHandlers(event.GetType()).
    /// </summary>
    /// <param name="event">The integration event instance</param>
    /// <returns>Collection of handlers for the event type</returns>
    IEnumerable<IIntegrationEventHandler<IIntegrationEvent>> GetHandlers(IIntegrationEvent @event)
        => GetHandlers(@event.GetType());
}