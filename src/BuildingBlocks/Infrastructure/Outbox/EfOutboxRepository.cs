using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BuildingBlocks.Application.Outbox;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Entity Framework implementation of the outbox repository.
/// Converts between Infrastructure entities (OutboxEntry) and Application DTOs (OutboxEventEntry).
/// </summary>
public sealed class EfOutboxRepository : IOutboxRepository
{
    private readonly DbContext _dbContext;
    private readonly ILogger<EfOutboxRepository> _logger;

    public EfOutboxRepository(DbContext dbContext, ILogger<EfOutboxRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> AddEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        Guid transactionId,
        string? traceId = null,
        Guid? requestId = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!events.Any()) return 0;

        try
        {
            var entries = events.Select(domainEvent => new OutboxEntry(
                id: OutboxEntryId.New(),
                transactionId: transactionId,
                eventType: domainEvent.GetType().AssemblyQualifiedName!,
                eventData: JsonSerializer.Serialize((object)domainEvent, domainEvent.GetType()),
                traceId: traceId,
                requestId: requestId,
                tenantId: null, // plug tenant if/when needed
                metadata: metadata != null ? JsonSerializer.Serialize(metadata) : null
            )).ToList();

            _dbContext.Set<OutboxEntry>().AddRange(entries);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Added {EventCount} domain events to outbox for transaction {TransactionId}",
                entries.Count, transactionId);

            return entries.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {EventCount} events to outbox", events.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEventEntry>> GetPendingAsync(
        int batchSize = 100,
        int processingTimeoutMinutes = 30,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-processingTimeoutMinutes);

            var query = _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e =>
                    e.Status == OutboxEntryStatus.Pending ||
                    (e.Status == OutboxEntryStatus.Processing && e.ProcessingStartedAt < cutoffTime) ||
                    (e.Status == OutboxEntryStatus.Failed && (e.NextRetryAt == null || e.NextRetryAt <= DateTime.UtcNow)));

            if (!string.IsNullOrWhiteSpace(tenantId))
                query = query.Where(e => e.TenantId == tenantId);

            var entities = await query
                .OrderBy(e => e.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            var entries = entities.Select(ToOutboxEventEntry).ToList();

            _logger.LogDebug("Found {EntryCount} pending outbox entries ready for processing", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending outbox entries");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEventEntry>> GetPendingByTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.TransactionId == transactionId && e.Status == OutboxEntryStatus.Pending)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(cancellationToken);

            var entries = entities.Select(ToOutboxEventEntry).ToList();

            _logger.LogDebug("Found {EntryCount} pending entries for transaction {TransactionId}",
                entries.Count, transactionId);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending entries for transaction {TransactionId}", transactionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEventEntry>> GetFailedReadyForRetryAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            var entities = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.Status == OutboxEntryStatus.Failed &&
                            (e.NextRetryAt == null || e.NextRetryAt <= now))
                .OrderBy(e => e.CreatedAt)
                .Take(maxEntries)
                .ToListAsync(cancellationToken);

            var entries = entities.Select(ToOutboxEventEntry).ToList();

            _logger.LogDebug("Found {EntryCount} failed entries ready for retry", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed entries ready for retry");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEventEntry>> GetDeadLetterEntriesAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.Status == OutboxEntryStatus.DeadLetter)
                .OrderBy(e => e.CreatedAt)
                .Take(maxEntries)
                .ToListAsync(cancellationToken);

            var entries = entities.Select(ToOutboxEventEntry).ToList();

            _logger.LogDebug("Found {EntryCount} dead letter entries", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dead letter entries");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEventEntry>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (!ids.Any()) return [];

        try
        {
            var entities = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => ids.Contains(e.Id.Value))
                .ToListAsync(cancellationToken);

            var entries = entities.Select(ToOutboxEventEntry).ToList();

            _logger.LogDebug("Retrieved {EntryCount} entries by IDs", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries by IDs");
            throw;
        }
    }

    public async Task MarkAsProcessingAsync(
        IReadOnlyList<OutboxEventEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (!entries.Any()) return;

        try
        {
            var ids = entries.Select(e => e.Id).ToList();
            var entities = await _dbContext.Set<OutboxEntry>()
                .Where(e => ids.Contains(e.Id.Value))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.MarkAsProcessing();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Marked {EntryCount} entries as processing", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark {EntryCount} entries as processing", entries.Count);
            throw;
        }
    }

    public async Task MarkAsCompletedAsync(
        IReadOnlyList<OutboxEventEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (!entries.Any()) return;

        try
        {
            var ids = entries.Select(e => e.Id).ToList();
            var entities = await _dbContext.Set<OutboxEntry>()
                .Where(e => ids.Contains(e.Id.Value))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.MarkAsCompleted();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Marked {EntryCount} entries as completed", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark {EntryCount} entries as completed", entries.Count);
            throw;
        }
    }

    public async Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        int baseRetryDelayMinutes,
        int maxRetries,
        CancellationToken cancellationToken = default)
    {
        // OutboxService already computed backoff & dead-letter; ignore legacy params.
        await MarkAsFailedAsync(failureInfos, cancellationToken);
    }

    public async Task MarkAsFailedAsync(
        IReadOnlyList<OutboxFailureInfo> failureInfos,
        CancellationToken cancellationToken = default)
    {
        if (!failureInfos.Any()) return;

        try
        {
            var ids = failureInfos.Select(f => f.EntryId).ToList();
            var entities = await _dbContext.Set<OutboxEntry>()
                .Where(e => ids.Contains(e.Id.Value))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                var failure = failureInfos.First(f => f.EntryId == entity.Id.Value);

                // record error
                entity.LastError = failure.ErrorMessage.Length > 2000
                    ? failure.ErrorMessage[..2000]
                    : failure.ErrorMessage;

                if (failure.MoveToDeadLetter)
                {
                    entity.MoveToDeadLetter(entity.LastError);
                }
                else
                {
                    // treat as failed with computed next retry; increment retry count here
                    entity.Status = OutboxEntryStatus.Failed;
                    entity.RetryCount = entity.RetryCount + 1;
                    entity.ProcessingStartedAt = null;
                    entity.NextRetryAt = failure.NextRetryAtUtc;
                    entity.Version++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Marked {EntryCount} entries as failed", failureInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark {EntryCount} entries as failed", failureInfos.Count);
            throw;
        }
    }

    public async Task ResetDeadLetterToPendingAsync(
        IReadOnlyList<Guid>? entryIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.DeadLetter);

            if (entryIds is { Count: > 0 })
                query = query.Where(e => entryIds.Contains(e.Id.Value));

            var entities = await query.ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.ResetToPending();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Reset {EntryCount} dead letter entries to pending", entities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset dead letter entries to pending");
            throw;
        }
    }

    public async Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statusCounts = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Pending)?.Count ?? 0;
            var processingCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Processing)?.Count ?? 0;
            var completedCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Completed)?.Count ?? 0;
            var failedCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Failed)?.Count ?? 0;
            var deadLetterCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.DeadLetter)?.Count ?? 0;

            var oldestPending = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.Status == OutboxEntryStatus.Pending)
                .OrderBy(e => e.CreatedAt)
                .Select(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var oldestProcessing = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.Status == OutboxEntryStatus.Processing)
                .OrderBy(e => e.ProcessingStartedAt)
                .Select(e => e.ProcessingStartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var completedEntries = await _dbContext.Set<OutboxEntry>()
                .AsNoTracking()
                .Where(e => e.Status == OutboxEntryStatus.Completed && e.ProcessedAt != null)
                .OrderByDescending(e => e.ProcessedAt)
                .Select(e => new { e.CreatedAt, ProcessedAt = e.ProcessedAt!.Value })
                .Take(1000)
                .ToListAsync(cancellationToken);

            TimeSpan? averageProcessingTime = null;
            if (completedEntries.Count != 0)
            {
                var totalMs = completedEntries.Sum(e => (e.ProcessedAt - e.CreatedAt).TotalMilliseconds);
                averageProcessingTime = TimeSpan.FromMilliseconds(totalMs / completedEntries.Count);
            }

            var statistics = new OutboxStatistics(
                PendingCount: pendingCount,
                ProcessingCount: processingCount,
                CompletedCount: completedCount,
                FailedCount: failedCount,
                DeadLetterCount: deadLetterCount,
                OldestPendingCreatedAt: oldestPending == default ? null : oldestPending,
                OldestProcessingStartedAt: oldestProcessing,
                AverageProcessingTime: averageProcessingTime);

            _logger.LogDebug("Outbox statistics: Pending={PendingCount}, Processing={ProcessingCount}, Completed={CompletedCount}, Failed={FailedCount}, DeadLetter={DeadLetterCount}",
                pendingCount, processingCount, completedCount, failedCount, deadLetterCount);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox statistics");
            throw;
        }
    }

    public async Task<int> CleanupCompletedEntriesAsync(
        TimeSpan retentionPeriod,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - retentionPeriod;

            int deletedCount;
            try
            {
                deletedCount = await _dbContext.Set<OutboxEntry>()
                    .Where(e => e.Status == OutboxEntryStatus.Completed &&
                                e.ProcessedAt != null &&
                                e.ProcessedAt < cutoffDate)
                    .Take(batchSize)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (NotSupportedException)
            {
                var entriesToDelete = await _dbContext.Set<OutboxEntry>()
                    .Where(e => e.Status == OutboxEntryStatus.Completed &&
                                e.ProcessedAt != null &&
                                e.ProcessedAt < cutoffDate)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (entriesToDelete.Count != 0)
                {
                    _dbContext.Set<OutboxEntry>().RemoveRange(entriesToDelete);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    deletedCount = entriesToDelete.Count;
                }
                else
                {
                    deletedCount = 0;
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {DeletedCount} completed outbox entries older than {RetentionPeriod}",
                    deletedCount, retentionPeriod);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup completed outbox entries older than {RetentionPeriod}", retentionPeriod);
            throw;
        }
    }

    private static OutboxEventEntry ToOutboxEventEntry(OutboxEntry entity)
        => new(
            Id: entity.Id.Value,
            TransactionId: entity.TransactionId,
            EventType: entity.EventType,
            EventData: entity.EventData,
            Status: ToOutboxEventStatus(entity.Status),
            RetryCount: entity.RetryCount,
            CreatedAt: entity.CreatedAt,
            ProcessingStartedAt: entity.ProcessingStartedAt,
            ProcessedAt: entity.ProcessedAt,
            LastError: entity.LastError,
            NextRetryAt: entity.NextRetryAt,
            TraceId: entity.TraceId,
            RequestId: entity.RequestId,
            TenantId: entity.TenantId,
            Metadata: entity.Metadata);

    private static OutboxEventStatus ToOutboxEventStatus(OutboxEntryStatus infraStatus) => infraStatus switch
    {
        OutboxEntryStatus.Pending => OutboxEventStatus.Pending,
        OutboxEntryStatus.Processing => OutboxEventStatus.Processing,
        OutboxEntryStatus.Completed => OutboxEventStatus.Completed,
        OutboxEntryStatus.Failed => OutboxEventStatus.Failed,
        OutboxEntryStatus.DeadLetter => OutboxEventStatus.DeadLetter,
        _ => throw new ArgumentOutOfRangeException(nameof(infraStatus), infraStatus, "Unknown outbox status")
    };
}
