namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Real-time status and progress information for replay operations.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Provides comprehensive monitoring and progress tracking for active replay operations.
/// </summary>
public sealed record ReplayStatus
{
    /// <summary>
    /// Unique identifier of the replay operation being tracked.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Human-readable name of the replay operation.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Current status of the replay operation.
    /// </summary>
    public ReplayOperationStatus Status { get; init; }

    /// <summary>
    /// When the replay operation was started in UTC.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// When the replay operation completed in UTC (if finished).
    /// </summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Current operation phase or stage.
    /// </summary>
    public ReplayPhase CurrentPhase { get; init; }

    /// <summary>
    /// Description of what's currently happening in the replay.
    /// </summary>
    public string? CurrentActivity { get; init; }

    /// <summary>
    /// Total number of events scheduled for replay.
    /// Available after event discovery phase completes.
    /// </summary>
    public long? TotalEventsCount { get; init; }

    /// <summary>
    /// Number of events successfully processed so far.
    /// </summary>
    public long ProcessedEventsCount { get; init; }

    /// <summary>
    /// Number of events that failed processing so far.
    /// </summary>
    public long FailedEventsCount { get; init; }

    /// <summary>
    /// Number of events skipped due to filters or duplicate prevention.
    /// </summary>
    public long SkippedEventsCount { get; init; }

    /// <summary>
    /// Progress percentage from 0 to 100.
    /// Based on processed events vs total events.
    /// </summary>
    public double? ProgressPercentage { get; init; }

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
    /// Average processing rate since operation started.
    /// </summary>
    public double? AverageProcessingRatePerSecond { get; init; }

    /// <summary>
    /// Information about the currently active batch.
    /// </summary>
    public ReplayBatchInfo? CurrentBatch { get; init; }

    /// <summary>
    /// Number of batches completed so far.
    /// </summary>
    public int CompletedBatchesCount { get; init; }

    /// <summary>
    /// Total number of batches expected for this replay.
    /// </summary>
    public int? TotalBatchesCount { get; init; }

    /// <summary>
    /// Current memory usage in bytes.
    /// </summary>
    public long? CurrentMemoryUsageBytes { get; init; }

    /// <summary>
    /// Peak memory usage reached during this replay in bytes.
    /// </summary>
    public long? PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Current CPU usage percentage.
    /// </summary>
    public double? CurrentCpuUsagePercent { get; init; }

    /// <summary>
    /// Current number of active worker threads.
    /// </summary>
    public int? ActiveWorkerThreads { get; init; }

    /// <summary>
    /// Recent errors that occurred during processing.
    /// Limited to the most recent N errors for performance.
    /// </summary>
    public IReadOnlyList<ReplayError> RecentErrors { get; init; } = [];

    /// <summary>
    /// Error message if the operation has failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Health status of the replay service.
    /// </summary>
    public ReplayHealthStatus HealthStatus { get; init; }

    /// <summary>
    /// Additional metrics and diagnostic information.
    /// </summary>
    public ReplayDiagnosticInfo? DiagnosticInfo { get; init; }

    /// <summary>
    /// Custom metadata associated with this replay operation.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Last time this status was updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; }

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
    /// Indicates if the operation has completed successfully.
    /// </summary>
    public bool IsCompleted => Status == ReplayOperationStatus.Completed;

    /// <summary>
    /// Indicates if the operation has failed or been cancelled.
    /// </summary>
    public bool HasFailed => Status is ReplayOperationStatus.Failed or 
                                   ReplayOperationStatus.Cancelled;

    /// <summary>
    /// Indicates if there are recent errors to investigate.
    /// </summary>
    public bool HasRecentErrors => RecentErrors.Count > 0;

    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var totalProcessed = ProcessedEventsCount + FailedEventsCount;
            return totalProcessed > 0 ? (double)ProcessedEventsCount / totalProcessed * 100.0 : 0.0;
        }
    }

    /// <summary>
    /// Creates a human-readable status summary.
    /// </summary>
    /// <returns>Formatted status summary</returns>
    public string CreateSummary()
    {
        var statusText = Status switch
        {
            ReplayOperationStatus.Running => "Running",
            ReplayOperationStatus.Completed => "Completed",
            ReplayOperationStatus.Failed => "Failed",
            ReplayOperationStatus.Cancelled => "Cancelled",
            ReplayOperationStatus.Paused => "Paused",
            _ => Status.ToString()
        };

        var progressText = ProgressPercentage?.ToString("F1") ?? "N/A";
        var rateText = ProcessingRatePerSecond?.ToString("F2") ?? "N/A";
        var etaText = EstimatedTimeRemaining?.ToString(@"hh\:mm\:ss") ?? "N/A";

        return $"""
            Replay Operation: {OperationName}
            Status: {statusText} ({CurrentPhase})
            Progress: {progressText}% ({ProcessedEventsCount:N0}/{TotalEventsCount?.ToString("N0") ?? "?"} events)
            Rate: {rateText} events/sec
            ETA: {etaText}
            Duration: {Duration:hh\:mm\:ss}
            Success Rate: {SuccessRate:F1}%
            """;
    }
}

/// <summary>
/// Different phases of replay operation execution.
/// </summary>
public enum ReplayPhase
{
    /// <summary>
    /// Operation is initializing and validating parameters.
    /// </summary>
    Initializing = 0,

    /// <summary>
    /// Discovering and filtering events to be replayed.
    /// </summary>
    DiscoveringEvents = 1,

    /// <summary>
    /// Planning batch execution strategy.
    /// </summary>
    PlanningExecution = 2,

    /// <summary>
    /// Actively processing events in batches.
    /// </summary>
    ProcessingEvents = 3,

    /// <summary>
    /// Retrying failed events.
    /// </summary>
    RetryingFailures = 4,

    /// <summary>
    /// Finalizing operation and cleanup.
    /// </summary>
    Finalizing = 5,

    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Completed = 6,

    /// <summary>
    /// Operation failed or was cancelled.
    /// </summary>
    Terminated = 7
}

/// <summary>
/// Health status of the replay service.
/// </summary>
public enum ReplayHealthStatus
{
    /// <summary>
    /// Service is operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Service is degraded but still functional.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Service has critical issues but may still process some operations.
    /// </summary>
    Critical = 2,

    /// <summary>
    /// Service is unavailable or not responding.
    /// </summary>
    Unavailable = 3
}