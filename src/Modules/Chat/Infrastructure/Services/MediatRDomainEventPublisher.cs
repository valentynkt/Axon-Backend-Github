using Axon.Modules.Chat.Domain.Services;
using Axon.Shared.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// MediatR-based domain event publisher implementation following SPARC Event Sourcing patterns
/// Implements IDomainEventPublisher interface with enhanced error handling and correlation tracking
/// </summary>
public sealed class MediatRDomainEventPublisher : IDomainEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatRDomainEventPublisher> _logger;

    public MediatRDomainEventPublisher(
        IMediator mediator,
        ILogger<MediatRDomainEventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a single domain event through MediatR notification pipeline
    /// </summary>
    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        try
        {
            _logger.LogDebug("Publishing domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name, domainEvent.EventId);

            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogTrace("Successfully published domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name, domainEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name, domainEvent.EventId);

            throw;
        }
    }

    /// <summary>
    /// Publishes multiple domain events in sequence with comprehensive error handling
    /// Each event is published individually to ensure proper error isolation and reporting
    /// </summary>
    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        var eventsList = domainEvents.ToList();
        if (!eventsList.Any())
        {
            _logger.LogDebug("No domain events to publish");
            return;
        }

        _logger.LogDebug("Publishing {Count} domain events", eventsList.Count);

        var exceptions = new List<Exception>();
        var successCount = 0;

        foreach (var domainEvent in eventsList)
        {
            try
            {
                await PublishAsync(domainEvent, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                _logger.LogError(ex, "Failed to publish domain event {EventType} with ID {EventId} in batch",
                    domainEvent.GetType().Name, domainEvent.EventId);
            }
        }

        if (exceptions.Any())
        {
            _logger.LogError("Failed to publish {FailedCount} out of {TotalCount} domain events",
                exceptions.Count, eventsList.Count);
            
            // Throw aggregate exception with all failures for caller to handle
            throw new AggregateException(
                $"Failed to publish {exceptions.Count} out of {eventsList.Count} domain events",
                exceptions);
        }

        _logger.LogDebug("Successfully published all {Count} domain events", eventsList.Count);
    }
}