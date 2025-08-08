namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Information about errors that occurred during replay processing.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides detailed error tracking for troubleshooting and monitoring.
/// </summary>
public sealed record ReplayError
{
    /// <summary>
    /// Unique identifier for this error occurrence.
    /// </summary>
    public Guid ErrorId { get; init; }

    /// <summary>
    /// When this error occurred in UTC.
    /// </summary>
    public DateTime OccurredAtUtc { get; init; }

    /// <summary>
    /// Type of error that occurred.
    /// </summary>
    public ReplayErrorType ErrorType { get; init; }

    /// <summary>
    /// Severity level of this error.
    /// </summary>
    public ReplayErrorSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Detailed error information including stack trace if available.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// ID of the event that caused this error, if applicable.
    /// </summary>
    public Guid? EventId { get; init; }

    /// <summary>
    /// Type of the event that caused this error, if applicable.
    /// </summary>
    public string? EventType { get; init; }

    /// <summary>
    /// Batch number where this error occurred.
    /// </summary>
    public int? BatchNumber { get; init; }

    /// <summary>
    /// Component or stage where the error occurred.
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// Number of retry attempts for this specific error.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Whether this error is considered recoverable.
    /// Recoverable errors may be retried, non-recoverable errors stop processing.
    /// </summary>
    public bool IsRecoverable { get; init; }

    /// <summary>
    /// Correlation ID linking this error to external systems.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional context information about the error.
    /// </summary>
    public IReadOnlyDictionary<string, string> Context { get; init; } = 
        new Dictionary<string, string>();
}

/// <summary>
/// Types of errors that can occur during replay operations.
/// </summary>
public enum ReplayErrorType
{
    /// <summary>
    /// Unknown or unclassified error type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Error occurred while reading events from storage.
    /// </summary>
    EventReadError = 1,

    /// <summary>
    /// Error occurred while processing/handling an event.
    /// </summary>
    EventProcessingError = 2,

    /// <summary>
    /// Error occurred while validating event data.
    /// </summary>
    EventValidationError = 3,

    /// <summary>
    /// Error occurred while deserializing event data.
    /// </summary>
    EventDeserializationError = 4,

    /// <summary>
    /// Error occurred due to schema compatibility issues.
    /// </summary>
    SchemaCompatibilityError = 5,

    /// <summary>
    /// Error occurred in the replay infrastructure or framework.
    /// </summary>
    InfrastructureError = 6,

    /// <summary>
    /// Error occurred due to resource constraints (memory, CPU, etc.).
    /// </summary>
    ResourceError = 7,

    /// <summary>
    /// Error occurred due to network connectivity issues.
    /// </summary>
    NetworkError = 8,

    /// <summary>
    /// Error occurred due to timeout conditions.
    /// </summary>
    TimeoutError = 9,

    /// <summary>
    /// Error occurred due to concurrency or threading issues.
    /// </summary>
    ConcurrencyError = 10,

    /// <summary>
    /// Error occurred due to configuration or setup issues.
    /// </summary>
    ConfigurationError = 11,

    /// <summary>
    /// Error occurred due to permission or security issues.
    /// </summary>
    SecurityError = 12
}

/// <summary>
/// Severity levels for replay errors.
/// </summary>
public enum ReplayErrorSeverity
{
    /// <summary>
    /// Low severity - informational or warning level.
    /// Does not affect replay operation significantly.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - may affect some events but operation can continue.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - affects many events or system functionality.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - may cause replay operation to fail.
    /// Requires immediate attention.
    /// </summary>
    Critical = 3
}