using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Application-layer port for outbox persistence operations.
/// Implemented in Infrastructure (e.g., EF Core).
/// </summary>
public interface IOutboxRepository
{
    /// <summary>Add new outbox entries within the current transaction.</summary>
    Task<int> AddEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        Guid transactionId,
        string? traceId = null,
        Guid? requestId = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get pending entries ready for processing.</summary>
    Task<IReadOnlyList<OutboxEventEntry>> GetPendingAsync(
        int batchSize = 100,
        int processingTimeoutMinutes = 30,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get pending entries for a specific transaction, ordered by creation time.</summary>
    Task<IReadOnlyList<OutboxEventEntry>> GetPendingByTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>Get failed entries that are ready for retry.</summary>
    Task<IReadOnlyList<OutboxEventEntry>> GetFailedReadyForRetryAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Get dead letter entries for manual intervention.</summary>
    Task<IReadOnlyList<OutboxEventEntry>> GetDeadLetterEntriesAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Get outbox entries by IDs for batch operations.</summary>
    Task<IReadOnlyList<OutboxEventEntry>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Mark entries as being processed.</summary>
    Task MarkAsProcessingAsync(
        IReadOnlyList<OutboxEventEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Mark entries as completed successfully.</summary>
    Task MarkAsCompletedAsync(
        IReadOnlyList<OutboxEventEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks entries as failed with legacy retry parameters.
    /// Prefer the overload that accepts computed <see cref="OutboxFailureInfo"/>.
    /// </summary>
    Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        int baseRetryDelayMinutes,
        int maxRetries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks entries as failed with computed retry information (recommended).
    /// </summary>
    Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        CancellationToken cancellationToken = default);

    /// <summary>Reset dead letter entries to pending for manual retry.</summary>
    Task ResetDeadLetterToPendingAsync(
        IReadOnlyList<Guid>? entryIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get processing statistics for monitoring dashboards/health checks.</summary>
    Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Cleanup completed entries older than the specified retention period.</summary>
    Task<int> CleanupCompletedEntriesAsync(
        TimeSpan retentionPeriod,
        int batchSize = 1000,
        CancellationToken cancellationToken = default);
}

/// <summary>Aggregate statistics for the outbox.</summary>
public sealed record OutboxStatistics(
    int PendingCount,
    int ProcessingCount,
    int CompletedCount,
    int FailedCount,
    int DeadLetterCount,
    DateTime? OldestPendingCreatedAt,
    DateTime? OldestProcessingStartedAt,
    TimeSpan? AverageProcessingTime);
