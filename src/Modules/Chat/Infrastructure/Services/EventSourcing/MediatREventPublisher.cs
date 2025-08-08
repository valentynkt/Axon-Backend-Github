using Axon.Shared.Domain;
using BuildingBlocks.Core.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Services.EventSourcing;

/// <summary>
/// Simple event publisher for domain events using MediatR
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task PublishManyAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// MediatR-based event publisher implementation
/// </summary>
public sealed class MediatREventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatREventPublisher> _logger;

    public MediatREventPublisher(IMediator mediator, ILogger<MediatREventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        try
        {
            _logger.LogDebug("Publishing domain event {EventType}", domainEvent.GetType().Name);
            await _mediator.Publish(domainEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish domain event {EventType}", domainEvent.GetType().Name);
            throw;
        }
    }

    public async Task PublishManyAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent, cancellationToken);
        }
    }
}