using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Events.Consumption.DeadLetter;

/// <summary>
/// No-operation implementation of dead letter store that discards entries.
/// Used as default implementation when dead letter storage is not configured.
/// Logs dead letter entries for observability but does not persist them.
/// </summary>
public sealed class NoOpInboxDeadLetterStore : IInboxDeadLetterStore
{
    private readonly ILogger<NoOpInboxDeadLetterStore> _logger;

    public NoOpInboxDeadLetterStore(ILogger<NoOpInboxDeadLetterStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task AddAsync(InboxDeadLetterEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _logger.LogWarning(
            "Dead letter entry discarded - no persistent store configured. " +
            "IdempotencyKey: {IdempotencyKey}, EventType: {EventType}, Error: {ErrorMessage}, Attempts: {AttemptCount}",
            entry.IdempotencyKey, entry.EventTypeName, entry.ErrorMessage, entry.AttemptCount);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<InboxDeadLetterEntry>> GetEntriesAsync(
        string? eventTypeName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetEntriesAsync called on NoOpInboxDeadLetterStore - returning empty collection");
        return Task.FromResult<IReadOnlyCollection<InboxDeadLetterEntry>>(Array.Empty<InboxDeadLetterEntry>());
    }

    public Task<long> GetCountAsync(
        string? eventTypeName = null,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetCountAsync called on NoOpInboxDeadLetterStore - returning 0");
        return Task.FromResult(0L);
    }
}