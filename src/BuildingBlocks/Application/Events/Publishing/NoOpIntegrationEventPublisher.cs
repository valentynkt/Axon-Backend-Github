using BuildingBlocks.Application.Events.Enveloping;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Publishing;

/// <summary>
/// No-operation implementation of IIntegrationEventPublisher for development/testing.
/// Logs envelope publishing without actually sending to any transport.
/// </summary>
public sealed class NoOpIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<NoOpIntegrationEventPublisher> _logger;

    public NoOpIntegrationEventPublisher(ILogger<NoOpIntegrationEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PublishAsync(IReadOnlyList<IntegrationEventEnvelope> envelopes, CancellationToken ct = default)
    {
        if (envelopes.Count == 0)
        {
            _logger.LogDebug("NoOp publisher: no envelopes to publish");
            return Task.CompletedTask;
        }

        _logger.LogDebug(
            "NoOp publisher: published {Count} envelopes - {Types}",
            envelopes.Count,
            string.Join(", ", envelopes.Select(e => e.Type)));

        foreach (var envelope in envelopes)
        {
            _logger.LogTrace(
                "NoOp envelope {EnvelopeId}: {Type} with {HeaderCount} headers",
                envelope.EnvelopeId,
                envelope.Type,
                envelope.Headers.Count);
        }

        return Task.CompletedTask;
    }
}