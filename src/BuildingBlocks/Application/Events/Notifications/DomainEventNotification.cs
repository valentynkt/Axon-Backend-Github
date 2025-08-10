using BuildingBlocks.Core.Domain.Events;
using MediatR;

namespace BuildingBlocks.Application.Events.Notifications;

/// <summary>
/// MediatR notification wrapper for domain events.
/// Use for in-process, post-commit policy/orchestration within the same process/module.
/// Cross-boundary communication must go through Outbox + IEventMapper.
/// </summary>
/// <typeparam name="TDomainEvent">Type of domain event being wrapped</typeparam>
public sealed class DomainEventNotification<TDomainEvent> : INotification
    where TDomainEvent : IDomainEvent
{
    public DomainEventNotification(TDomainEvent domainEvent)
    {
        DomainEvent = domainEvent ?? throw new ArgumentNullException(nameof(domainEvent));
    }

    public TDomainEvent DomainEvent { get; }

    public Guid EventId => DomainEvent.EventId;
    public DateTime OccurredAt => DomainEvent.OccurredAt;
    public int Version => DomainEvent.Version;

    public static DomainEventNotification<TDomainEvent> Wrap(TDomainEvent e) => new(e);
}