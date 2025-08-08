using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Interface for centralized outbox message processing service.
/// Created for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Provides reliable background processing with health monitoring capabilities for centralized OutboxMessage entities.
/// </summary>
public interface IOutboxMessageProcessor
{
    /// <summary>
    /// Process all pending outbox messages in batches.
    /// Used by background services for reliable event processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxMessageProcessingResult>> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Process pending outbox messages with specified batch size.
    /// Allows fine-grained control over processing batch sizes.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to process in this batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxMessageProcessingResult>> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed outbox messages that are ready for retry (based on exponential backoff).
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts before moving to dead letter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with retry processing statistics</returns>
    Task<Result<OutboxMessageProcessingResult>> RetryFailedAsync(int maxRetries = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get health status of the outbox message processor for health checks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result with processor status</returns>
    Task<Result<OutboxMessageProcessorHealth>> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the background processor if it's not already running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the background processor gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health information for the outbox message processor.
/// </summary>
public sealed record OutboxMessageProcessorHealth(
    bool IsRunning,
    DateTime? LastProcessingRun,
    TimeSpan? TimeSinceLastRun,
    int PendingMessageCount,
    int FailedMessageCount,
    IReadOnlyList<string> RecentErrors);

/// <summary>
/// Result of outbox message processing operations.
/// </summary>
public sealed record OutboxMessageProcessingResult(
    int ProcessedCount,
    int SuccessfulCount,
    int FailedCount,
    TimeSpan ProcessingDuration,
    IReadOnlyList<OutboxMessageProcessingError> Errors);

/// <summary>
/// Error information from outbox message processing.
/// </summary>
public sealed record OutboxMessageProcessingError(
    Guid MessageId,
    string EventType,
    string ErrorMessage,
    DateTime OccurredAt);