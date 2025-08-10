using BuildingBlocks.Core.Abstractions.Events;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Publishing;

public sealed class NoOpIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<NoOpIntegrationEventPublisher> _logger;
    public NoOpIntegrationEventPublisher(ILogger<NoOpIntegrationEventPublisher> logger) => _logger = logger;

    public Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default)
    {
        var list = events?.ToList() ?? [];
        if (list.Count == 0)
        {
            _logger.LogDebug("NoOp publisher: no integration events to publish");
            return Task.CompletedTask;
        }

        _logger.LogDebug("NoOp publisher: published {Count} integration events - {Types}",
            list.Count, string.Join(", ", list.Select(e => e.GetType().Name)));
        return Task.CompletedTask;
    }
}