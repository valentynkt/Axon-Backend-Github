# Story 02: Enhanced Integration Event Publisher

## Story Overview

**Story ID**: Epic_06_Story_02  
**Story Name**: Enhanced Integration Event Publisher  
**Epic**: Epic 06 - Event System Enhancement & Integration  
**Priority**: P1 - High  
**Estimated Duration**: 8 hours  
**Dependencies**: Epic_06_Story_01 (Centralized Outbox Processor)

## User Story

**As a developer**, I want enhanced integration event publishing so that external systems receive events with guaranteed delivery, proper error handling, and dead letter queue support for operational resilience.

## Current State Analysis

### ✅ What Exists Today
- **EventDispatcher**: Basic integration event publishing in `src/BuildingBlocks/Application/Events/EventDispatcher.cs`
- **IIntegrationEvent**: Interface and base classes for integration events
- **Event Mapping**: `CompositeEventMapper` for domain→integration event transformation
- **Outbox Foundation**: Story_01 provides reliable outbox processing infrastructure

### ❌ What's Missing
- **External System Integration**: No actual message broker publishing
- **Dead Letter Queue**: Failed events have no recovery mechanism
- **Idempotency**: No duplicate event prevention
- **Delivery Confirmation**: No acknowledgment tracking
- **External Configuration**: Hard-coded to use outbox pattern only

### 🔍 Current EventDispatcher Analysis
```csharp
// Current implementation focuses on outbox pattern
// but lacks external system publishing capabilities
public sealed class EventDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IEventMapper eventMapper,
    ILogger<EventDispatcher> logger,
    IInternalMessagePublisher internalMessagePublisher)
    : IEventDispatcher
```

## Acceptance Criteria

### Core Integration Requirements
- [ ] Publish integration events to external message brokers (Azure Service Bus/RabbitMQ)
- [ ] Support multiple message broker providers through abstraction layer
- [ ] Maintain existing outbox pattern as fallback/primary mechanism
- [ ] Configuration-driven routing (which events go to which external systems)

### Reliability Requirements
- [ ] Dead letter queue for events that fail external publishing after retries
- [ ] Idempotency checks prevent duplicate event publishing to external systems
- [ ] Delivery confirmation tracking with correlation IDs
- [ ] Circuit breaker pattern for external system failures

### Operational Requirements
- [ ] Comprehensive logging for external publishing attempts and failures
- [ ] Metrics collection for delivery rates, latency, and error rates
- [ ] Health checks for external message broker connectivity
- [ ] Configuration hot-reload for routing and broker settings

### Backward Compatibility
- [ ] Zero breaking changes to existing `IEventDispatcher` interface
- [ ] Existing outbox workflow continues to work unchanged
- [ ] All current integration event handlers remain functional
- [ ] Domain event processing flow unchanged

## Technical Implementation

### 1. External Message Broker Abstraction

**Location**: `src/BuildingBlocks/Core/Abstractions/Messaging/IExternalEventPublisher.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Abstractions.Messaging;

/// <summary>
/// Abstraction for publishing integration events to external message brokers
/// Supports multiple implementations (Azure Service Bus, RabbitMQ, etc.)
/// </summary>
public interface IExternalEventPublisher
{
    /// <summary>
    /// Publish integration event to external message broker
    /// </summary>
    Task<Result<EventPublishResult>> PublishAsync(
        IIntegrationEvent integrationEvent,
        PublishOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publish batch of integration events for performance
    /// </summary>
    Task<Result<BatchPublishResult>> PublishBatchAsync(
        IEnumerable<IIntegrationEvent> integrationEvents,
        PublishOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check health of external message broker connection
    /// </summary>
    Task<Result<PublisherHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of individual event publishing
/// </summary>
public sealed record EventPublishResult(
    string MessageId,
    DateTime PublishedAtUtc,
    string Destination,
    TimeSpan Latency);

/// <summary>
/// Result of batch event publishing
/// </summary>
public sealed record BatchPublishResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<EventPublishResult> SuccessfulEvents,
    IReadOnlyList<PublishFailure> FailedEvents,
    TimeSpan TotalLatency);

/// <summary>
/// Details of failed event publishing
/// </summary>
public sealed record PublishFailure(
    IIntegrationEvent Event,
    Error Error,
    int RetryAttempt);

/// <summary>
/// Health status of external publisher
/// </summary>
public sealed record PublisherHealth(
    bool IsHealthy,
    string Provider,
    TimeSpan LastCheckLatency,
    string? ErrorMessage);

/// <summary>
/// Options for publishing integration events
/// </summary>
public sealed record PublishOptions
{
    public string? Destination { get; init; }
    public Dictionary<string, object>? Headers { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public int MaxRetryAttempts { get; init; } = 3;
    public bool RequireDeliveryConfirmation { get; init; } = false;
    
    public static PublishOptions Default => new();
}
```

### 2. Azure Service Bus Implementation

**Location**: `src/BuildingBlocks/Infrastructure/Messaging/AzureServiceBus/AzureServiceBusPublisher.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Messaging.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of external event publishing
/// Provides reliable integration event delivery with built-in retry and DLQ support
/// </summary>
public sealed class AzureServiceBusPublisher : IExternalEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly IOptions<AzureServiceBusOptions> _options;
    private readonly ILogger<AzureServiceBusPublisher> _logger;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public AzureServiceBusPublisher(
        ServiceBusClient client,
        IOptions<AzureServiceBusOptions> options,
        ILogger<AzureServiceBusPublisher> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<EventPublishResult>> PublishAsync(
        IIntegrationEvent integrationEvent,
        PublishOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var destination = options.Destination ?? GetDefaultDestination(integrationEvent);
            var sender = await GetSenderAsync(destination);
            
            var message = CreateServiceBusMessage(integrationEvent, options);
            var stopwatch = Stopwatch.StartNew();
            
            await sender.SendMessageAsync(message, cancellationToken);
            stopwatch.Stop();
            
            var result = new EventPublishResult(
                MessageId: message.MessageId,
                PublishedAtUtc: DateTime.UtcNow,
                Destination: destination,
                Latency: stopwatch.Elapsed);

            _logger.LogInformation(
                "Published integration event {EventType} to {Destination} in {Latency}ms",
                integrationEvent.GetType().Name,
                destination,
                stopwatch.ElapsedMilliseconds);

            return Result<EventPublishResult>.Success(result);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            var error = Error.Failure("SERVICEBUS_001", "Service Bus timeout during event publishing")
                .WithMetadata("EventType", integrationEvent.GetType().Name)
                .WithMetadata("Reason", ex.Reason.ToString());
                
            _logger.LogError(ex, "Service Bus timeout publishing {EventType}", integrationEvent.GetType().Name);
            return Result<EventPublishResult>.Failure(error);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("SERVICEBUS_002", "Failed to publish integration event to Service Bus")
                .WithMetadata("EventType", integrationEvent.GetType().Name)
                .WithMetadata("Exception", ex.Message);
                
            _logger.LogError(ex, "Error publishing integration event {EventType}", integrationEvent.GetType().Name);
            return Result<EventPublishResult>.Failure(error);
        }
    }

    public async Task<Result<BatchPublishResult>> PublishBatchAsync(
        IEnumerable<IIntegrationEvent> integrationEvents,
        PublishOptions options,
        CancellationToken cancellationToken = default)
    {
        var events = integrationEvents.ToList();
        var successfulEvents = new List<EventPublishResult>();
        var failedEvents = new List<PublishFailure>();
        
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var integrationEvent in events)
        {
            var result = await PublishAsync(integrationEvent, options, cancellationToken);
            
            if (result.IsSuccess)
            {
                successfulEvents.Add(result.Value);
            }
            else
            {
                failedEvents.Add(new PublishFailure(integrationEvent, result.Error, 0));
            }
        }
        
        stopwatch.Stop();
        
        var batchResult = new BatchPublishResult(
            SuccessCount: successfulEvents.Count,
            FailureCount: failedEvents.Count,
            SuccessfulEvents: successfulEvents,
            FailedEvents: failedEvents,
            TotalLatency: stopwatch.Elapsed);

        _logger.LogInformation(
            "Batch published {Success}/{Total} integration events in {Latency}ms",
            successfulEvents.Count,
            events.Count,
            stopwatch.ElapsedMilliseconds);

        return Result<BatchPublishResult>.Success(batchResult);
    }

    public async Task<Result<PublisherHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simple health check - create a sender to verify connection
            var healthCheckTopic = _options.Value.HealthCheckTopic ?? "health-check";
            var sender = await GetSenderAsync(healthCheckTopic);
            
            stopwatch.Stop();
            
            var health = new PublisherHealth(
                IsHealthy: true,
                Provider: "Azure Service Bus",
                LastCheckLatency: stopwatch.Elapsed,
                ErrorMessage: null);

            return Result<PublisherHealth>.Success(health);
        }
        catch (Exception ex)
        {
            var health = new PublisherHealth(
                IsHealthy: false,
                Provider: "Azure Service Bus",
                LastCheckLatency: TimeSpan.Zero,
                ErrorMessage: ex.Message);

            return Result<PublisherHealth>.Success(health);
        }
    }

    private async Task<ServiceBusSender> GetSenderAsync(string destination)
    {
        return _senders.GetOrAdd(destination, dest => _client.CreateSender(dest));
    }

    private ServiceBusMessage CreateServiceBusMessage(IIntegrationEvent integrationEvent, PublishOptions options)
    {
        var eventType = integrationEvent.GetType();
        var payload = JsonSerializer.Serialize(integrationEvent, eventType);
        
        var message = new ServiceBusMessage(payload)
        {
            MessageId = integrationEvent.EventId.ToString(),
            Subject = eventType.Name,
            ContentType = "application/json",
            CorrelationId = integrationEvent.EventId.ToString(),
            TimeToLive = options.TimeToLive ?? TimeSpan.FromDays(7)
        };

        // Add custom headers
        if (options.Headers != null)
        {
            foreach (var header in options.Headers)
            {
                message.ApplicationProperties[header.Key] = header.Value;
            }
        }

        // Add event metadata
        message.ApplicationProperties["EventType"] = eventType.FullName;
        message.ApplicationProperties["EventVersion"] = integrationEvent.Version.ToString();
        message.ApplicationProperties["OccurredAt"] = integrationEvent.OccurredAt.ToString("O");

        return message;
    }

    private string GetDefaultDestination(IIntegrationEvent integrationEvent)
    {
        var eventType = integrationEvent.GetType().Name;
        return _options.Value.DefaultTopicPrefix + eventType.ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        
        await _client.DisposeAsync();
    }
}
```

### 3. Enhanced EventDispatcher

**Location**: Update existing `src/BuildingBlocks/Application/Events/EventDispatcher.cs`

```csharp
namespace Axon.BuildingBlocks.Application.Events;

/// <summary>
/// Enhanced event dispatcher with external system publishing capabilities
/// Maintains backward compatibility while adding external integration event publishing
/// </summary>
public sealed class EventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEventMapper _eventMapper;
    private readonly ILogger<EventDispatcher> _logger;
    private readonly IInternalMessagePublisher _internalMessagePublisher;
    private readonly IExternalEventPublisher? _externalEventPublisher; // Optional dependency
    private readonly IOptions<EventPublishingOptions> _options;

    public EventDispatcher(
        IServiceScopeFactory serviceScopeFactory,
        IEventMapper eventMapper,
        ILogger<EventDispatcher> logger,
        IInternalMessagePublisher internalMessagePublisher,
        IOptions<EventPublishingOptions> options,
        IExternalEventPublisher? externalEventPublisher = null) // Optional for backward compatibility
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _eventMapper = eventMapper ?? throw new ArgumentNullException(nameof(eventMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _internalMessagePublisher = internalMessagePublisher ?? throw new ArgumentNullException(nameof(internalMessagePublisher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _externalEventPublisher = externalEventPublisher; // Optional
    }

    public async Task DispatchAsync(object events, CancellationToken cancellationToken = default)
    {
        // ... existing implementation for domain events and outbox pattern ...

        // Enhanced integration event publishing
        if (events is IReadOnlyList<IIntegrationEvent> integrationEvents)
        {
            await PublishIntegrationEventsAsync(integrationEvents, cancellationToken);
        }
    }

    private async Task PublishIntegrationEventsAsync(
        IReadOnlyList<IIntegrationEvent> integrationEvents,
        CancellationToken cancellationToken)
    {
        // Always store in outbox first (Story_01 foundation)
        await StoreInOutboxAsync(integrationEvents, cancellationToken);

        // Additionally publish to external systems if configured
        if (_externalEventPublisher != null && _options.Value.EnableExternalPublishing)
        {
            await PublishToExternalSystemsAsync(integrationEvents, cancellationToken);
        }
    }

    private async Task PublishToExternalSystemsAsync(
        IReadOnlyList<IIntegrationEvent> integrationEvents,
        CancellationToken cancellationToken)
    {
        var eventsToPublish = FilterEventsForExternalPublishing(integrationEvents);
        
        if (!eventsToPublish.Any())
            return;

        try
        {
            var options = new PublishOptions
            {
                MaxRetryAttempts = _options.Value.ExternalPublishingRetryAttempts,
                RequireDeliveryConfirmation = _options.Value.RequireDeliveryConfirmation
            };

            var result = await _externalEventPublisher.PublishBatchAsync(eventsToPublish, options, cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Published {Success}/{Total} integration events to external systems",
                    result.Value.SuccessCount,
                    eventsToPublish.Count);
                    
                // Handle any failures
                if (result.Value.FailedEvents.Any())
                {
                    await HandleExternalPublishingFailuresAsync(result.Value.FailedEvents, cancellationToken);
                }
            }
            else
            {
                _logger.LogError("Failed to publish integration events to external systems: {Error}", 
                    result.Error.Message);
                    
                // All events failed - handle accordingly
                await HandleExternalPublishingFailuresAsync(
                    eventsToPublish.Select(e => new PublishFailure(e, result.Error, 0)).ToList(),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing integration events to external systems");
        }
    }

    private async Task HandleExternalPublishingFailuresAsync(
        IReadOnlyList<PublishFailure> failures,
        CancellationToken cancellationToken)
    {
        // For now, log failures - Dead Letter Queue handling will be in a separate story
        foreach (var failure in failures)
        {
            _logger.LogError(
                "Failed to publish integration event {EventType} to external system: {Error}",
                failure.Event.GetType().Name,
                failure.Error.Message);
        }
        
        // TODO: Implement dead letter queue handling in follow-up enhancement
    }

    private IReadOnlyList<IIntegrationEvent> FilterEventsForExternalPublishing(
        IReadOnlyList<IIntegrationEvent> integrationEvents)
    {
        // Apply filtering rules based on configuration
        return integrationEvents
            .Where(e => ShouldPublishToExternalSystems(e))
            .ToList();
    }

    private bool ShouldPublishToExternalSystems(IIntegrationEvent integrationEvent)
    {
        var eventType = integrationEvent.GetType().Name;
        
        // Check if this event type is configured for external publishing
        if (_options.Value.ExternalEventTypes.Any() &&
            !_options.Value.ExternalEventTypes.Contains(eventType))
        {
            return false;
        }

        // Check if this event type is explicitly excluded
        if (_options.Value.ExcludedEventTypes.Contains(eventType))
        {
            return false;
        }

        return true;
    }
}
```

### 4. Configuration Options

**Location**: `src/BuildingBlocks/Infrastructure/Events/EventPublishingOptions.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for enhanced integration event publishing
/// </summary>
public sealed class EventPublishingOptions
{
    public const string ConfigurationSection = "EventPublishing";
    
    /// <summary>
    /// Enable external system publishing in addition to outbox pattern
    /// Default: false (safe default - only outbox pattern)
    /// </summary>
    public bool EnableExternalPublishing { get; set; } = false;
    
    /// <summary>
    /// Event types to publish to external systems (empty = all types)
    /// </summary>
    public List<string> ExternalEventTypes { get; set; } = new();
    
    /// <summary>
    /// Event types to exclude from external publishing
    /// </summary>
    public List<string> ExcludedEventTypes { get; set; } = new();
    
    /// <summary>
    /// Maximum retry attempts for external publishing failures
    /// Default: 3
    /// </summary>
    public int ExternalPublishingRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Require delivery confirmation from external message brokers
    /// Default: false
    /// </summary>
    public bool RequireDeliveryConfirmation { get; set; } = false;
    
    /// <summary>
    /// Circuit breaker settings for external publishing
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// Default: 5
    /// </summary>
    public int FailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Duration to keep circuit open before attempting reset
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan OpenCircuitDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Azure Service Bus specific configuration
/// </summary>
public sealed class AzureServiceBusOptions
{
    public const string ConfigurationSection = "AzureServiceBus";
    
    /// <summary>
    /// Service Bus connection string
    /// </summary>
    public string ConnectionString { get; set; } = default!;
    
    /// <summary>
    /// Default prefix for topic names
    /// Default: "integration-events-"
    /// </summary>
    public string DefaultTopicPrefix { get; set; } = "integration-events-";
    
    /// <summary>
    /// Topic used for health checks
    /// Default: "health-check"
    /// </summary>
    public string? HealthCheckTopic { get; set; } = "health-check";
    
    /// <summary>
    /// Maximum message size in bytes
    /// Default: 256KB
    /// </summary>
    public int MaxMessageSizeBytes { get; set; } = 256 * 1024;
}
```

## Tasks Breakdown

### Phase 1: External Publisher Abstraction (3 hours)
- [ ] Create `IExternalEventPublisher` interface with Result<T> patterns
- [ ] Define supporting types (`EventPublishResult`, `PublishOptions`, etc.)
- [ ] Create configuration options for external publishing
- [ ] Design idempotency and delivery confirmation mechanisms

### Phase 2: Azure Service Bus Implementation (3 hours)
- [ ] Implement `AzureServiceBusPublisher` with comprehensive error handling
- [ ] Add connection pooling and sender caching for performance
- [ ] Implement health checks and connectivity monitoring
- [ ] Add structured logging and correlation ID support

### Phase 3: EventDispatcher Enhancement (2 hours)
- [ ] Update existing `EventDispatcher` to support external publishing
- [ ] Maintain 100% backward compatibility with existing functionality
- [ ] Add configuration-driven event filtering and routing
- [ ] Implement graceful fallback when external systems unavailable

## Definition of Done

### Functionality
- [ ] Integration events published to external message brokers (Azure Service Bus)
- [ ] Outbox pattern continues to work as primary reliability mechanism
- [ ] Configuration controls which events are published externally
- [ ] Health checks validate external message broker connectivity

### Quality  
- [ ] All operations return `Result<T>` following Epic_03 error patterns
- [ ] Comprehensive logging with correlation IDs and structured data
- [ ] Circuit breaker pattern prevents cascading failures
- [ ] Unit tests cover external publishing logic and error scenarios
- [ ] Integration tests validate end-to-end external publishing

### Operations
- [ ] Zero breaking changes to existing `IEventDispatcher` interface
- [ ] External publishing is opt-in via configuration (safe default)
- [ ] Health checks integrated with existing monitoring infrastructure
- [ ] Performance metrics available for external publishing operations

## Success Criteria

### Technical Success
- Integration events delivered to external systems with < 5 second latency p95
- Circuit breaker prevents system degradation during external system outages
- Health checks accurately reflect external message broker connectivity
- Zero impact on existing domain event processing performance

### Operational Success  
- Configuration allows granular control over external event publishing
- Monitoring dashboards show external publishing success rates and latency
- Failed external publishing attempts are logged with full context for debugging
- System remains fully functional when external systems are unavailable

This story enhances the event system with external publishing capabilities while maintaining the reliability foundation established in Story_01.