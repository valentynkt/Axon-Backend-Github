using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Infrastructure.Messaging.Outbox;

/// <summary>
/// Wrapper for domain events that need to be published as integration events.
/// Provides a bridge between domain events and integration event publishing.
/// </summary>
/// <typeparam name="TDomainEventType">The type of domain event being wrapped</typeparam>
public record IntegrationEventWrapper<TDomainEventType>(TDomainEventType DomainEvent) : IntegrationEventBase
    where TDomainEventType : IDomainEvent
{
    /// <summary>
    /// The wrapped domain event containing the actual business data.
    /// </summary>
    public TDomainEventType DomainEvent { get; } = DomainEvent;
}