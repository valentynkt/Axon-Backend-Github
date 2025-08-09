namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Configuration for replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayConfiguration
{
    /// <summary>
    /// Number of events to process in each batch.
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Maximum number of concurrent operations.
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Maximum number of retry attempts for failed events.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Delay between processing batches.
    /// </summary>
    public TimeSpan DelayBetweenBatches { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Continue processing even when individual events fail.
    /// </summary>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// Prevent processing duplicate events.
    /// </summary>
    public bool PreventDuplicates { get; init; } = true;
}

/// <summary>
/// Tracks the progress of a replay operation.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayProgress
{
    /// <summary>
    /// Operation identifier.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Current phase of the operation.
    /// </summary>
    public ReplayPhase CurrentPhase { get; init; }

    /// <summary>
    /// Total number of events to process.
    /// </summary>
    public long? TotalEventsCount { get; init; }

    /// <summary>
    /// Number of events processed so far.
    /// </summary>
    public long ProcessedEventsCount { get; init; }

    /// <summary>
    /// Number of events that failed processing.
    /// </summary>
    public long FailedEventsCount { get; init; }

    /// <summary>
    /// Number of events skipped.
    /// </summary>
    public long SkippedEventsCount { get; init; }

    /// <summary>
    /// Current processing rate in events per second.
    /// </summary>
    public double? ProcessingRatePerSecond { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; }
}

/// <summary>
/// Result of replay validation.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; init; } = [];

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Estimated event count that would be replayed.
    /// </summary>
    public long? EstimatedEventCount { get; init; }

    /// <summary>
    /// Estimated time to complete the replay.
    /// </summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>
    /// Resources required for the replay.
    /// </summary>
    public IReadOnlyDictionary<string, string> RequiredResources { get; init; } = 
        new Dictionary<string, string>();
}

/// <summary>
/// Result of cancelling replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayCancellationResult
{
    /// <summary>
    /// Number of operations cancelled.
    /// </summary>
    public int CancelledCount { get; init; }

    /// <summary>
    /// IDs of cancelled operations.
    /// </summary>
    public IReadOnlyList<Guid> CancelledOperationIds { get; init; } = [];

    /// <summary>
    /// Reason for cancellation.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of cancellation.
    /// </summary>
    public DateTime CancelledAtUtc { get; init; }
}

/// <summary>
/// Metrics for replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayMetrics
{
    /// <summary>
    /// Time period for the metrics.
    /// </summary>
    public DateTime StartTimeUtc { get; init; }

    /// <summary>
    /// End time for the metrics period.
    /// </summary>
    public DateTime EndTimeUtc { get; init; }

    /// <summary>
    /// Total number of replay operations.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessfulOperations { get; init; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    public int FailedOperations { get; init; }

    /// <summary>
    /// Total events processed.
    /// </summary>
    public long TotalEventsProcessed { get; init; }

    /// <summary>
    /// Average processing rate across all operations.
    /// </summary>
    public double AverageProcessingRate { get; init; }

    /// <summary>
    /// Peak processing rate achieved.
    /// </summary>
    public double PeakProcessingRate { get; init; }

    /// <summary>
    /// Average operation duration.
    /// </summary>
    public TimeSpan AverageOperationDuration { get; init; }

    /// <summary>
    /// Resource utilization statistics.
    /// </summary>
    public IReadOnlyDictionary<string, double> ResourceUtilization { get; init; } = 
        new Dictionary<string, double>();
}

/// <summary>
/// Request for replay metrics.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// </summary>
public sealed record ReplayMetricsRequest
{
    /// <summary>
    /// Start of the time period for metrics.
    /// </summary>
    public DateTime StartTimeUtc { get; init; }

    /// <summary>
    /// End of the time period for metrics.
    /// </summary>
    public DateTime EndTimeUtc { get; init; }

    /// <summary>
    /// Specific operation IDs to include.
    /// </summary>
    public IReadOnlyList<Guid> OperationIds { get; init; } = [];

    /// <summary>
    /// Include detailed breakdowns.
    /// </summary>
    public bool IncludeDetails { get; init; }
}