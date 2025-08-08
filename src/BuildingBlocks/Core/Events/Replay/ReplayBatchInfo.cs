namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Information about a specific batch of events being processed during replay.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides detailed tracking of batch-level processing for monitoring and diagnostics.
/// </summary>
public sealed record ReplayBatchInfo
{
    /// <summary>
    /// Sequential batch number within the replay operation.
    /// Starts at 1 for the first batch.
    /// </summary>
    public int BatchNumber { get; init; }

    /// <summary>
    /// When this batch started processing in UTC.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// When this batch completed processing in UTC.
    /// Null if batch is still processing.
    /// </summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Current status of this batch.
    /// </summary>
    public ReplayBatchStatus Status { get; init; }

    /// <summary>
    /// Total number of events in this batch.
    /// Set when batch is created.
    /// </summary>
    public int TotalEventsInBatch { get; init; }

    /// <summary>
    /// Number of events successfully processed in this batch.
    /// </summary>
    public int ProcessedEventsInBatch { get; init; }

    /// <summary>
    /// Number of events that failed processing in this batch.
    /// </summary>
    public int FailedEventsInBatch { get; init; }

    /// <summary>
    /// Number of events skipped in this batch.
    /// </summary>
    public int SkippedEventsInBatch { get; init; }

    /// <summary>
    /// Time range of events in this batch (earliest event time).
    /// </summary>
    public DateTime? BatchStartTime { get; init; }

    /// <summary>
    /// Time range of events in this batch (latest event time).
    /// </summary>
    public DateTime? BatchEndTime { get; init; }

    /// <summary>
    /// Event version range for this batch (minimum version).
    /// </summary>
    public long? MinEventVersion { get; init; }

    /// <summary>
    /// Event version range for this batch (maximum version).
    /// </summary>
    public long? MaxEventVersion { get; init; }

    /// <summary>
    /// Average processing time per event in this batch.
    /// </summary>
    public TimeSpan? AverageProcessingTime { get; init; }

    /// <summary>
    /// Error message if batch failed.
    /// Contains information about what caused the batch to fail.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Memory usage peak during this batch processing.
    /// Useful for resource monitoring and optimization.
    /// </summary>
    public long? PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Custom metadata for this batch.
    /// Can include debugging information or batch-specific context.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Calculates the processing duration for this batch.
    /// Returns elapsed time for running batches or total duration for completed ones.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc?.Subtract(StartedAtUtc) ?? 
                               DateTime.UtcNow.Subtract(StartedAtUtc);

    /// <summary>
    /// Calculates the success rate for this batch as a percentage.
    /// </summary>
    public double SuccessRate => TotalEventsInBatch > 0 
        ? (double)ProcessedEventsInBatch / TotalEventsInBatch * 100.0 
        : 0.0;

    /// <summary>
    /// Indicates if the batch is currently being processed.
    /// </summary>
    public bool IsProcessing => Status == ReplayBatchStatus.Processing;

    /// <summary>
    /// Indicates if the batch completed successfully.
    /// </summary>
    public bool IsCompleted => Status == ReplayBatchStatus.Completed;

    /// <summary>
    /// Indicates if the batch encountered errors.
    /// </summary>
    public bool HasErrors => Status is ReplayBatchStatus.Failed or 
                                   ReplayBatchStatus.PartiallyCompleted;
}

/// <summary>
/// Status values for replay batch processing.
/// </summary>
public enum ReplayBatchStatus
{
    /// <summary>
    /// Batch is queued for processing.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Batch is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Batch completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Batch failed to process.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Batch completed with some failures.
    /// </summary>
    PartiallyCompleted = 4,

    /// <summary>
    /// Batch processing was cancelled.
    /// </summary>
    Cancelled = 5
}