# Story 04: Event Replay & Recovery Service

## Story Overview

**Story ID**: Epic_06_Story_04  
**Story Name**: Event Replay & Recovery Service  
**Epic**: Epic 06 - Event System Enhancement & Integration  
**Priority**: P2 - Medium  
**Estimated Duration**: 6 hours  
**Dependencies**: Epic_06_Story_01 (Outbox Processor), Epic_06_Story_03 (Schema Registry)

## User Story

**As a developer/operator**, I want event replay capabilities so that I can debug production issues, recover from failures, and test system behavior by replaying historical events with full traceability and safety controls.

## Current State Analysis

### ✅ What Exists Today
- **Event Storage**: `OutboxMessage` stores all events with comprehensive metadata
- **Event Serialization**: `SystemTextJsonEventSerializer` handles serialization/deserialization
- **Schema Migration**: Story_03 provides schema evolution capabilities
- **Event Metadata**: Complete event context including correlation IDs, timestamps, and payload

### ❌ What's Missing
- **Replay Service**: No functionality to replay historical events
- **Event Filtering**: No capability to filter events by time, aggregate, or type
- **Replay Validation**: No safety mechanisms to prevent duplicate processing
- **Replay Progress**: No tracking or monitoring of replay operations
- **Recovery Tools**: No operational tools for event system recovery

### 🔍 Available Event Data Analysis
```sql
-- OutboxMessage contains rich event history
SELECT 
    Type,           -- Event type for filtering
    Payload,        -- Event data for replay
    Metadata,       -- Correlation and context information
    OccurredAtUtc,  -- Event timing
    ProcessedAtUtc  -- Processing status
FROM OutboxMessages
WHERE ProcessedAtUtc IS NOT NULL; -- Successfully processed events
```

## Acceptance Criteria

### Replay Functionality Requirements
- [ ] `IEventReplayService` provides event replay by time range, aggregate ID, and event type
- [ ] Support for selective event replay (specific events or event patterns)
- [ ] Batch replay operations for performance with configurable batch sizes
- [ ] Dry-run capability to validate replay operations without actual execution

### Safety & Validation Requirements
- [ ] Replay validation prevents duplicate event processing during replay
- [ ] Event deduplication using event IDs and correlation tracking
- [ ] Rollback capability for failed replay operations
- [ ] Replay isolation ensures replayed events don't interfere with live processing

### Progress & Monitoring Requirements
- [ ] Replay operation tracking with progress reporting and ETAs
- [ ] Comprehensive logging of replay operations with correlation IDs
- [ ] Replay metrics and performance monitoring
- [ ] Health checks for replay service status

### Recovery & Operations Requirements
- [ ] Recovery scenarios for failed outbox processing
- [ ] Event gap detection and automatic replay suggestions
- [ ] Replay operation history and auditing
- [ ] Configuration-driven replay policies and access controls

## Technical Implementation

### 1. Event Replay Service Interface

**Location**: `src/BuildingBlocks/Core/Abstractions/Events/IEventReplayService.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Service for replaying historical events for debugging, testing, and recovery scenarios
/// Provides safe, trackable event replay with validation and progress monitoring
/// </summary>
public interface IEventReplayService
{
    /// <summary>
    /// Start event replay operation with specified criteria
    /// </summary>
    Task<Result<ReplayOperation>> StartReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get status and progress of ongoing replay operation
    /// </summary>
    Task<Result<ReplayStatus>> GetReplayStatusAsync(
        Guid replayOperationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop ongoing replay operation gracefully
    /// </summary>
    Task<Result<Unit>> StopReplayAsync(
        Guid replayOperationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get replay operation history and statistics
    /// </summary>
    Task<Result<IReadOnlyList<ReplayOperation>>> GetReplayHistoryAsync(
        ReplayHistoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate replay request without executing (dry run)
    /// </summary>
    Task<Result<ReplayValidationResult>> ValidateReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get service health status
    /// </summary>
    Task<Result<ReplayServiceHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request parameters for event replay operations
/// </summary>
public sealed record ReplayRequest
{
    public required string OperationName { get; init; }
    public string? Description { get; init; }
    
    // Time-based filtering
    public DateTime? StartTimeUtc { get; init; }
    public DateTime? EndTimeUtc { get; init; }
    
    // Event-based filtering
    public List<string> EventTypes { get; init; } = new();
    public List<string> AggregateIds { get; init; } = new();
    public List<Guid> SpecificEventIds { get; init; } = new();
    
    // Execution options
    public bool DryRun { get; init; } = false;
    public int BatchSize { get; init; } = 100;
    public TimeSpan DelayBetweenBatches { get; init; } = TimeSpan.FromMilliseconds(100);
    public bool PreventDuplicates { get; init; } = true;
    public bool IgnoreProcessedEvents { get; init; } = true;
    
    // Replay behavior
    public ReplayMode Mode { get; init; } = ReplayMode.Chronological;
    public bool MaintainOriginalTimestamps { get; init; } = false;
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Replay execution modes
/// </summary>
public enum ReplayMode
{
    Chronological,      // Replay events in original time order
    TypeGrouped,        // Group by event type, then chronological
    AggregateGrouped,   // Group by aggregate, then chronological
    Parallel            // Replay events in parallel where safe
}

/// <summary>
/// Information about a replay operation
/// </summary>
public sealed record ReplayOperation
{
    public Guid Id { get; init; }
    public string OperationName { get; init; } = default!;
    public string? Description { get; init; }
    public ReplayRequest Request { get; init; } = default!;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public ReplayOperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Status of replay operation
/// </summary>
public enum ReplayOperationStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Validating
}

/// <summary>
/// Current status and progress of replay operation
/// </summary>
public sealed record ReplayStatus
{
    public Guid OperationId { get; init; }
    public ReplayOperationStatus Status { get; init; }
    public int TotalEventsFound { get; init; }
    public int EventsProcessed { get; init; }
    public int EventsSkipped { get; init; }
    public int EventsFaileds { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public double ProgressPercentage => TotalEventsFound > 0 ? (double)EventsProcessed / TotalEventsFound * 100 : 0;
    public string? CurrentBatchInfo { get; init; }
    public DateTime LastUpdateUtc { get; init; }
}

/// <summary>
/// Result of replay validation (dry run)
/// </summary>
public sealed record ReplayValidationResult
{
    public bool IsValid { get; init; }
    public int EventsFound { get; init; }
    public int EstimatedDuplicates { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public IReadOnlyList<ReplayValidationIssue> Issues { get; init; } = Array.Empty<ReplayValidationIssue>();
    public IReadOnlyList<string> EventTypesSummary { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AggregateIdsSummary { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Issues found during replay validation
/// </summary>
public sealed record ReplayValidationIssue
{
    public ReplayIssueType Type { get; init; }
    public string Description { get; init; } = default!;
    public string? Suggestion { get; init; }
    public string? EventId { get; init; }
    public string? EventType { get; init; }
}

public enum ReplayIssueType
{
    EventNotFound,
    SchemaIncompatible,
    DuplicateEvent,
    ProcessingOrderIssue,
    PermissionDenied
}

/// <summary>
/// Request for replay operation history
/// </summary>
public sealed record ReplayHistoryRequest
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public ReplayOperationStatus? Status { get; init; }
    public string? OperationNameFilter { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 50;
}

/// <summary>
/// Health status of replay service
/// </summary>
public sealed record ReplayServiceHealth
{
    public bool IsHealthy { get; init; }
    public int ActiveReplayOperations { get; init; }
    public int TotalReplayOperationsToday { get; init; }
    public DateTime LastSuccessfulReplayUtc { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? Metrics { get; init; }
}
```

### 2. Event Replay Service Implementation

**Location**: `src/BuildingBlocks/Infrastructure/Events/EventReplayService.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Implementation of event replay service with comprehensive safety and monitoring
/// </summary>
public sealed class EventReplayService : IEventReplayService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<EventReplayOptions> _options;
    private readonly ILogger<EventReplayService> _logger;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ConcurrentDictionary<Guid, ReplayOperationState> _activeReplays = new();

    public EventReplayService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<EventReplayOptions> options,
        ILogger<EventReplayService> logger,
        IEventDispatcher eventDispatcher)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<Result<ReplayOperation>> StartReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            var validationResult = await ValidateReplayAsync(request, cancellationToken);
            if (validationResult.IsFailure)
                return Result<ReplayOperation>.Failure(validationResult.Error);

            if (!validationResult.Value.IsValid)
            {
                return Result<ReplayOperation>.Failure(
                    Error.Validation("REPLAY_001", "Replay validation failed")
                        .WithMetadata("Issues", string.Join(", ", validationResult.Value.Issues.Select(i => i.Description))));
            }

            // Create replay operation
            var operation = new ReplayOperation
            {
                Id = Guid.NewGuid(),
                OperationName = request.OperationName,
                Description = request.Description,
                Request = request,
                StartedAtUtc = DateTime.UtcNow,
                Status = ReplayOperationStatus.Pending
            };

            // Store operation
            await StoreReplayOperationAsync(operation, cancellationToken);

            // Start background processing if not dry run
            if (!request.DryRun)
            {
                _ = Task.Run(async () => await ExecuteReplayOperationAsync(operation, cancellationToken));
            }

            _logger.LogInformation(
                "Started replay operation {OperationId} '{OperationName}' with {EventCount} events",
                operation.Id, operation.OperationName, validationResult.Value.EventsFound);

            return Result<ReplayOperation>.Success(operation);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("REPLAY_002", "Failed to start replay operation")
                .WithMetadata("OperationName", request.OperationName)
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error starting replay operation {OperationName}", request.OperationName);
            return Result<ReplayOperation>.Failure(error);
        }
    }

    public async Task<Result<ReplayValidationResult>> ValidateReplayAsync(
        ReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

            // Build query for events matching criteria
            var query = BuildEventQuery(context, request);
            
            // Count total events
            var totalEvents = await query.CountAsync(cancellationToken);
            
            if (totalEvents == 0)
            {
                return Result<ReplayValidationResult>.Success(new ReplayValidationResult
                {
                    IsValid = false,
                    EventsFound = 0,
                    Issues = new[]
                    {
                        new ReplayValidationIssue
                        {
                            Type = ReplayIssueType.EventNotFound,
                            Description = "No events found matching the specified criteria",
                            Suggestion = "Check the time range and event filters"
                        }
                    }
                });
            }

            // Sample events for analysis
            var sampleSize = Math.Min(totalEvents, 1000);
            var sampleEvents = await query
                .Take(sampleSize)
                .ToListAsync(cancellationToken);

            // Analyze sample for validation issues
            var issues = new List<ReplayValidationIssue>();
            var duplicates = 0;

            if (request.PreventDuplicates)
            {
                duplicates = await EstimateDuplicatesAsync(context, sampleEvents, cancellationToken);
            }

            // Check schema compatibility
            await ValidateSchemaCompatibilityAsync(sampleEvents, issues, cancellationToken);

            // Estimate duration
            var estimatedDuration = EstimateReplayDuration(totalEvents, request.BatchSize, request.DelayBetweenBatches);

            // Generate summaries
            var eventTypesSummary = sampleEvents
                .GroupBy(e => e.Type)
                .Select(g => $"{g.Key}: {g.Count()}")
                .Take(10)
                .ToList();

            var result = new ReplayValidationResult
            {
                IsValid = !issues.Any(i => i.Type == ReplayIssueType.SchemaIncompatible),
                EventsFound = totalEvents,
                EstimatedDuplicates = duplicates,
                EstimatedDuration = estimatedDuration,
                Issues = issues,
                EventTypesSummary = eventTypesSummary
            };

            return Result<ReplayValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("REPLAY_003", "Replay validation failed")
                .WithMetadata("OperationName", request.OperationName)
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error validating replay operation {OperationName}", request.OperationName);
            return Result<ReplayValidationResult>.Failure(error);
        }
    }

    private async Task ExecuteReplayOperationAsync(ReplayOperation operation, CancellationToken cancellationToken)
    {
        var operationState = new ReplayOperationState
        {
            Operation = operation,
            Status = ReplayOperationStatus.Running,
            StartTime = DateTime.UtcNow
        };

        _activeReplays.TryAdd(operation.Id, operationState);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

            // Get events to replay
            var query = BuildEventQuery(context, operation.Request);
            var totalEvents = await query.CountAsync(cancellationToken);
            
            operationState.TotalEvents = totalEvents;

            _logger.LogInformation(
                "Starting replay execution for operation {OperationId} with {TotalEvents} events",
                operation.Id, totalEvents);

            // Process events in batches
            var processedCount = 0;
            var batchNumber = 0;

            await foreach (var batch in GetEventBatchesAsync(query, operation.Request.BatchSize, cancellationToken))
            {
                batchNumber++;
                operationState.CurrentBatch = batchNumber;
                operationState.CurrentBatchInfo = $"Batch {batchNumber} ({batch.Count} events)";

                try
                {
                    var batchResult = await ProcessEventBatchAsync(batch, operation.Request, cancellationToken);
                    
                    operationState.EventsProcessed += batchResult.SuccessCount;
                    operationState.EventsSkipped += batchResult.SkippedCount;
                    operationState.EventsFailed += batchResult.FailedCount;
                    
                    processedCount += batch.Count;

                    _logger.LogDebug(
                        "Processed batch {BatchNumber} for replay {OperationId}: {Success} success, {Skipped} skipped, {Failed} failed",
                        batchNumber, operation.Id, batchResult.SuccessCount, batchResult.SkippedCount, batchResult.FailedCount);

                    // Delay between batches if configured
                    if (operation.Request.DelayBetweenBatches > TimeSpan.Zero)
                    {
                        await Task.Delay(operation.Request.DelayBetweenBatches, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Replay operation {OperationId} cancelled during batch {BatchNumber}", 
                        operation.Id, batchNumber);
                    operationState.Status = ReplayOperationStatus.Cancelled;
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch {BatchNumber} in replay operation {OperationId}", 
                        batchNumber, operation.Id);
                    operationState.EventsFailed += batch.Count;
                }
            }

            // Mark operation as completed
            operationState.Status = ReplayOperationStatus.Completed;
            operationState.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Completed replay operation {OperationId}: {Processed}/{Total} events processed, {Skipped} skipped, {Failed} failed in {Duration}",
                operation.Id, operationState.EventsProcessed, totalEvents, operationState.EventsSkipped, 
                operationState.EventsFailed, DateTime.UtcNow - operationState.StartTime);

            // Update stored operation
            await UpdateReplayOperationStatusAsync(operation.Id, ReplayOperationStatus.Completed, 
                operationState.CompletedAt, null, cancellationToken);
        }
        catch (Exception ex)
        {
            operationState.Status = ReplayOperationStatus.Failed;
            operationState.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Replay operation {OperationId} failed", operation.Id);

            await UpdateReplayOperationStatusAsync(operation.Id, ReplayOperationStatus.Failed, 
                DateTime.UtcNow, ex.Message, cancellationToken);
        }
        finally
        {
            _activeReplays.TryRemove(operation.Id, out _);
        }
    }

    private async Task<BatchProcessingResult> ProcessEventBatchAsync(
        List<OutboxMessage> events,
        ReplayRequest request,
        CancellationToken cancellationToken)
    {
        var result = new BatchProcessingResult();

        foreach (var eventMessage in events)
        {
            try
            {
                // Skip if already processed and configured to ignore
                if (request.IgnoreProcessedEvents && eventMessage.ProcessedAtUtc.HasValue)
                {
                    result.SkippedCount++;
                    continue;
                }

                // Deserialize event
                var eventType = Type.GetType(eventMessage.Type);
                if (eventType == null)
                {
                    _logger.LogWarning("Unknown event type {EventType} in replay", eventMessage.Type);
                    result.FailedCount++;
                    continue;
                }

                var eventData = JsonSerializer.Deserialize(eventMessage.Payload, eventType);
                if (eventData == null)
                {
                    _logger.LogWarning("Failed to deserialize event {EventId} of type {EventType}", 
                        eventMessage.Id, eventMessage.Type);
                    result.FailedCount++;
                    continue;
                }

                // Dispatch replayed event
                if (eventData is IIntegrationEvent integrationEvent)
                {
                    // Mark as replay in metadata
                    var replayMetadata = new Dictionary<string, object>
                    {
                        ["IsReplay"] = true,
                        ["ReplayOperationId"] = request.OperationName,
                        ["OriginalOccurredAt"] = eventMessage.OccurredAtUtc
                    };

                    // Dispatch with replay context
                    await _eventDispatcher.DispatchAsync(new[] { integrationEvent }, cancellationToken);
                }

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventId} during replay", eventMessage.Id);
                result.FailedCount++;
            }
        }

        return result;
    }

    private sealed class ReplayOperationState
    {
        public ReplayOperation Operation { get; set; } = default!;
        public ReplayOperationStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalEvents { get; set; }
        public int EventsProcessed { get; set; }
        public int EventsSkipped { get; set; }
        public int EventsFailed { get; set; }
        public int CurrentBatch { get; set; }
        public string? CurrentBatchInfo { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private sealed class BatchProcessingResult
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }

    // Additional helper methods would be implemented here...
    private IQueryable<OutboxMessage> BuildEventQuery(IWriteDbContext context, ReplayRequest request) 
    {
        var query = context.Set<OutboxMessage>().AsQueryable();

        if (request.StartTimeUtc.HasValue)
            query = query.Where(e => e.OccurredAtUtc >= request.StartTimeUtc.Value);

        if (request.EndTimeUtc.HasValue)
            query = query.Where(e => e.OccurredAtUtc <= request.EndTimeUtc.Value);

        if (request.EventTypes.Any())
            query = query.Where(e => request.EventTypes.Contains(e.Type));

        if (request.SpecificEventIds.Any())
            query = query.Where(e => request.SpecificEventIds.Contains(e.Id));

        return query.OrderBy(e => e.OccurredAtUtc);
    }
    
    // ... other helper methods implementation
}
```

### 3. Configuration Options

**Location**: `src/BuildingBlocks/Infrastructure/Events/EventReplayOptions.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for event replay service
/// </summary>
public sealed class EventReplayOptions
{
    public const string ConfigurationSection = "EventReplay";
    
    /// <summary>
    /// Maximum number of concurrent replay operations
    /// Default: 2
    /// </summary>
    public int MaxConcurrentReplays { get; set; } = 2;
    
    /// <summary>
    /// Default batch size for replay operations
    /// Default: 100
    /// </summary>
    public int DefaultBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Maximum batch size allowed
    /// Default: 1000
    /// </summary>
    public int MaxBatchSize { get; set; } = 1000;
    
    /// <summary>
    /// Default delay between batches during replay
    /// Default: 100ms
    /// </summary>
    public TimeSpan DefaultBatchDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    
    /// <summary>
    /// Maximum replay operation duration before automatic cancellation
    /// Default: 1 hour
    /// </summary>
    public TimeSpan MaxReplayDuration { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Enable replay operation history storage
    /// Default: true
    /// </summary>
    public bool EnableReplayHistory { get; set; } = true;
    
    /// <summary>
    /// Replay history retention period
    /// Default: 30 days
    /// </summary>
    public TimeSpan ReplayHistoryRetention { get; set; } = TimeSpan.FromDays(30);
    
    /// <summary>
    /// Require authentication for replay operations
    /// Default: true
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;
}
```

## Tasks Breakdown

### Phase 1: Replay Service Foundation (3 hours)
- [ ] Create `IEventReplayService` interface with comprehensive request/response types
- [ ] Design replay validation and safety mechanisms
- [ ] Implement basic replay operation tracking and state management
- [ ] Create configuration options for replay service

### Phase 2: Replay Execution Logic (2 hours)
- [ ] Implement event filtering and batch processing logic
- [ ] Add event deserialization with schema migration support (Story_03)
- [ ] Create replay execution engine with progress tracking
- [ ] Add comprehensive error handling and rollback capabilities

### Phase 3: Integration & Monitoring (1 hour)
- [ ] Integrate with existing outbox processing infrastructure (Story_01)
- [ ] Add health checks and monitoring for replay operations
- [ ] Create replay operation history storage and retrieval
- [ ] Write comprehensive tests for replay scenarios

## Definition of Done

### Functionality
- [ ] Historical events can be replayed by time range, aggregate ID, and event type
- [ ] Dry-run validation prevents problematic replay operations
- [ ] Replay progress tracked and monitored with real-time status updates
- [ ] Replay operations isolated from live event processing

### Quality
- [ ] All operations return `Result<T>` following Epic_03 error patterns
- [ ] Comprehensive logging with correlation IDs for replay traceability  
- [ ] Event deduplication prevents duplicate processing during replay
- [ ] Unit tests cover replay logic, validation, and error scenarios

### Operations
- [ ] Replay operations configurable for different batch sizes and delays
- [ ] Health checks report replay service status and active operations
- [ ] Replay history available for operational analysis and auditing
- [ ] Performance metrics track replay operation efficiency

## Success Criteria

### Technical Success  
- Replay operations process events with < 10 second latency p95 for 1000 event batches
- Event validation prevents 100% of problematic replay attempts
- Replay progress tracking provides accurate ETAs within 10% margin
- Zero impact on live event processing during replay operations

### Operational Success
- Developers can debug production issues by replaying specific event sequences
- Operations teams can recover from failed processing by replaying missed events
- Replay operations provide complete audit trail for compliance requirements
- Replay service remains available with 99.9% uptime during normal operations

This story provides powerful debugging and recovery capabilities while maintaining the safety and reliability established in previous stories.