namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Information about a replay operation including configuration and execution status.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Provides complete tracking and monitoring of replay operation lifecycle.
/// </summary>
public sealed record ReplayOperation
{
    /// <summary>
    /// Unique identifier for this replay operation.
    /// Used for tracking, status queries, and operation management.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Human-readable name for this replay operation.
    /// Specified in the original request for operational identification.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Optional detailed description of the replay operation.
    /// Used for auditing and operational documentation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Complete replay request that initiated this operation.
    /// Contains all original parameters and configuration.
    /// </summary>
    public required ReplayRequest Request { get; init; }

    /// <summary>
    /// When this replay operation was started in UTC.
    /// Set when the operation begins execution.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// When this replay operation completed in UTC.
    /// Null while operation is still running.
    /// </summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Current status of the replay operation.
    /// Updated in real-time as operation progresses.
    /// </summary>
    public ReplayOperationStatus Status { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// Contains detailed error information for troubleshooting.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total number of events scheduled for replay.
    /// Set after initial event discovery and filtering.
    /// </summary>
    public long? TotalEventsCount { get; init; }

    /// <summary>
    /// Number of events successfully processed.
    /// Updated in real-time during execution.
    /// </summary>
    public long ProcessedEventsCount { get; init; }

    /// <summary>
    /// Number of events that failed processing.
    /// Includes events that exceeded retry limits.
    /// </summary>
    public long FailedEventsCount { get; init; }

    /// <summary>
    /// Number of events skipped due to filters or duplicate prevention.
    /// </summary>
    public long SkippedEventsCount { get; init; }

    /// <summary>
    /// Estimated time remaining for operation completion.
    /// Based on current processing rate and remaining events.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current processing rate in events per second.
    /// Calculated based on recent processing performance.
    /// </summary>
    public double? ProcessingRatePerSecond { get; init; }

    /// <summary>
    /// Progress percentage from 0 to 100.
    /// Based on processed events vs total events.
    /// </summary>
    public double? ProgressPercentage { get; init; }

    /// <summary>
    /// User or system that initiated this replay operation.
    /// Used for auditing and access control.
    /// </summary>
    public string? InitiatedBy { get; init; }

    /// <summary>
    /// Correlation ID for tracking across systems.
    /// Links this operation to external monitoring systems.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Custom metadata associated with this operation.
    /// Contains operational context and additional tracking information.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Most recent processing batch information.
    /// Updated as batches complete processing.
    /// </summary>
    public ReplayBatchInfo? CurrentBatch { get; init; }

    /// <summary>
    /// Recent error details for troubleshooting.
    /// Contains last N errors with timestamps and context.
    /// </summary>
    public IReadOnlyList<ReplayError> RecentErrors { get; init; } = [];

    /// <summary>
    /// Performance metrics for this operation.
    /// Includes throughput, resource usage, and timing information.
    /// </summary>
    public ReplayPerformanceMetrics? PerformanceMetrics { get; init; }

    /// <summary>
    /// Calculates the duration of the replay operation.
    /// Returns elapsed time for running operations or total duration for completed ones.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc?.Subtract(StartedAtUtc) ?? 
                               DateTime.UtcNow.Subtract(StartedAtUtc);

    /// <summary>
    /// Indicates if the operation is currently active.
    /// </summary>
    public bool IsActive => Status is ReplayOperationStatus.Running or 
                                   ReplayOperationStatus.Starting or 
                                   ReplayOperationStatus.Paused;

    /// <summary>
    /// Indicates if the operation completed successfully.
    /// </summary>
    public bool IsSuccessful => Status == ReplayOperationStatus.Completed;

    /// <summary>
    /// Indicates if the operation failed or was cancelled.
    /// </summary>
    public bool IsFailed => Status is ReplayOperationStatus.Failed or 
                                  ReplayOperationStatus.Cancelled;
}