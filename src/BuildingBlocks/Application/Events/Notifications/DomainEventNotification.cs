using BuildingBlocks.Core.Domain.Events;
using MediatR;

namespace BuildingBlocks.Application.Events.Notifications;

/// <summary>
/// Factory methods for creating domain event notifications.
/// </summary>
public static class DomainEventNotification
{
    public static DomainEventNotification<TDomainEvent> Wrap<TDomainEvent>(TDomainEvent e)
        where TDomainEvent : IDomainEvent => new(e);
}

/// <summary>
/// MediatR notification wrapper for domain events.
/// Use for in-process, post-commit policy/orchestration within the same process/module.
/// Cross-boundary communication must go through Outbox + IHaveIntegrationEvent.
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

    // Static factory method moved to non-generic utility class to avoid CA1000
}