using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Events;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.Messaging;

/// <summary>
/// Abstraction for publishing integration events to external message brokers.
/// Supports both single and batch publishing with comprehensive error handling,
/// health monitoring, and delivery confirmation capabilities.
/// </summary>
public interface IExternalEventPublisher
{
    /// <summary>
    /// Publishes a single integration event to the external message broker.
    /// </summary>
    /// <param name="integrationEvent">The integration event to publish</param>
    /// <param name="options">Publishing options and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing publishing details on success, or error on failure</returns>
    Task<Result<EventPublishResult>> PublishAsync(
        IIntegrationEvent integrationEvent,
        PublishOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple integration events to the external message broker in a single batch operation.
    /// Provides atomic-like behavior where possible, with detailed results for each event.
    /// </summary>
    /// <param name="integrationEvents">Collection of integration events to publish</param>
    /// <param name="options">Publishing options and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing batch publishing details on success, or error on failure</returns>
    Task<Result<BatchPublishResult>> PublishBatchAsync(
        IEnumerable<IIntegrationEvent> integrationEvents,
        PublishOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current health status of the external event publisher.
    /// Includes connectivity, performance metrics, and capacity information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing publisher health status on success, or error on failure</returns>
    Task<Result<PublisherHealth>> GetHealthAsync(
        CancellationToken cancellationToken = default);
}