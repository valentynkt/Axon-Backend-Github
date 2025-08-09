using Axon.Shared.Domain;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Model;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Service for publishing domain events
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a single domain event
    /// </summary>
    /// <param name="domainEvent">The domain event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple domain events
    /// </summary>
    /// <param name="domainEvents">The domain events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for handling domain event publishing from aggregates
/// </summary>
public interface IAggregateEventPublisher
{
    /// <summary>
    /// Publishes all pending domain events from an aggregate root
    /// </summary>
    /// <param name="aggregateRoot">The aggregate root with pending events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishEventsAsync<TId>(AggregateRoot<TId> aggregateRoot, CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Publishes all pending domain events from multiple aggregate roots
    /// </summary>
    /// <param name="aggregateRoots">The aggregate roots with pending events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishEventsAsync(IEnumerable<AggregateRoot<object>> aggregateRoots, CancellationToken cancellationToken = default);
}