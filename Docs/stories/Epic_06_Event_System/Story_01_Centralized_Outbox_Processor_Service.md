# Story 01: Centralized Outbox Processor Service

## Story Overview

**Story ID**: Epic_06_Story_01  
**Story Name**: Centralized Outbox Processor Service  
**Epic**: Epic 06 - Event System Enhancement & Integration  
**Priority**: P0 - Critical Foundation  
**Estimated Duration**: 8 hours  
**Dependencies**: None (foundation story)

## User Story

**As a system architect**, I want a centralized outbox processor service so that all modules can reliably publish integration events through a unified, scalable mechanism with guaranteed delivery and proper error handling.

## Current State Analysis

### ✅ What Exists Today
- **Chat Module Implementation**: `OutboxMessage` entity with comprehensive metadata
- **Domain Event Interceptor**: Automatic outbox message creation in `DomainEventInterceptor.cs`
- **Event Serialization**: `SystemTextJsonEventSerializer` with versioning support
- **Transaction Safety**: Events only persisted after successful commit

### ❌ What's Missing
- **Centralized Processing**: Each module would need separate outbox processing
- **Background Service**: No `IHostedService` for continuous processing
- **Cross-Module Support**: OutboxMessage only available in Chat module
- **Retry Logic**: No exponential backoff for failed events
- **Monitoring**: No health checks or metrics for outbox processing

## Acceptance Criteria

### Foundation Requirements
- [ ] `OutboxMessage` moved from Chat module to BuildingBlocks as shared entity
- [ ] `IOutboxProcessor` interface defines processing contract
- [ ] `OutboxProcessorService` implements `IHostedService` for background processing
- [ ] All modules can use outbox pattern through shared infrastructure

### Processing Requirements  
- [ ] Batch processing support (configurable batch sizes)
- [ ] Exponential backoff retry logic for failed events
- [ ] Dead letter queue for events exceeding max retry attempts
- [ ] Processing only occurs for unprocessed messages (`ProcessedAtUtc IS NULL`)

### Operational Requirements
- [ ] Comprehensive structured logging with correlation IDs
- [ ] Health checks for outbox processor service status
- [ ] Configuration options for processing intervals and batch sizes
- [ ] Graceful shutdown handling without event loss

### Integration Requirements
- [ ] Compatible with existing `DomainEventInterceptor` workflow
- [ ] Works with current `EventDispatcher` for integration event publishing
- [ ] Maintains all existing Chat module functionality
- [ ] Zero breaking changes to existing domain event processing

## Technical Implementation

### 1. Move OutboxMessage to Shared Location

**Target Location**: `src/BuildingBlocks/Infrastructure/Events/OutboxMessage.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Shared outbox message entity for reliable event publishing across all modules
/// Moved from Chat module to enable cross-module usage
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Fully qualified event type name for deserialization
    /// </summary>
    public string Type { get; init; } = default!;
    
    /// <summary>
    /// Serialized event payload using System.Text.Json
    /// </summary>
    public string Payload { get; init; } = default!;
    
    /// <summary>
    /// Event metadata containing correlation, causation, and context information
    /// </summary>
    public string Metadata { get; init; } = default!;
    
    /// <summary>
    /// Timestamp when the domain event occurred (UTC)
    /// </summary>
    public DateTime OccurredAtUtc { get; init; }
    
    /// <summary>
    /// Timestamp when the message was successfully processed (UTC)
    /// NULL indicates unprocessed message
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }
    
    /// <summary>
    /// Number of processing attempts for retry logic
    /// </summary>
    public int ProcessingAttempts { get; set; }
    
    /// <summary>
    /// Last error message if processing failed
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Next retry attempt timestamp for exponential backoff
    /// </summary>
    public DateTime? NextRetryAtUtc { get; set; }

    /// <summary>
    /// Factory method for creating outbox messages following SPARC patterns
    /// </summary>
    public static OutboxMessage Create(
        string type,
        string payload,
        string metadata,
        DateTime occurredAtUtc) =>
        new()
        {
            Type = type,
            Payload = payload,
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc,
            ProcessingAttempts = 0
        };
    
    /// <summary>
    /// Mark processing attempt with error information
    /// </summary>
    public Result<Unit> MarkAttempt(string? errorMessage = null)
    {
        ProcessingAttempts++;
        LastError = errorMessage;
        
        // Exponential backoff: 2^attempts minutes (1, 2, 4, 8, 16...)
        NextRetryAtUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, ProcessingAttempts));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    /// <summary>
    /// Mark message as successfully processed
    /// </summary>
    public Result<Unit> MarkProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        LastError = null;
        NextRetryAtUtc = null;
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

### 2. Create IOutboxProcessor Interface

**Location**: `src/BuildingBlocks/Core/Abstractions/Events/IOutboxProcessor.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Interface for processing outbox messages with reliable delivery guarantees
/// Implementations should handle retry logic, dead letter queues, and error handling
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Process all pending outbox messages across all modules
    /// </summary>
    Task<Result<int>> ProcessPendingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process pending outbox messages for a specific aggregate/entity
    /// Useful for targeted processing after specific operations
    /// </summary>
    Task<Result<int>> ProcessPendingAsync(string aggregateId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process a specific batch of outbox messages
    /// </summary>
    Task<Result<int>> ProcessBatchAsync(
        IEnumerable<OutboxMessage> messages, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current processing health status
    /// </summary>
    Task<Result<OutboxProcessorHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health information for outbox processor
/// </summary>
public sealed record OutboxProcessorHealth(
    int PendingCount,
    int FailedCount,
    int DeadLetterCount,
    DateTime LastProcessedAtUtc,
    TimeSpan ProcessingLag);
```

### 3. Implement OutboxProcessorService

**Location**: `src/BuildingBlocks/Infrastructure/Events/OutboxProcessorService.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Background service for reliable outbox message processing
/// Implements exponential backoff, dead letter queue, and comprehensive monitoring
/// </summary>
public sealed class OutboxProcessorService : BackgroundService, IOutboxProcessor
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<OutboxProcessorOptions> _options;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly IEventDispatcher _eventDispatcher;

    public OutboxProcessorService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<OutboxProcessorOptions> options,
        ILogger<OutboxProcessorService> logger,
        IEventDispatcher eventDispatcher)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor service starting with interval {Interval}ms", 
            _options.Value.ProcessingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await ProcessPendingAsync(stoppingToken);
                
                if (result.IsFailure)
                {
                    _logger.LogError("Outbox processing cycle failed: {Error}", result.Error.Message);
                }
                else if (result.Value > 0)
                {
                    _logger.LogInformation("Processed {Count} outbox messages successfully", result.Value);
                }

                await Task.Delay(_options.Value.ProcessingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Outbox processor service stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in outbox processor service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Back off on errors
            }
        }
    }

    public async Task<Result<int>> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();
        
        try
        {
            var pendingMessages = await GetPendingMessagesAsync(context, cancellationToken);
            
            if (!pendingMessages.Any())
                return Result<int>.Success(0);

            return await ProcessBatchAsync(pendingMessages, cancellationToken);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("OUTBOX_001", "Failed to process pending outbox messages")
                .WithMetadata("Exception", ex.Message);
                
            _logger.LogError(ex, "Error processing pending outbox messages");
            return Result<int>.Failure(error);
        }
    }
    
    public async Task<Result<int>> ProcessBatchAsync(
        IEnumerable<OutboxMessage> messages, 
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var processedCount = 0;
        
        using var scope = _serviceScopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();
        
        foreach (var message in messageList)
        {
            try
            {
                // Process individual message
                var result = await ProcessSingleMessageAsync(message, cancellationToken);
                
                if (result.IsSuccess)
                {
                    message.MarkProcessed();
                    processedCount++;
                }
                else
                {
                    message.MarkAttempt(result.Error.Message);
                    
                    // Move to dead letter queue if max attempts exceeded
                    if (message.ProcessingAttempts >= _options.Value.MaxRetryAttempts)
                    {
                        await MoveToDeadLetterQueueAsync(message, cancellationToken);
                    }
                }
                
                context.Set<OutboxMessage>().Update(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
                message.MarkAttempt(ex.Message);
                context.Set<OutboxMessage>().Update(message);
            }
        }
        
        await context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(processedCount);
    }

    private async Task<Result<Unit>> ProcessSingleMessageAsync(
        OutboxMessage message, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the event
            var eventType = Type.GetType(message.Type);
            if (eventType == null)
            {
                return Result<Unit>.Failure(
                    Error.Validation("OUTBOX_002", $"Unknown event type: {message.Type}"));
            }

            var @event = JsonSerializer.Deserialize(message.Payload, eventType);
            if (@event == null)
            {
                return Result<Unit>.Failure(
                    Error.Validation("OUTBOX_003", "Failed to deserialize event payload"));
            }

            // Publish through event dispatcher
            if (@event is IIntegrationEvent integrationEvent)
            {
                await _eventDispatcher.DispatchAsync(new[] { integrationEvent }, cancellationToken);
            }

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(
                Error.Failure("OUTBOX_004", "Failed to process outbox message")
                    .WithMetadata("MessageId", message.Id.ToString())
                    .WithMetadata("Exception", ex.Message));
        }
    }
}
```

### 4. Configuration Options

**Location**: `src/BuildingBlocks/Infrastructure/Events/OutboxProcessorOptions.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for the outbox processor service
/// </summary>
public sealed class OutboxProcessorOptions
{
    public const string ConfigurationSection = "OutboxProcessor";
    
    /// <summary>
    /// Interval between processing cycles in milliseconds
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int ProcessingIntervalMs { get; set; } = 5000;
    
    /// <summary>
    /// Maximum number of messages to process in a single batch
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;
    
    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter queue
    /// Default: 5
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;
    
    /// <summary>
    /// Maximum age of pending messages before they're considered stale (hours)
    /// Default: 24 hours
    /// </summary>
    public int MaxPendingAgeHours { get; set; } = 24;
    
    /// <summary>
    /// Enable dead letter queue for failed messages
    /// Default: true
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;
    
    /// <summary>
    /// Enable health checks for the outbox processor
    /// Default: true
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;
}
```

## Tasks Breakdown

### Phase 1: Foundation (4 hours)
- [ ] Move `OutboxMessage` from Chat module to `src/BuildingBlocks/Infrastructure/Events/`
- [ ] Update Chat module to reference shared `OutboxMessage`
- [ ] Create `IOutboxProcessor` interface with Result<T> patterns
- [ ] Create `OutboxProcessorOptions` configuration class
- [ ] Update all existing references and ensure no breaking changes

### Phase 2: Service Implementation (3 hours)  
- [ ] Implement `OutboxProcessorService` with `IHostedService`
- [ ] Add exponential backoff retry logic
- [ ] Implement batch processing capabilities
- [ ] Add comprehensive structured logging
- [ ] Create dead letter queue handling

### Phase 3: Integration & Testing (1 hour)
- [ ] Add DI registration extension methods
- [ ] Create health checks for outbox processor
- [ ] Write unit tests for retry logic and error handling
- [ ] Integration test with existing Chat module workflow
- [ ] Validate zero impact on current domain event processing

## Definition of Done

### Functionality
- [ ] `OutboxMessage` available in all modules through BuildingBlocks
- [ ] Background service processes outbox messages continuously
- [ ] Failed messages retry with exponential backoff
- [ ] Dead letter queue handles max retry exceeded scenarios
- [ ] Batch processing improves performance for high throughput

### Quality
- [ ] All operations return `Result<T>` following Epic_03 patterns
- [ ] Comprehensive logging with structured data and correlation IDs
- [ ] Health checks report outbox processor status
- [ ] Unit tests cover retry logic, error handling, and batch processing
- [ ] Integration tests validate end-to-end workflow

### Operations
- [ ] Configuration options allow tuning for different environments
- [ ] Graceful shutdown prevents message loss during deployment
- [ ] Monitoring metrics available for operational dashboards
- [ ] Zero breaking changes to existing Chat module functionality

## Migration Strategy

### Step 1: Parallel Implementation
1. Create shared `OutboxMessage` in BuildingBlocks
2. Implement `OutboxProcessorService` alongside existing Chat interceptor
3. Validate both systems work in parallel

### Step 2: Migration
1. Update Chat module to use shared `OutboxMessage`
2. Enable `OutboxProcessorService` in configuration
3. Monitor both old and new processing paths

### Step 3: Cleanup
1. Remove Chat-specific outbox processing if desired
2. Standardize all modules on shared infrastructure
3. Update documentation and architecture diagrams

## Dependencies & Prerequisites

### Technical Dependencies
- ✅ Epic_03 Result<T> pattern for error handling
- ✅ Epic_05 TransactionBehavior for domain event collection
- ✅ Existing EventDispatcher for integration event publishing
- ✅ IServiceScopeFactory and IHostedService (.NET infrastructure)

### Infrastructure Dependencies
- Database table for OutboxMessage (already exists in Chat module)
- Configuration section for OutboxProcessorOptions
- Health check endpoints for monitoring

### Team Dependencies  
- No external team dependencies
- Self-contained within event system components
- Compatible with existing domain event workflows

## Success Criteria

### Technical Success
- All modules can reliably publish integration events through outbox pattern
- Background service processes events with < 30 second average latency
- Retry logic handles transient failures without manual intervention
- Dead letter queue captures persistent failures for manual review

### Operational Success
- Health checks indicate outbox processor status in monitoring dashboards
- Processing metrics available for capacity planning and optimization
- Zero message loss during normal operations and graceful shutdowns
- Configuration allows tuning for different deployment environments

This story provides the foundation for all subsequent Epic 06 stories by establishing the centralized, reliable outbox processing infrastructure.