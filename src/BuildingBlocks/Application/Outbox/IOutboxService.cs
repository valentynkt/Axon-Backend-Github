using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Service for managing outbox entries and event publishing.
/// Provides reliable event storage and processing within database transactions.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Store domain events in the outbox within the current transaction.
    /// Events will be persisted atomically with business data.
    /// </summary>
    /// <param name="events">Domain events to store</param>
    /// <param name="transactionId">Transaction ID for correlation</param>
    /// <param name="traceId">W3C trace ID for distributed tracing</param>
    /// <param name="requestId">Request ID for correlation</param>
    /// <param name="metadata">Additional context metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with number of events stored</returns>
    Task<Result<int>> StoreEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        Guid transactionId,
        string? traceId = null,
        Guid? requestId = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process pending events for a specific transaction.
    /// Used for immediate processing after transaction commit.
    /// </summary>
    /// <param name="transactionId">Transaction ID to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxProcessingResult>> ProcessPendingEventsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process all pending events across all transactions.
    /// Used by background services for batch processing.
    /// </summary>
    /// <param name="batchSize">Maximum number of entries to process in one batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxProcessingResult>> ProcessAllPendingEventsAsync(
        int? batchSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get processing statistics for monitoring and health checks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Outbox processing statistics</returns>
    Task<Result<OutboxStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed events that are eligible for retry.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with retry statistics</returns>
    Task<Result<OutboxProcessingResult>> RetryFailedEventsAsync(
        int? maxEntries = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocess dead letter entries manually.
    /// </summary>
    /// <param name="entryIds">Specific entry IDs to reprocess, or null for all dead letters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with reprocessing statistics</returns>
    Task<Result<OutboxProcessingResult>> ReprocessDeadLetterEventsAsync(
        IReadOnlyList<Guid>? entryIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for outbox entry persistence operations.
/// Provides optimized queries for high-throughput event processing.
/// This interface belongs to the Application layer but implementations are in Infrastructure.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Add new outbox entries within the current transaction.
    /// </summary>
    /// <param name="events">Domain events to store</param>
    /// <param name="transactionId">Transaction ID for correlation</param>
    /// <param name="traceId">W3C trace ID for distributed tracing</param>
    /// <param name="requestId">Request ID for correlation</param>
    /// <param name="metadata">Additional context metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries created</returns>
    Task<int> AddEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        Guid transactionId,
        string? traceId = null,
        Guid? requestId = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending entries ready for processing, with optimized ordering.
    /// </summary>
    /// <param name="batchSize">Maximum number of entries to return</param>
    /// <param name="processingTimeoutMinutes">Minutes after which processing entries are considered stale</param>
    /// <param name="tenantId">Optional tenant filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entries ready for processing</returns>
    Task<IReadOnlyList<OutboxEventEntry>> GetPendingAsync(
        int batchSize = 100,
        int processingTimeoutMinutes = 30,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending entries for a specific transaction, ordered by creation time.
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pending entries for the transaction</returns>
    Task<IReadOnlyList<OutboxEventEntry>> GetPendingByTransactionAsync(
        Guid transactionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get failed entries that are ready for retry.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Failed entries ready for retry</returns>
    Task<IReadOnlyList<OutboxEventEntry>> GetFailedReadyForRetryAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dead letter entries for manual intervention.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dead letter entries</returns>
    Task<IReadOnlyList<OutboxEventEntry>> GetDeadLetterEntriesAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get outbox entries by IDs for batch operations.
    /// </summary>
    /// <param name="ids">Entry IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Found entries</returns>
    Task<IReadOnlyList<OutboxEventEntry>> GetByIdsAsync(
        IReadOnlyList<Guid> ids, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark entries as being processed.
    /// </summary>
    /// <param name="entries">Entries to mark as processing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessingAsync(
        IReadOnlyList<OutboxEventEntry> entries, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark entries as completed successfully.
    /// </summary>
    /// <param name="entries">Entries to mark as completed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsCompletedAsync(
        IReadOnlyList<OutboxEventEntry> entries, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark entries as failed with error information.
    /// </summary>
    /// <param name="failureInfos">Entry failure information</param>
    /// <param name="baseRetryDelayMinutes">Base delay for retry calculation</param>
    /// <param name="maxRetries">Maximum retry attempts before dead letter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        int baseRetryDelayMinutes,
        int maxRetries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks entries as failed with computed retry information from backoff policy.
    /// </summary>
    Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset dead letter entries to pending for manual retry.
    /// </summary>
    /// <param name="entryIds">Entry IDs to reset, or null for all dead letters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResetDeadLetterToPendingAsync(
        IReadOnlyList<Guid>? entryIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get processing statistics for monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Outbox statistics</returns>
    Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up completed entries older than the specified retention period.
    /// </summary>
    /// <param name="retentionPeriod">How long to keep completed entries</param>
    /// <param name="batchSize">Maximum number of entries to delete in one operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries cleaned up</returns>
    Task<int> CleanupCompletedEntriesAsync(
        TimeSpan retentionPeriod,
        int batchSize = 1000,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about outbox processing for monitoring and health checks.
/// </summary>
public sealed record OutboxStatistics(
    int PendingCount,
    int ProcessingCount,
    int CompletedCount,
    int FailedCount,
    int DeadLetterCount,
    DateTime? OldestPendingCreatedAt,
    DateTime? OldestProcessingStartedAt,
    TimeSpan? AverageProcessingTime);

/// <summary>
/// Result of outbox processing operations.
/// </summary>
public sealed record OutboxProcessingResult(
    int ProcessedCount,
    int SuccessfulCount,
    int FailedCount,
    int MovedToDeadLetterCount,
    TimeSpan ProcessingDuration,
    IReadOnlyList<OutboxProcessingError> Errors);

/// <summary>
/// Error information from outbox processing.
/// </summary>
public sealed record OutboxProcessingError(
    Guid EntryId,
    string EventType,
    string ErrorMessage,
    DateTime OccurredAt);