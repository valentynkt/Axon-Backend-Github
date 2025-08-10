using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MassTransit;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// MassTransit-backed publisher for integration events.
/// KISS: just iterate and publish. Reliability is handled by MassTransit's EF Outbox.
/// </summary>
public sealed class MassTransitIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitIntegrationEventPublisher> _logger;

    public MassTransitIntegrationEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitIntegrationEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default)
    {
        if (events is null) return;

        var tasks = new List<Task>(8);
        foreach (var e in events)
        {
            if (e is null) continue;

            // Publish as the concrete runtime type so routing/topology works naturally.
            tasks.Add(_publishEndpoint.Publish(e, e.GetType(), ct));
        }

        if (tasks.Count == 0)
        {
            _logger.LogDebug("No integration events to publish.");
            return;
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("Published {Count} integration event(s) via MassTransit.", tasks.Count);
    }
}