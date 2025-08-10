using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Dispatching;

/// <summary>
/// Dispatches outbound integration events for cross-boundary communication.
/// Handles domain event to integration event mapping and publishing (Lane B).
/// </summary>
public interface IIntegrationEventDispatcher
{
    /// <summary>
    /// Asynchronously dispatches a collection of domain events for outbound publishing.
    /// Domain events are mapped to integration events and published across boundaries.
    /// </summary>
    /// <param name="events">The collection of domain events to dispatch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously dispatches a single domain event for outbound publishing.
    /// The event is mapped to integration events and published according to routing configuration.
    /// </summary>
    /// <param name="event">The domain event to dispatch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(IDomainEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously dispatches a collection of integration events for direct publishing.
    /// Integration events are published directly without mapping.
    /// </summary>
    /// <param name="events">The collection of integration events to dispatch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(IReadOnlyList<IIntegrationEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously dispatches a single integration event for direct publishing.
    /// The event is published directly according to routing configuration.
    /// </summary>
    /// <param name="event">The integration event to dispatch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default);
}