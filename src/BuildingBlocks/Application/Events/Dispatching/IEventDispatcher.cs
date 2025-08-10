using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Dispatching;

/// <summary>
/// Interface for dispatching events within the application layer.
/// Handles both individual events and collections of events, supporting
/// domain events, integration events, and internal commands.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Asynchronously sends a collection of events for processing.
    /// Events are processed according to their type and routing configuration.
    /// </summary>
    /// <typeparam name="T">The type of events being sent</typeparam>
    /// <param name="events">The collection of events to dispatch</param>
    /// <param name="type">Optional type hint for event processing</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync<T>(IReadOnlyList<T> events, Type? type = null, CancellationToken cancellationToken = default)
        where T : IEvent;

    /// <summary>
    /// Asynchronously sends a single event for processing.
    /// The event is processed according to its type and routing configuration.
    /// </summary>
    /// <typeparam name="T">The type of event being sent</typeparam>
    /// <param name="event">The event to dispatch</param>
    /// <param name="type">Optional type hint for event processing</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendAsync<T>(T @event, Type? type = null, CancellationToken cancellationToken = default)
        where T : IEvent;
}