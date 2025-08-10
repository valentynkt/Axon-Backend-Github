using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// Handler for a specific integration event type.
/// Handlers are registered via DI and resolved by the integration event dispatcher.
/// Multiple handlers can be registered for the same event type.
/// </summary>
/// <typeparam name="TEvent">Type of integration event to handle</typeparam>
public interface IIntegrationEventHandler<in TEvent> 
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Handle the integration event.
    /// Handlers should be idempotent as events may be delivered multiple times.
    /// </summary>
    /// <param name="event">The integration event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the handling operation</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}