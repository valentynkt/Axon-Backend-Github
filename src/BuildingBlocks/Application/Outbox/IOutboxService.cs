using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Minimal outbox façade for KISS/YAGNI:
/// - Application maps and publishes domain events
/// - Infrastructure (MassTransit EF Outbox) guarantees reliability
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Publish a batch of domain events.
    /// Infrastructure should be configured to use MassTransit Outbox so that
    /// Publish is captured transactionally and delivered reliably.
    /// </summary>
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);

    /// <summary>
    /// Convenience overload.
    /// </summary>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
        => PublishAsync(new[] { domainEvent }, ct);
}