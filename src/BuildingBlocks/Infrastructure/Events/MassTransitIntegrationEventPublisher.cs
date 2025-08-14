using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Core.Abstractions.Events;
using MassTransit;

namespace BuildingBlocks.Infrastructure.Events;

public sealed class MassTransitIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publish;
    private readonly IEnvelopeContextAccessor _ctx;

    public MassTransitIntegrationEventPublisher(IPublishEndpoint publish, IEnvelopeContextAccessor ctx)
    {
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    public async Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default)
    {
        if (events is null) return;

        var meta = _ctx.Current;

        foreach (var e in events)
        {
            await _publish.Publish(e, send =>
            {
                if (meta is null) return;

                if (!string.IsNullOrWhiteSpace(meta.TraceId))
                    send.Headers.Set("trace-id", meta.TraceId);

                if (meta.RequestId.HasValue)
                    send.Headers.Set("request-id", meta.RequestId.Value);

                if (!string.IsNullOrWhiteSpace(meta.TenantId))
                    send.Headers.Set("tenant-id", meta.TenantId);

                if (meta.Metadata is not null)
                    foreach (var (k, v) in meta.Metadata)
                        if (v is not null) send.Headers.Set(k, v);

                if (meta.OutboxEntryId != Guid.Empty)
                    send.Headers.Set("outbox-entry-id", meta.OutboxEntryId);

                if (meta.TransactionId != Guid.Empty)
                    send.Headers.Set("transaction-id", meta.TransactionId);
            }, ct);
        }
    }
}