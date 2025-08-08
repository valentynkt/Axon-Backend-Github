using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application.Outbox;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Entity Framework implementation of the outbox repository.
/// Optimized for high-throughput event processing with efficient queries.
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

    public async Task AddAsync(IReadOnlyList<OutboxEntry> entries, CancellationToken cancellationToken = default)
    {
        if (!entries.Any()) return;

        try
        {
            _dbContext.Set<OutboxEntry>().AddRange(entries);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Added {EntryCount} outbox entries to database", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {EntryCount} outbox entries", entries.Count);
            throw;
        }
    }

    public async Task UpdateAsync(OutboxEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Set<OutboxEntry>().Update(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Updated outbox entry {EntryId} status to {Status}", entry.Id, entry.Status);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict when updating outbox entry {EntryId}. Entry may have been processed by another instance",
                entry.Id);
            
            // Reload the entry to get current state
            await _dbContext.Entry(entry).ReloadAsync(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update outbox entry {EntryId}", entry.Id);
            throw;
        }
    }

    public async Task UpdateBatchAsync(IReadOnlyList<OutboxEntry> entries, CancellationToken cancellationToken = default)
    {
        if (!entries.Any()) return;

        try
        {
            _dbContext.Set<OutboxEntry>().UpdateRange(entries);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Updated {EntryCount} outbox entries in batch", entries.Count);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict when updating {EntryCount} outbox entries in batch. Some entries may have been processed by another instance",
                entries.Count);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update {EntryCount} outbox entries in batch", entries.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetPendingByTransactionAsync(
        Guid transactionId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.TransactionId == transactionId && e.Status == OutboxEntryStatus.Pending)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {EntryCount} pending outbox entries for transaction {TransactionId}",
                entries.Count, transactionId);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get pending outbox entries for transaction {TransactionId}",
                transactionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(
        int batchSize = 100,
        int processingTimeoutMinutes = 30,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-processingTimeoutMinutes);
            
            var query = _dbContext.Set<OutboxEntry>()
                .Where(e => 
                    (e.Status == OutboxEntryStatus.Pending) ||
                    (e.Status == OutboxEntryStatus.Processing && e.ProcessingStartedAt < cutoffTime) ||
                    (e.Status == OutboxEntryStatus.Failed && (e.NextRetryAt == null || e.NextRetryAt <= DateTime.UtcNow)));

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                query = query.Where(e => e.TenantId == tenantId);
            }

            var entries = await query
                .OrderBy(e => e.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {EntryCount} pending outbox entries ready for processing (tenant: {TenantId})",
                entries.Count, tenantId ?? "all");

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending outbox entries");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetFailedReadyForRetryAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            var entries = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.Failed && 
                           (e.NextRetryAt == null || e.NextRetryAt <= now))
                .OrderBy(e => e.CreatedAt)
                .Take(maxEntries)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {EntryCount} failed outbox entries ready for retry", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed outbox entries ready for retry");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetDeadLetterEntriesAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.DeadLetter)
                .OrderBy(e => e.CreatedAt)
                .Take(maxEntries)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {EntryCount} dead letter outbox entries", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dead letter outbox entries");
            throw;
        }
    }

    public async Task<OutboxEntry?> GetByIdAsync(OutboxEntryId id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Set<OutboxEntry>()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox entry by ID {EntryId}", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetByIdsAsync(
        IReadOnlyList<OutboxEntryId> ids, 
        CancellationToken cancellationToken = default)
    {
        if (!ids.Any()) return [];

        try
        {
            var entries = await _dbContext.Set<OutboxEntry>()
                .Where(e => ids.Contains(e.Id))
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {EntryCount} outbox entries by IDs", entries.Count);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox entries by IDs");
            throw;
        }
    }

    public async Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            // Get counts by status efficiently in a single query
            var statusCounts = await _dbContext.Set<OutboxEntry>()
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Pending)?.Count ?? 0;
            var processingCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Processing)?.Count ?? 0;
            var completedCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Completed)?.Count ?? 0;
            var failedCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.Failed)?.Count ?? 0;
            var deadLetterCount = statusCounts.FirstOrDefault(s => s.Status == OutboxEntryStatus.DeadLetter)?.Count ?? 0;

            // Get oldest pending and processing timestamps
            var oldestPending = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.Pending)
                .OrderBy(e => e.CreatedAt)
                .Select(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var oldestProcessing = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.Processing)
                .OrderBy(e => e.ProcessingStartedAt)
                .Select(e => e.ProcessingStartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Calculate average processing time for completed entries (last 1000)
            var completedEntries = await _dbContext.Set<OutboxEntry>()
                .Where(e => e.Status == OutboxEntryStatus.Completed && 
                           e.ProcessedAt != null)
                .OrderByDescending(e => e.ProcessedAt)
                .Select(e => new { e.CreatedAt, ProcessedAt = e.ProcessedAt!.Value })
                .Take(1000)
                .ToListAsync(cancellationToken);

            TimeSpan? averageProcessingTime = null;
            if (completedEntries.Count != 0)
            {
                var totalProcessingTime = completedEntries
                    .Sum(e => (e.ProcessedAt - e.CreatedAt).TotalMilliseconds);
                averageProcessingTime = TimeSpan.FromMilliseconds(totalProcessingTime / completedEntries.Count);
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

            _logger.LogDebug(
                "Outbox statistics: Pending={PendingCount}, Processing={ProcessingCount}, Completed={CompletedCount}, Failed={FailedCount}, DeadLetter={DeadLetterCount}",
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
            
            // Use ExecuteDeleteAsync for better performance (EF Core 7+)
            // For large datasets, this is much more efficient than loading and deleting entities
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
                // Fallback for older EF Core versions or providers that don't support ExecuteDeleteAsync
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
                _logger.LogInformation(
                    "Cleaned up {DeletedCount} completed outbox entries older than {RetentionPeriod}",
                    deletedCount, retentionPeriod);
            }
            else
            {
                _logger.LogDebug("No completed outbox entries found for cleanup");
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to cleanup completed outbox entries older than {RetentionPeriod}",
                retentionPeriod);
            throw;
        }
    }
}