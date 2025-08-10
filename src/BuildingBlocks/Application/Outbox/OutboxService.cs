using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Thin façade that delegates to the integration event dispatcher.
/// Mapping and publishing happen here; durability/retry are handled by Infrastructure
/// (MassTransit Outbox) configured on the bus.
/// </summary>
public sealed class OutboxService : IOutboxService
{
    private readonly IIntegrationEventDispatcher _dispatcher;
    private readonly ILogger<OutboxService> _logger;

    public OutboxService(IIntegrationEventDispatcher dispatcher, ILogger<OutboxService> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        var list = domainEvents?.Where(e => e is not null).ToList() ?? new();
        if (list.Count == 0)
        {
            _logger.LogDebug("Outbox: no domain events to publish.");
            return;
        }

        await _dispatcher.SendAsync(list, cancellationToken: ct);
        _logger.LogDebug("Outbox: published {Count} domain events.", list.Count);
    }
}