using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Comprehensive request parameters for event replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides extensive filtering, execution options, and safety controls for reliable event replay.
/// </summary>
public sealed record ReplayRequest
{
    /// <summary>
    /// Human-readable name for this replay operation.
    /// Used for tracking, monitoring, and operational identification.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string OperationName { get; init; }

    /// <summary>
    /// Optional detailed description of the replay operation.
    /// Used for auditing and operational documentation.
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// Start time boundary for event filtering (inclusive).
    /// Only events that occurred on or after this time will be included.
    /// </summary>
    public DateTime? StartTimeUtc { get; init; }

    /// <summary>
    /// End time boundary for event filtering (exclusive).
    /// Only events that occurred before this time will be included.
    /// </summary>
    public DateTime? EndTimeUtc { get; init; }

    /// <summary>
    /// Specific event types to include in replay.
    /// If empty, all event types within time range will be included.
    /// </summary>
    public IReadOnlyList<string> EventTypes { get; init; } = [];

    /// <summary>
    /// Specific aggregate IDs to include in replay.
    /// If empty, events from all aggregates will be included.
    /// </summary>
    public IReadOnlyList<string> AggregateIds { get; init; } = [];

    /// <summary>
    /// Specific event IDs for precise replay control.
    /// If specified, only these exact events will be replayed regardless of other filters.
    /// </summary>
    public IReadOnlyList<Guid> SpecificEventIds { get; init; } = [];

    /// <summary>
    /// Event stream names to include in replay.
    /// Useful for replaying events from specific bounded contexts or modules.
    /// </summary>
    public IReadOnlyList<string> StreamNames { get; init; } = [];

    /// <summary>
    /// Minimum event version to include (inclusive).
    /// Used for incremental replay operations.
    /// </summary>
    public long? MinEventVersion { get; init; }

    /// <summary>
    /// Maximum event version to include (inclusive).
    /// Used for controlled replay up to a specific point.
    /// </summary>
    public long? MaxEventVersion { get; init; }

    /// <summary>
    /// Validation-only mode - performs all checks without executing the replay.
    /// Useful for testing replay configuration and safety validation.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Number of events to process in each batch.
    /// Larger batches improve throughput but consume more memory.
    /// </summary>
    [Range(1, 10000)]
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Delay between processing batches to control system load.
    /// Helps prevent overwhelming downstream systems during replay.
    /// </summary>
    public TimeSpan DelayBetweenBatches { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum number of concurrent batch operations.
    /// Higher concurrency improves performance but increases resource usage.
    /// </summary>
    [Range(1, 10)]
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Prevent processing duplicate events during replay.
    /// Maintains idempotency by checking event processing history.
    /// </summary>
    public bool PreventDuplicates { get; init; } = true;

    /// <summary>
    /// Skip events that have already been processed successfully.
    /// Useful for resuming interrupted replay operations.
    /// </summary>
    public bool IgnoreProcessedEvents { get; init; } = true;

    /// <summary>
    /// Continue processing even when individual events fail.
    /// If false, replay stops on first error.
    /// </summary>
    public bool ContinueOnError { get; init; }

    /// <summary>
    /// Maximum number of retry attempts for failed events.
    /// Failed events will be retried with exponential backoff.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Initial delay for retry attempts with exponential backoff.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How events should be ordered during replay.
    /// </summary>
    public ReplayMode Mode { get; init; } = ReplayMode.Chronological;

    /// <summary>
    /// Priority level for this replay operation.
    /// Higher priority operations get more system resources.
    /// </summary>
    public ReplayPriority Priority { get; init; } = ReplayPriority.Normal;

    /// <summary>
    /// Maximum duration allowed for the entire replay operation.
    /// Operation will be cancelled if it exceeds this timeout.
    /// </summary>
    public TimeSpan? MaxExecutionTime { get; init; }

    /// <summary>
    /// Custom metadata for the replay operation.
    /// Can include correlation IDs, user information, or operational context.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Validates the replay request for logical consistency and required parameters.
    /// </summary>
    /// <returns>List of validation errors, empty if request is valid</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OperationName))
            errors.Add("Operation name is required");

        if (StartTimeUtc.HasValue && EndTimeUtc.HasValue && StartTimeUtc >= EndTimeUtc)
            errors.Add("Start time must be before end time");

        if (MinEventVersion.HasValue && MaxEventVersion.HasValue && MinEventVersion > MaxEventVersion)
            errors.Add("Minimum event version must be less than or equal to maximum event version");

        if (BatchSize <= 0)
            errors.Add("Batch size must be greater than 0");

        if (DelayBetweenBatches < TimeSpan.Zero)
            errors.Add("Delay between batches cannot be negative");

        if (MaxConcurrency <= 0)
            errors.Add("Max concurrency must be greater than 0");

        if (MaxRetryAttempts < 0)
            errors.Add("Max retry attempts cannot be negative");

        if (RetryDelay < TimeSpan.Zero)
            errors.Add("Retry delay cannot be negative");

        if (MaxExecutionTime.HasValue && MaxExecutionTime <= TimeSpan.Zero)
            errors.Add("Max execution time must be positive");

        return errors;
    }
}