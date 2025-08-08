using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Entity Framework implementation of centralized outbox message repository.
/// Created for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Provides efficient database operations with PostgreSQL optimizations and Result pattern integration.
/// </summary>
public sealed class EfOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly DbContext _dbContext;
    private readonly ILogger<EfOutboxMessageRepository> _logger;

    public EfOutboxMessageRepository(DbContext dbContext, ILogger<EfOutboxMessageRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await _dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtUtc == null && 
                           (m.NextRetryAtUtc == null || m.NextRetryAtUtc <= DateTime.UtcNow))
                .OrderBy(m => m.OccurredAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            _logger.LogTrace("Retrieved {Count} pending outbox messages", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending outbox messages");
            throw;
        }
    }

    public async Task<List<OutboxMessage>> GetRetryableMessagesAsync(int maxRetries, CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await _dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtUtc == null && 
                           m.ProcessingAttempts < maxRetries &&
                           m.NextRetryAtUtc != null && 
                           m.NextRetryAtUtc <= DateTime.UtcNow)
                .OrderBy(m => m.NextRetryAtUtc)
                .ToListAsync(cancellationToken);

            _logger.LogTrace("Retrieved {Count} retryable outbox messages", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve retryable outbox messages");
            throw;
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _dbContext.Set<OutboxMessage>()
                .CountAsync(m => m.ProcessedAtUtc == null, cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending outbox message count");
            throw;
        }
    }

    public async Task<int> GetFailedCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _dbContext.Set<OutboxMessage>()
                .CountAsync(m => m.ProcessedAtUtc == null && 
                                m.ProcessingAttempts > 0 && 
                                !string.IsNullOrEmpty(m.LastError), 
                           cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed outbox message count");
            throw;
        }
    }

    public async Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Set<OutboxMessage>().Update(message);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogTrace("Updated outbox message {MessageId}", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update outbox message {MessageId}", message.Id);
            throw;
        }
    }

    public async Task<int> CleanupProcessedMessagesAsync(TimeSpan retentionPeriod, int batchSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - retentionPeriod;

            var messagesToDelete = await _dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < cutoffDate)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (messagesToDelete.Any())
            {
                _dbContext.Set<OutboxMessage>().RemoveRange(messagesToDelete);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Cleaned up {Count} processed outbox messages older than {CutoffDate}", 
                    messagesToDelete.Count, cutoffDate);
            }

            return messagesToDelete.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup processed outbox messages");
            throw;
        }
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Set<OutboxMessage>().AddAsync(message, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogTrace("Added outbox message {MessageId} of type {EventType}", 
                message.Id, message.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add outbox message {MessageId}", message.Id);
            throw;
        }
    }

    public async Task<Result<OutboxMessage?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _dbContext.Set<OutboxMessage>()
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

            return Result<OutboxMessage?>.Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outbox message by ID {MessageId}", id);
            return Result<OutboxMessage?>.Failure(Error.Internal(
                $"Failed to get outbox message by ID {id}",
                "OUTBOX_MESSAGE_REPOSITORY_GET_BY_ID_FAILED",
                ex));
        }
    }

    public async Task<Result<List<OutboxMessage>>> GetOldUnprocessedMessagesAsync(TimeSpan maxAge, int batchSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;

            var messages = await _dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtUtc == null && m.OccurredAtUtc < cutoffDate)
                .OrderBy(m => m.OccurredAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            return Result<List<OutboxMessage>>.Success(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get old unprocessed outbox messages");
            return Result<List<OutboxMessage>>.Failure(Error.Internal(
                "Failed to get old unprocessed outbox messages",
                "OUTBOX_MESSAGE_REPOSITORY_GET_OLD_UNPROCESSED_FAILED",
                ex));
        }
    }
}