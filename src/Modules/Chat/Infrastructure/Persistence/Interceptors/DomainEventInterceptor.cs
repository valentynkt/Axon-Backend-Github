using Axon.Modules.Chat.Infrastructure.Persistence.Entities;
using Axon.Modules.Chat.Infrastructure.Services.EventSourcing;
using Axon.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Domain event interceptor implementing outbox pattern for transactional event persistence
/// Following SPARC Event Sourcing and CQRS architecture patterns
/// Ensures transactional consistency by persisting domain events as outbox messages
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IEventSerializer _eventSerializer;
    private readonly ILogger<DomainEventInterceptor> _logger;

    public DomainEventInterceptor(
        IEventSerializer eventSerializer,
        ILogger<DomainEventInterceptor> logger)
    {
        _eventSerializer = eventSerializer;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts synchronous SaveChanges operations to collect and persist domain events
    /// Implementation follows SPARC Event Sourcing patterns with outbox integration
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ProcessDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts asynchronous SaveChangesAsync operations to collect and persist domain events
    /// Implementation follows SPARC Event Sourcing patterns with outbox integration
    /// </summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ProcessDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Processes domain events synchronously for compatibility with sync SaveChanges
    /// Falls back to async processing where required (event serialization)
    /// </summary>
    private void ProcessDomainEvents(DbContext? context)
    {
        if (context is null)
        {
            _logger.LogWarning("DbContext is null in domain event interceptor");
            return;
        }

        try
        {
            // Collect domain events from aggregate roots before saving
            var domainEvents = CollectDomainEvents(context);

            if (!domainEvents.Any())
            {
                _logger.LogTrace("No domain events found to process");
                return;
            }

            _logger.LogDebug("Processing {Count} domain events synchronously", domainEvents.Count);

            // Process domain events synchronously using Task.Run to avoid blocking
            var outboxMessages = Task.Run(async () => await CreateOutboxMessagesAsync(domainEvents, CancellationToken.None))
                .GetAwaiter()
                .GetResult();

            // Add outbox messages to context for persistence
            foreach (var outboxMessage in outboxMessages)
            {
                context.Set<OutboxMessage>().Add(outboxMessage);
            }

            // Clear domain events from aggregates to prevent reprocessing
            ClearDomainEvents(context);

            _logger.LogDebug("Successfully processed {Count} domain events and created {OutboxCount} outbox messages",
                domainEvents.Count, outboxMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process domain events in synchronous save operation");
            throw new InvalidOperationException("Domain event processing failed", ex);
        }
    }

    /// <summary>
    /// Processes domain events asynchronously for optimal performance
    /// Implementation follows SPARC Event Sourcing patterns with proper error handling
    /// </summary>
    private async Task ProcessDomainEventsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            _logger.LogWarning("DbContext is null in domain event interceptor");
            return;
        }

        try
        {
            // Collect domain events from aggregate roots before saving
            var domainEvents = CollectDomainEvents(context);

            if (!domainEvents.Any())
            {
                _logger.LogTrace("No domain events found to process");
                return;
            }

            _logger.LogDebug("Processing {Count} domain events asynchronously", domainEvents.Count);

            // Create outbox messages for transactional event persistence
            var outboxMessages = await CreateOutboxMessagesAsync(domainEvents, cancellationToken);

            // Add outbox messages to context for persistence
            foreach (var outboxMessage in outboxMessages)
            {
                context.Set<OutboxMessage>().Add(outboxMessage);
            }

            // Clear domain events from aggregates to prevent reprocessing
            ClearDomainEvents(context);

            _logger.LogDebug("Successfully processed {Count} domain events and created {OutboxCount} outbox messages",
                domainEvents.Count, outboxMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process domain events in asynchronous save operation");
            throw new InvalidOperationException("Domain event processing failed", ex);
        }
    }

    /// <summary>
    /// Collects domain events from all aggregate roots tracked by the change tracker
    /// Returns events in chronological order for proper processing sequence
    /// </summary>
    private List<IDomainEvent> CollectDomainEvents(DbContext context)
    {
        var aggregateEntries = context.ChangeTracker.Entries<IAggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .ToList();

        if (!aggregateEntries.Any())
        {
            return new List<IDomainEvent>();
        }

        var allDomainEvents = new List<IDomainEvent>();

        foreach (var entry in aggregateEntries)
        {
            var domainEvents = entry.Entity.DomainEvents.ToList();
            allDomainEvents.AddRange(domainEvents);

            _logger.LogTrace("Collected {Count} domain events from aggregate {AggregateType}",
                domainEvents.Count, entry.Entity.GetType().Name);
        }

        // Sort events by occurrence time to maintain proper ordering
        var sortedEvents = allDomainEvents
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.EventId)
            .ToList();

        _logger.LogDebug("Collected total of {Count} domain events from {AggregateCount} aggregates",
            sortedEvents.Count, aggregateEntries.Count);

        return sortedEvents;
    }

    /// <summary>
    /// Creates outbox messages from domain events for transactional persistence
    /// Uses event serialization for proper payload and metadata handling
    /// </summary>
    private async Task<List<OutboxMessage>> CreateOutboxMessagesAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        var outboxMessages = new List<OutboxMessage>();

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                // Serialize the event payload
                var payload = await _eventSerializer.SerializeAsync(domainEvent, cancellationToken);

                // Build event metadata with correlation and causation information
                var metadata = await _eventSerializer.BuildMetadataAsync(
                    domainEvent,
                    GetCorrelationId(),
                    GetCurrentUserId(),
                    cancellationToken);

                // Create outbox message for transactional persistence
                var outboxMessage = OutboxMessage.Create(
                    type: domainEvent.GetType().AssemblyQualifiedName!,
                    payload: payload,
                    metadata: metadata,
                    occurredAtUtc: domainEvent.OccurredAt);

                outboxMessages.Add(outboxMessage);

                _logger.LogTrace("Created outbox message for domain event {EventType} with ID {EventId}",
                    domainEvent.GetType().Name, domainEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create outbox message for domain event {EventType} with ID {EventId}",
                    domainEvent.GetType().Name, domainEvent.EventId);
                throw new InvalidOperationException($"Failed to create outbox message for event {domainEvent.GetType().Name}", ex);
            }
        }

        _logger.LogDebug("Successfully created {Count} outbox messages", outboxMessages.Count);
        return outboxMessages;
    }

    /// <summary>
    /// Clears domain events from all aggregate roots to prevent reprocessing
    /// Called after successful outbox message creation
    /// </summary>
    private void ClearDomainEvents(DbContext context)
    {
        var aggregateEntries = context.ChangeTracker.Entries<IAggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .ToList();

        foreach (var entry in aggregateEntries)
        {
            var eventCount = entry.Entity.DomainEvents.Count;
            entry.Entity.ClearDomainEvents();

            _logger.LogTrace("Cleared {Count} domain events from aggregate {AggregateType}",
                eventCount, entry.Entity.GetType().Name);
        }

        if (aggregateEntries.Any())
        {
            _logger.LogDebug("Cleared domain events from {Count} aggregates", aggregateEntries.Count);
        }
    }

    /// <summary>
    /// Gets correlation ID from current activity or generates a new one
    /// Supports distributed tracing across service boundaries
    /// </summary>
    private static string GetCorrelationId()
    {
        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Gets current user ID from execution context
    /// Falls back to "SYSTEM" for automated processes
    /// </summary>
    private static string GetCurrentUserId()
    {
        // TODO: Integrate with ICurrentUserService when available in interceptor context
        // For now, use Activity context or default to SYSTEM
        return Activity.Current?.GetBaggageItem("UserId") ?? "SYSTEM";
    }
}