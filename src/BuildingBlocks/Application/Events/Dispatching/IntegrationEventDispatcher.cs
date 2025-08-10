using BuildingBlocks.Application.Events.Mapping;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Dispatching;

public sealed class IntegrationEventDispatcher : IIntegrationEventDispatcher
{
    private readonly IEventMapper _mapper;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<IntegrationEventDispatcher> _logger;

    public IntegrationEventDispatcher(
        IEventMapper mapper,
        IIntegrationEventPublisher publisher,
        ILogger<IntegrationEventDispatcher> logger)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        if (events is null || events.Count == 0)
        {
            _logger.LogDebug("No domain events to dispatch");
            return;
        }

        var toPublish = new List<IIntegrationEvent>(capacity: events.Count);

        foreach (var domainEvent in events)
        {
            if (domainEvent is IHaveIntegrationEvent have)
                toPublish.AddRange(have.GetIntegrationEvents());

            var mapped = _mapper.MapToIntegrationEvent(domainEvent);
            if (mapped is not null)
                toPublish.Add(mapped);
        }

        if (toPublish.Count == 0)
        {
            _logger.LogTrace("No integration events produced for {Count} domain events", events.Count);
            return;
        }

        await _publisher.PublishAsync(toPublish, cancellationToken);
        _logger.LogDebug("Published {Count} integration events from domain events", toPublish.Count);
    }

    public Task SendAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        => SendAsync(new[] { @event }, cancellationToken);

    public async Task SendAsync(IReadOnlyList<IIntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        if (events is null || events.Count == 0)
        {
            _logger.LogDebug("No integration events to dispatch");
            return;
        }

        await _publisher.PublishAsync(events, cancellationToken);
        _logger.LogDebug("Published {Count} direct integration events", events.Count);
    }

    public Task SendAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
        => SendAsync(new[] { @event }, cancellationToken);
}
