using System.Security.Claims;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Messaging.Outbox;
using BuildingBlocks.Infrastructure.Persistence.PersistMessageProcessor;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events;

/// <summary>
/// Default implementation of IEventDispatcher that handles domain events, integration events, and internal commands.
/// Uses dependency injection to resolve mappers and processors for event handling.
/// Supports both single event and batch event processing with appropriate routing.
/// </summary>
public sealed class EventDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IEventMapper eventMapper,
    ILogger<EventDispatcher> logger,
    IPersistMessageProcessor persistMessageProcessor,
    IHttpContextAccessor httpContextAccessor)
    : IEventDispatcher
{
    public async Task SendAsync<T>(IReadOnlyList<T> events, Type? type = null,
                                   CancellationToken cancellationToken = default)
        where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        
        if (events.Count == 0)
        {
            logger.LogDebug("No events to process");
            return;
        }

        logger.LogDebug("Processing {EventCount} events of type {EventType}", events.Count, typeof(T).Name);

        var eventType = DetermineEventType(type);

        await ProcessEvents(events, eventType, cancellationToken);
        
        if (eventType == EventType.InternalCommand)
        {
            await ProcessInternalCommands(events, cancellationToken);
        }

        logger.LogDebug("Completed processing {EventCount} events", events.Count);
    }

    public async Task SendAsync<T>(T @event, Type? type = null,
        CancellationToken cancellationToken = default)
        where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        await SendAsync(new[] { @event }, type, cancellationToken);
    }

    private static EventType DetermineEventType(Type? type)
    {
        return type != null && type.IsAssignableTo(typeof(IInternalCommand))
            ? EventType.InternalCommand
            : EventType.DomainEvent;
    }

    private async Task ProcessEvents<T>(IReadOnlyList<T> events, EventType eventType, 
        CancellationToken cancellationToken) where T : IEvent
    {
        switch (events)
        {
            case IReadOnlyList<IDomainEvent> domainEvents:
                await ProcessDomainEvents(domainEvents, cancellationToken);
                break;

            case IReadOnlyList<IIntegrationEvent> integrationEvents:
                await PublishIntegrationEvents(integrationEvents, cancellationToken);
                break;
        }
    }

    private async Task ProcessDomainEvents(IReadOnlyList<IDomainEvent> domainEvents, 
        CancellationToken cancellationToken)
    {
        var integrationEvents = await MapDomainEventToIntegrationEventAsync(domainEvents)
            .ConfigureAwait(false);

        await PublishIntegrationEvents(integrationEvents, cancellationToken);
    }

    private async Task PublishIntegrationEvents(IReadOnlyList<IIntegrationEvent> integrationEvents, 
        CancellationToken cancellationToken)
    {
        foreach (var integrationEvent in integrationEvents)
        {
            await persistMessageProcessor.PublishMessageAsync(
                new MessageEnvelope(integrationEvent, SetHeaders()),
                cancellationToken);
        }
    }

    private async Task ProcessInternalCommands<T>(IReadOnlyList<T> events, 
        CancellationToken cancellationToken) where T : IEvent
    {
        if (events is not IReadOnlyList<IDomainEvent> domainEvents)
            return;
            
        var internalMessages = await MapDomainEventToInternalCommandAsync(domainEvents)
            .ConfigureAwait(false);

        foreach (var internalMessage in internalMessages)
        {
            await persistMessageProcessor.AddInternalMessageAsync(internalMessage, cancellationToken);
        }
    }


    private Task<IReadOnlyList<IIntegrationEvent>> MapDomainEventToIntegrationEventAsync(
        IReadOnlyList<IDomainEvent> events)
    {
        logger.LogTrace("Processing integration events start...");

        var wrappedIntegrationEvents = GetWrappedIntegrationEvents(events.ToList())?.ToList();
        if (wrappedIntegrationEvents?.Count > 0)
            return Task.FromResult<IReadOnlyList<IIntegrationEvent>>(wrappedIntegrationEvents);

        var integrationEvents = new List<IIntegrationEvent>();
        using var scope = serviceScopeFactory.CreateScope();
        foreach (var @event in events)
        {
            var eventType = @event.GetType();
            logger.LogTrace("Handling domain event: {EventTypeName}", eventType.Name);

            var integrationEvent = eventMapper.MapToIntegrationEvent(@event);

            if (integrationEvent is null)
                continue;

            integrationEvents.Add(integrationEvent);
        }

        logger.LogTrace("Processing integration events done...");

        return Task.FromResult<IReadOnlyList<IIntegrationEvent>>(integrationEvents);
    }


    private Task<IReadOnlyList<IInternalCommand>> MapDomainEventToInternalCommandAsync(
        IReadOnlyList<IDomainEvent> events)
    {
        logger.LogTrace("Processing internal message start...");

        var internalCommands = new List<IInternalCommand>();
        using var scope = serviceScopeFactory.CreateScope();
        foreach (var @event in events)
        {
            var eventType = @event.GetType();
            logger.LogTrace("Handling domain event: {EventTypeName}", eventType.Name);

            var integrationEvent = eventMapper.MapToInternalCommand(@event);

            if (integrationEvent is null)
                continue;

            internalCommands.Add(integrationEvent);
        }

        logger.LogTrace("Processing internal message done...");

        return Task.FromResult<IReadOnlyList<IInternalCommand>>(internalCommands);
    }

    private static IEnumerable<IIntegrationEvent> GetWrappedIntegrationEvents(IReadOnlyList<IDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents.Where(x => x is IHaveIntegrationEvent))
        {
            if (domainEvent is IHaveIntegrationEvent haveIntegrationEvent)
            {
                // Use the new GetIntegrationEvents method
                foreach (var integrationEvent in haveIntegrationEvent.GetIntegrationEvents())
                {
                    yield return integrationEvent;
                }
            }
            else
            {
                // Fallback to the wrapper approach for backward compatibility
                var genericType = typeof(IntegrationEventWrapper<>)
                    .MakeGenericType(domainEvent.GetType());

                var domainNotificationEvent = (IIntegrationEvent)Activator
                    .CreateInstance(genericType, domainEvent)!;

                yield return domainNotificationEvent;
            }
        }
    }

    private Dictionary<string, object?> SetHeaders()
    {
        var headers = new Dictionary<string, object?>();
        
        var correlationId = httpContextAccessor?.HttpContext?.GetCorrelationId();
        if (correlationId is not null)
        {
            headers.Add("CorrelationId", correlationId);
        }
        
        var userId = httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            headers.Add("UserId", userId);
        }
        
        var userName = httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
        if (userName is not null)
        {
            headers.Add("UserName", userName);
        }

        return headers;
    }
}