namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Complete result information from a finished replay operation.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Contains comprehensive information about the outcome of a replay operation.
/// </summary>
public sealed record ReplayResult
{
    /// <summary>
    /// Unique identifier of the replay operation.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Human-readable name of the replay operation.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Final status of the replay operation.
    /// </summary>
    public ReplayOperationStatus FinalStatus { get; init; }

    /// <summary>
    /// When the replay operation started in UTC.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// When the replay operation completed in UTC.
    /// </summary>
    public DateTime CompletedAtUtc { get; init; }

    /// <summary>
    /// Total duration of the replay operation.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc.Subtract(StartedAtUtc);

    /// <summary>
    /// Total number of events that were processed.
    /// </summary>
    public long TotalEventsProcessed { get; init; }

    /// <summary>
    /// Number of events that were processed successfully.
    /// </summary>
    public long SuccessfulEventsCount { get; init; }

    /// <summary>
    /// Number of events that failed processing.
    /// </summary>
    public long FailedEventsCount { get; init; }

    /// <summary>
    /// Number of events that were skipped.
    /// </summary>
    public long SkippedEventsCount { get; init; }

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalEventsProcessed > 0 
        ? (double)SuccessfulEventsCount / TotalEventsProcessed * 100.0 
        : 0.0;

    /// <summary>
    /// Average processing rate in events per second.
    /// </summary>
    public double AverageProcessingRate { get; init; }

    /// <summary>
    /// Performance metrics collected during the operation.
    /// </summary>
    public ReplayPerformanceMetrics? PerformanceMetrics { get; init; }

    /// <summary>
    /// Summary of errors that occurred during replay.
    /// </summary>
    public ReplayErrorSummary? ErrorSummary { get; init; }

    /// <summary>
    /// Configuration used for this replay operation.
    /// </summary>
    public ReplayConfiguration Configuration { get; init; } = default!;

    /// <summary>
    /// Final error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Detailed diagnostic information collected during replay.
    /// </summary>
    public ReplayDiagnosticInfo? DiagnosticInfo { get; init; }

    /// <summary>
    /// Custom metadata associated with this operation.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();

    /// <summary>
    /// Indicates if the operation completed successfully.
    /// </summary>
    public bool IsSuccessful => FinalStatus == ReplayOperationStatus.Completed;

    /// <summary>
    /// Indicates if the operation failed.
    /// </summary>
    public bool HasFailed => FinalStatus == ReplayOperationStatus.Failed;

    /// <summary>
    /// Indicates if the operation was cancelled.
    /// </summary>
    public bool WasCancelled => FinalStatus == ReplayOperationStatus.Cancelled;

    /// <summary>
    /// Creates a human-readable summary of the replay result.
    /// </summary>
    /// <returns>Formatted result summary</returns>
    public string CreateSummary()
    {
        var statusText = FinalStatus switch
        {
            ReplayOperationStatus.Completed => "✓ Completed Successfully",
            ReplayOperationStatus.Failed => "✗ Failed",
            ReplayOperationStatus.Cancelled => "⚠ Cancelled",
            _ => FinalStatus.ToString()
        };

        return $"""
            Replay Operation Result: {OperationName}
            Status: {statusText}
            Duration: {Duration:hh\:mm\:ss}
            Events Processed: {TotalEventsProcessed:N0}
            Success Rate: {SuccessRate:F1}%
            Average Rate: {AverageProcessingRate:F2} events/sec
            Started: {StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
            Completed: {CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }
}

/// <summary>
/// Summary of errors that occurred during a replay operation.
/// </summary>
public sealed record ReplayErrorSummary
{
    /// <summary>
    /// Total number of errors encountered.
    /// </summary>
    public int TotalErrorCount { get; init; }

    /// <summary>
    /// Number of unique error types encountered.
    /// </summary>
    public int UniqueErrorTypeCount { get; init; }

    /// <summary>
    /// Most frequent error types and their occurrence counts.
    /// </summary>
    public IReadOnlyDictionary<ReplayErrorType, int> ErrorTypeFrequency { get; init; } =
        new Dictionary<ReplayErrorType, int>();

    /// <summary>
    /// Most frequent error severity levels and their counts.
    /// </summary>
    public IReadOnlyDictionary<ReplayErrorSeverity, int> SeverityFrequency { get; init; } =
        new Dictionary<ReplayErrorSeverity, int>();

    /// <summary>
    /// List of the most critical errors encountered.
    /// </summary>
    public IReadOnlyList<ReplayError> CriticalErrors { get; init; } = [];

    /// <summary>
    /// List of the most common error messages.
    /// </summary>
    public IReadOnlyList<string> MostCommonErrors { get; init; } = [];

    /// <summary>
    /// Number of errors that were recoverable.
    /// </summary>
    public int RecoverableErrorCount { get; init; }

    /// <summary>
    /// Number of errors that were not recoverable.
    /// </summary>
    public int NonRecoverableErrorCount { get; init; }

    /// <summary>
    /// Error rate as errors per processed event.
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Indicates if there were any critical errors.
    /// </summary>
    public bool HasCriticalErrors => CriticalErrors.Count > 0;

    /// <summary>
    /// Recovery rate for errors as a percentage (0-100).
    /// </summary>
    public double RecoveryRate => TotalErrorCount > 0 
        ? (double)RecoverableErrorCount / TotalErrorCount * 100.0 
        : 0.0;
}

/// <summary>
/// Health information for the replay service.
/// </summary>
public sealed record ReplayServiceHealth
{
    /// <summary>
    /// Overall health status of the service.
    /// </summary>
    public ReplayHealthStatus Status { get; init; }

    /// <summary>
    /// Number of currently active replay operations.
    /// </summary>
    public int ActiveOperationsCount { get; init; }

    /// <summary>
    /// Maximum number of concurrent operations allowed.
    /// </summary>
    public int MaxConcurrentOperations { get; init; }

    /// <summary>
    /// Current resource utilization percentage.
    /// </summary>
    public double ResourceUtilizationPercent { get; init; }

    /// <summary>
    /// Average processing rate across all active operations.
    /// </summary>
    public double AverageProcessingRate { get; init; }

    /// <summary>
    /// System uptime since service started.
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Last known error or issue.
    /// </summary>
    public string? LastKnownIssue { get; init; }

    /// <summary>
    /// When the last issue occurred.
    /// </summary>
    public DateTime? LastIssueTimestamp { get; init; }

    /// <summary>
    /// Service version information.
    /// </summary>
    public string? ServiceVersion { get; init; }

    /// <summary>
    /// Additional health check details.
    /// </summary>
    public IReadOnlyDictionary<string, object> HealthChecks { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Indicates if the service is operating normally.
    /// </summary>
    public bool IsHealthy => Status == ReplayHealthStatus.Healthy;

    /// <summary>
    /// Indicates if the service has capacity for new operations.
    /// </summary>
    public bool HasCapacity => ActiveOperationsCount < MaxConcurrentOperations;

    /// <summary>
    /// Creates a summary of service health.
    /// </summary>
    /// <returns>Health status summary</returns>
    public string CreateHealthSummary()
    {
        var statusIcon = Status switch
        {
            ReplayHealthStatus.Healthy => "✓",
            ReplayHealthStatus.Degraded => "⚠",
            ReplayHealthStatus.Critical => "⚠",
            ReplayHealthStatus.Unavailable => "✗",
            _ => "?"
        };

        return $"""
            Replay Service Health: {statusIcon} {Status}
            Active Operations: {ActiveOperationsCount}/{MaxConcurrentOperations}
            Resource Utilization: {ResourceUtilizationPercent:F1}%
            Average Processing Rate: {AverageProcessingRate:F2} events/sec
            Uptime: {Uptime:dd\\.hh\\:mm\\:ss}
            """;
    }
}