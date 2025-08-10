using BuildingBlocks.Application.Events.Enveloping;

namespace BuildingBlocks.Application.Events.Publishing;

/// <summary>
/// Port for publishing integration event envelopes to external transport.
/// Implementation will be provided by Infrastructure layer (MassTransit, Kafka, etc.).
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publish multiple integration event envelopes.
    /// </summary>
    /// <param name="envelopes">Envelopes to publish</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the publishing operation</returns>
    Task PublishAsync(IReadOnlyList<IntegrationEventEnvelope> envelopes, CancellationToken ct = default);

    /// <summary>
    /// Publish a single integration event envelope.
    /// </summary>
    /// <param name="envelope">Envelope to publish</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the publishing operation</returns>
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default)
        => PublishAsync(new[] { envelope }, ct);
}