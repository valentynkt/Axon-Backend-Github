using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Application.Events.Mapping;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Dispatching;

/// <summary>
/// Application-pure dispatcher: DomainEvent -> map -> envelopes -> publisher.
/// Replaces legacy EventDispatcher with clean separation of concerns.
/// </summary>
public sealed class IntegrationEventDispatcher : IEventDispatcher
{
    private readonly IEventMapper _mapper;
    private readonly IIntegrationEventEnvelopeFactory _envelopeFactory;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IEnvelopeContextAccessor _contextAccessor;
    private readonly ILogger<IntegrationEventDispatcher> _logger;

    public IntegrationEventDispatcher(
        IEventMapper mapper,
        IIntegrationEventEnvelopeFactory envelopeFactory,
        IIntegrationEventPublisher publisher,
        IEnvelopeContextAccessor contextAccessor,
        ILogger<IntegrationEventDispatcher> logger)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync<T>(IReadOnlyList<T> events, Type? type = null, CancellationToken cancellationToken = default)
        where T : IEvent
    {
        if (events.Count == 0) 
        {
            _logger.LogDebug("No events to dispatch");
            return;
        }

        _logger.LogDebug("Dispatching {EventCount} events of type {EventType}", events.Count, typeof(T).Name);

        if (events is IReadOnlyList<IDomainEvent> domainEvents)
        {
            await ProcessDomainEvents(domainEvents, cancellationToken);
            return;
        }

        // Already integration events? Envelop & publish directly.
        if (events is IReadOnlyList<IIntegrationEvent> integrationEvents)
        {
            await ProcessIntegrationEvents(integrationEvents, cancellationToken);
            return;
        }

        _logger.LogWarning("Unsupported event type {EventType} for dispatching", typeof(T).Name);
    }

    public Task SendAsync<T>(T @event, Type? type = null, CancellationToken cancellationToken = default)
        where T : IEvent
        => SendAsync(new[] { @event }, type, cancellationToken);

    private async Task ProcessDomainEvents(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var integrationEvents = new List<IIntegrationEvent>();

            // Strategy 1: Check if domain event directly provides integration events
            if (domainEvent is IHaveIntegrationEvent haveIntegrationEvent)
            {
                integrationEvents.AddRange(haveIntegrationEvent.GetIntegrationEvents());
                _logger.LogDebug(
                    "Domain event {EventType} provided {Count} integration events directly",
                    domainEvent.GetType().Name, integrationEvents.Count);
            }

            // Strategy 2: Use mapper to transform domain -> integration
            var mappedEvent = _mapper.MapToIntegrationEvent(domainEvent);
            if (mappedEvent is not null)
            {
                integrationEvents.Add(mappedEvent);
                _logger.LogDebug(
                    "Domain event {EventType} mapped to integration event {IntegrationType}",
                    domainEvent.GetType().Name, mappedEvent.GetType().Name);
            }

            // Skip if no integration events produced
            if (integrationEvents.Count == 0)
            {
                _logger.LogTrace(
                    "No integration events produced for domain event {EventType}",
                    domainEvent.GetType().Name);
                continue;
            }

            // Create envelopes with current context from accessor
            var envelopes = _envelopeFactory.Create(domainEvent, integrationEvents, _contextAccessor.Current);
            
            if (envelopes.Count > 0)
            {
                await _publisher.PublishAsync(envelopes, cancellationToken);
                _logger.LogDebug(
                    "Published {EnvelopeCount} envelopes for domain event {EventType}",
                    envelopes.Count, domainEvent.GetType().Name);
            }
        }
    }

    private async Task ProcessIntegrationEvents(IReadOnlyList<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken)
    {
        foreach (var integrationEvent in integrationEvents)
        {
            // Use a minimal domain event shadow for factory parameter
            var shadowDomainEvent = new DirectIntegrationEventShadow(integrationEvent.GetType().Name);
            
            var envelopes = _envelopeFactory.Create(
                shadowDomainEvent,
                new[] { integrationEvent },
                _contextAccessor.Current);

            if (envelopes.Count > 0)
            {
                await _publisher.PublishAsync(envelopes, cancellationToken);
                _logger.LogDebug(
                    "Published envelope for direct integration event {EventType}",
                    integrationEvent.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Minimal stand-in domain event when processing direct integration events.
    /// </summary>
    private sealed record DirectIntegrationEventShadow(string SourceEventType) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
        public int Version => 1;
        public string Name => SourceEventType;
    }
}