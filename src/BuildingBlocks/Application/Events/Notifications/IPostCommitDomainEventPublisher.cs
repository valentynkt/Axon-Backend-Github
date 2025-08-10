using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Notifications;

/// <summary>
/// Publishes in-process MediatR notifications for domain events AFTER a successful commit.
/// This is Lane A only (no cross-boundary I/O).
/// </summary>
public interface IPostCommitDomainEventPublisher
{
    Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    Task PublishAsync(IDomainEvent @event, CancellationToken ct = default);
}