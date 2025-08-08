using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Interface for dispatching domain events after aggregate persistence.
/// This integrates with Epic 5 Pipeline Behaviors for reliable event processing.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatch domain events asynchronously after successful transaction commit.
    /// This is called by the TransactionBehavior in Epic 5.
    /// </summary>
    /// <param name="domainEvents">Domain events to dispatch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure of event dispatch</returns>
    Task<Result<Unit>> DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Dispatch a single domain event asynchronously.
    /// </summary>
    /// <param name="domainEvent">Domain event to dispatch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure of event dispatch</returns>
    Task<Result<Unit>> DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain event dispatcher implementation that integrates with MediatR.
/// Works with Epic 5 Pipeline Behaviors for reliable event processing.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IMediator mediator,
        ILogger<DomainEventDispatcher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Unit>> DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        var events = domainEvents.ToList();
        if (!events.Any())
        {
            return Result<Unit>.Success(Unit.Value);
        }

        using var scope = _logger.BeginScope("DomainEventDispatch");
        _logger.LogDebug("Dispatching {EventCount} domain events", events.Count);

        var errors = new List<Error>();

        foreach (var domainEvent in events)
        {
            var result = await DispatchSingleEventAsync(domainEvent, cancellationToken);
            if (result.IsFailure)
            {
                errors.Add(result.Error);
                
                // Log error but continue processing other events
                _logger.LogError(
                    "Failed to dispatch domain event {EventType} with ID {EventId}: {Error}",
                    domainEvent.GetType().Name,
                    domainEvent.EventId,
                    result.Error.Message);
            }
        }

        if (errors.Any())
        {
            _logger.LogError(
                "Domain event dispatch completed with {ErrorCount} failures out of {EventCount} events",
                errors.Count,
                events.Count);
            
            return Result<Unit>.Failure(Error.Aggregate(errors.ToArray()));
        }

        _logger.LogDebug("Successfully dispatched all {EventCount} domain events", events.Count);
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Unit>> DispatchAsync(
        IDomainEvent domainEvent, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return await DispatchSingleEventAsync(domainEvent, cancellationToken);
    }

    private async Task<Result<Unit>> DispatchSingleEventAsync(
        IDomainEvent domainEvent, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var eventScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventType"] = domainEvent.GetType().Name,
                ["EventId"] = domainEvent.EventId,
                ["EventVersion"] = domainEvent.Version
            });

            _logger.LogDebug(
                "Dispatching domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name,
                domainEvent.EventId);

            // Publish through MediatR - this will go through Epic 5 pipeline behaviors
            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogDebug(
                "Successfully dispatched domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name,
                domainEvent.EventId);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception occurred while dispatching domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name,
                domainEvent.EventId);

            return Result<Unit>.Failure(
                Error.Failure(
                    $"Failed to dispatch domain event {domainEvent.GetType().Name}: {ex.Message}",
                    "DOMAIN_EVENT_DISPATCH_FAILED"));
        }
    }
}

/// <summary>
/// Extensions for aggregate root to support domain event dispatch integration.
/// These methods work with Epic 5 Pipeline Behaviors.
/// </summary>
public static class DomainEventExtensions
{
    /// <summary>
    /// Dispatch all domain events from an aggregate and clear them.
    /// This is typically called by the TransactionBehavior after successful persistence.
    /// </summary>
    /// <typeparam name="TAggregate">Type of aggregate</typeparam>
    /// <param name="aggregate">The aggregate with domain events</param>
    /// <param name="dispatcher">Domain event dispatcher</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure of event dispatch</returns>
    public static async Task<Result<Unit>> DispatchDomainEventsAsync<TAggregate>(
        this TAggregate aggregate,
        IDomainEventDispatcher dispatcher,
        CancellationToken cancellationToken = default)
        where TAggregate : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (!aggregate.DomainEvents.Any())
        {
            return Result<Unit>.Success(Unit.Value);
        }

        // Get events before clearing (in case of partial failure)
        var events = aggregate.DomainEvents.ToList();
        
        // Dispatch events
        var result = await dispatcher.DispatchAsync(events, cancellationToken);
        
        // Only clear events if dispatch was successful
        // If dispatch fails, events remain for retry or manual processing
        if (result.IsSuccess)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// Dispatch domain events from multiple aggregates efficiently.
    /// This batches events from multiple aggregates for better performance.
    /// </summary>
    /// <param name="aggregates">Aggregates with domain events</param>
    /// <param name="dispatcher">Domain event dispatcher</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure of event dispatch</returns>
    public static async Task<Result<Unit>> DispatchDomainEventsAsync(
        this IEnumerable<IAggregateRoot> aggregates,
        IDomainEventDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var aggregateList = aggregates.ToList();
        if (!aggregateList.Any())
        {
            return Result<Unit>.Success(Unit.Value);
        }

        // Collect all events from all aggregates
        var allEvents = aggregateList
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        if (!allEvents.Any())
        {
            return Result<Unit>.Success(Unit.Value);
        }

        // Dispatch all events in batch
        var result = await dispatcher.DispatchAsync(allEvents, cancellationToken);

        // Only clear events if dispatch was successful
        if (result.IsSuccess)
        {
            foreach (var aggregate in aggregateList)
            {
                aggregate.ClearDomainEvents();
            }
        }

        return result;
    }
}