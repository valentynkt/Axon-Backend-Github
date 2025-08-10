using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Abstraction for processing outbox messages in a centralized background service.
/// Transport-neutral interface that focuses on message processing orchestration,
/// health monitoring, and reliability patterns.
/// </summary>
public interface IOutboxMessageProcessor
{
    /// <summary>
    /// Process pending outbox messages with default batch size.
    /// Used by background services for continuous processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing processing statistics and outcome</returns>
    Task<Result<OutboxMessageProcessingResult>> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Process pending outbox messages with specific batch size.
    /// Allows for fine-tuned control over processing throughput.
    /// </summary>
    /// <param name="batchSize">Number of messages to process in this batch</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing processing statistics and outcome</returns>
    Task<Result<OutboxMessageProcessingResult>> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry processing of previously failed messages.
    /// Supports resilience patterns with configurable retry limits.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts for each message</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing retry processing statistics</returns>
    Task<Result<OutboxMessageProcessingResult>> RetryFailedAsync(int maxRetries = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get health status of the outbox message processor.
    /// Used by health check systems for monitoring and alerting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing health status information</returns>
    Task<Result<OutboxMessageProcessorHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Results from processing a batch of outbox messages.
/// Contains comprehensive statistics for monitoring and debugging.
/// </summary>
public sealed record OutboxMessageProcessingResult(
    int ProcessedCount,
    int SuccessfulCount,
    int FailedCount,
    TimeSpan ProcessingDuration,
    IReadOnlyList<OutboxMessageProcessingError> Errors)
{
    /// <summary>
    /// Indicates whether all processed messages were successful.
    /// </summary>
    public bool AllSuccessful => FailedCount == 0;

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => ProcessedCount == 0 ? 100.0 : (double)SuccessfulCount / ProcessedCount * 100.0;
}

/// <summary>
/// Health information for the outbox message processor.
/// Used for health checks and monitoring dashboards.
/// </summary>
public sealed record OutboxMessageProcessorHealth(
    bool IsRunning,
    DateTime? LastProcessingRun,
    TimeSpan? TimeSinceLastRun,
    int PendingMessageCount,
    int DeadLetterMessageCount,
    IReadOnlyList<string> RecentErrors)
{
    /// <summary>
    /// Indicates if the processor is healthy.
    /// Based on running state and recent error activity.
    /// </summary>
    public bool IsHealthy => IsRunning && RecentErrors.Count < 10;

    /// <summary>
    /// Indicates if the processor has been inactive for too long.
    /// </summary>
    public bool IsStale => TimeSinceLastRun?.TotalMinutes > 10;
}

/// <summary>
/// Represents an error that occurred during outbox message processing.
/// Contains contextual information for debugging and retry logic.
/// </summary>
public sealed record OutboxMessageProcessingError(
    Guid MessageId,
    string EventType,
    string ErrorMessage,
    string? StackTrace,
    DateTime OccurredAt,
    int RetryCount)
{
    /// <summary>
    /// Indicates whether this error is likely retriable.
    /// Based on error type and current retry count.
    /// </summary>
    public bool IsRetriable => RetryCount < 3 && !ErrorMessage.Contains("Validation", StringComparison.OrdinalIgnoreCase);
}