namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Request parameters for querying replay operation history.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Provides flexible filtering and pagination for historical replay data.
/// </summary>
public sealed record ReplayHistoryRequest
{
    /// <summary>
    /// Filter by specific operation IDs. If provided, only these operations will be included.
    /// </summary>
    public IReadOnlyList<Guid> OperationIds { get; init; } = [];

    /// <summary>
    /// Filter by operation names (supports partial matching).
    /// </summary>
    public IReadOnlyList<string> OperationNames { get; init; } = [];

    /// <summary>
    /// Filter by operation status.
    /// </summary>
    public IReadOnlyList<ReplayOperationStatus> Statuses { get; init; } = [];

    /// <summary>
    /// Filter operations that started on or after this date.
    /// </summary>
    public DateTime? StartedAfterUtc { get; init; }

    /// <summary>
    /// Filter operations that started on or before this date.
    /// </summary>
    public DateTime? StartedBeforeUtc { get; init; }

    /// <summary>
    /// Filter operations that completed on or after this date.
    /// </summary>
    public DateTime? CompletedAfterUtc { get; init; }

    /// <summary>
    /// Filter operations that completed on or before this date.
    /// </summary>
    public DateTime? CompletedBeforeUtc { get; init; }

    /// <summary>
    /// Filter by minimum duration of operations.
    /// </summary>
    public TimeSpan? MinDuration { get; init; }

    /// <summary>
    /// Filter by maximum duration of operations.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>
    /// Filter by minimum number of events processed.
    /// </summary>
    public long? MinEventsProcessed { get; init; }

    /// <summary>
    /// Filter by maximum number of events processed.
    /// </summary>
    public long? MaxEventsProcessed { get; init; }

    /// <summary>
    /// Filter by replay priority levels.
    /// </summary>
    public IReadOnlyList<ReplayPriority> Priorities { get; init; } = [];

    /// <summary>
    /// Filter by replay modes used.
    /// </summary>
    public IReadOnlyList<ReplayMode> Modes { get; init; } = [];

    /// <summary>
    /// Filter operations that had errors above this severity level.
    /// </summary>
    public ReplayErrorSeverity? MinErrorSeverity { get; init; }

    /// <summary>
    /// Filter by success rate range (0-100).
    /// </summary>
    public double? MinSuccessRate { get; init; }

    /// <summary>
    /// Filter by success rate range (0-100).
    /// </summary>
    public double? MaxSuccessRate { get; init; }

    /// <summary>
    /// Filter operations that processed specific event types.
    /// </summary>
    public IReadOnlyList<string> EventTypes { get; init; } = [];

    /// <summary>
    /// Filter operations that processed events for specific aggregate IDs.
    /// </summary>
    public IReadOnlyList<Guid> AggregateIds { get; init; } = [];

    /// <summary>
    /// Include operations initiated by specific users or systems.
    /// </summary>
    public IReadOnlyList<string> InitiatedBy { get; init; } = [];

    /// <summary>
    /// Search in operation metadata values.
    /// </summary>
    public IReadOnlyDictionary<string, string> MetadataFilters { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Number of records to skip for pagination.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Maximum number of records to return.
    /// </summary>
    public int Take { get; init; } = 50;

    /// <summary>
    /// Sort order for the results.
    /// </summary>
    public ReplayHistorySortBy SortBy { get; init; } = ReplayHistorySortBy.StartedAtDescending;

    /// <summary>
    /// Whether to include detailed performance metrics in the results.
    /// </summary>
    public bool IncludeMetrics { get; init; }

    /// <summary>
    /// Whether to include error details in the results.
    /// </summary>
    public bool IncludeErrors { get; init; }

    /// <summary>
    /// Whether to include diagnostic information in the results.
    /// </summary>
    public bool IncludeDiagnostics { get; init; }

    /// <summary>
    /// Validates the request parameters.
    /// </summary>
    /// <returns>Validation result with any errors found</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Take <= 0)
            errors.Add("Take must be greater than 0");

        if (Take > 1000)
            errors.Add("Take cannot exceed 1000 records");

        if (Skip < 0)
            errors.Add("Skip cannot be negative");

        if (StartedAfterUtc.HasValue && StartedBeforeUtc.HasValue && 
            StartedAfterUtc.Value >= StartedBeforeUtc.Value)
            errors.Add("StartedAfterUtc must be earlier than StartedBeforeUtc");

        if (CompletedAfterUtc.HasValue && CompletedBeforeUtc.HasValue && 
            CompletedAfterUtc.Value >= CompletedBeforeUtc.Value)
            errors.Add("CompletedAfterUtc must be earlier than CompletedBeforeUtc");

        if (MinDuration.HasValue && MaxDuration.HasValue && MinDuration.Value >= MaxDuration.Value)
            errors.Add("MinDuration must be less than MaxDuration");

        if (MinEventsProcessed.HasValue && MaxEventsProcessed.HasValue && 
            MinEventsProcessed.Value >= MaxEventsProcessed.Value)
            errors.Add("MinEventsProcessed must be less than MaxEventsProcessed");

        if (MinSuccessRate.HasValue && (MinSuccessRate.Value < 0 || MinSuccessRate.Value > 100))
            errors.Add("MinSuccessRate must be between 0 and 100");

        if (MaxSuccessRate.HasValue && (MaxSuccessRate.Value < 0 || MaxSuccessRate.Value > 100))
            errors.Add("MaxSuccessRate must be between 0 and 100");

        if (MinSuccessRate.HasValue && MaxSuccessRate.HasValue && 
            MinSuccessRate.Value >= MaxSuccessRate.Value)
            errors.Add("MinSuccessRate must be less than MaxSuccessRate");

        return errors;
    }
}

/// <summary>
/// Sort options for replay history queries.
/// </summary>
public enum ReplayHistorySortBy
{
    /// <summary>
    /// Sort by start time, newest first.
    /// </summary>
    StartedAtDescending = 0,

    /// <summary>
    /// Sort by start time, oldest first.
    /// </summary>
    StartedAtAscending = 1,

    /// <summary>
    /// Sort by completion time, newest first.
    /// </summary>
    CompletedAtDescending = 2,

    /// <summary>
    /// Sort by completion time, oldest first.
    /// </summary>
    CompletedAtAscending = 3,

    /// <summary>
    /// Sort by duration, longest first.
    /// </summary>
    DurationDescending = 4,

    /// <summary>
    /// Sort by duration, shortest first.
    /// </summary>
    DurationAscending = 5,

    /// <summary>
    /// Sort by events processed, most first.
    /// </summary>
    EventsProcessedDescending = 6,

    /// <summary>
    /// Sort by events processed, least first.
    /// </summary>
    EventsProcessedAscending = 7,

    /// <summary>
    /// Sort by success rate, highest first.
    /// </summary>
    SuccessRateDescending = 8,

    /// <summary>
    /// Sort by success rate, lowest first.
    /// </summary>
    SuccessRateAscending = 9,

    /// <summary>
    /// Sort by operation name alphabetically.
    /// </summary>
    NameAscending = 10,

    /// <summary>
    /// Sort by operation name reverse alphabetically.
    /// </summary>
    NameDescending = 11
}