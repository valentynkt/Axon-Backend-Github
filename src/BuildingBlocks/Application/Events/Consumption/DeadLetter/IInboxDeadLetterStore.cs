namespace BuildingBlocks.Application.Events.Consumption.DeadLetter;

/// <summary>
/// Storage abstraction for dead-lettered integration events.
/// Provides persistence for failed messages that cannot be processed after exhausting retries.
/// Implementation is responsible for durable storage and optional retrieval/replay capabilities.
/// </summary>
public interface IInboxDeadLetterStore
{
    /// <summary>
    /// Adds a dead-lettered integration event to persistent storage.
    /// Should handle duplicate entries gracefully (idempotent operation).
    /// </summary>
    /// <param name="entry">Dead letter entry containing event and error details</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task AddAsync(InboxDeadLetterEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves dead-lettered entries for analysis or replay (optional capability).
    /// Implementation may return empty collection if retrieval is not supported.
    /// </summary>
    /// <param name="eventTypeName">Optional filter by event type name</param>
    /// <param name="fromDate">Optional filter for entries after this date</param>
    /// <param name="toDate">Optional filter for entries before this date</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of matching dead letter entries</returns>
    Task<IReadOnlyCollection<InboxDeadLetterEntry>> GetEntriesAsync(
        string? eventTypeName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of dead-lettered entries for monitoring and alerting (optional capability).
    /// Implementation may return 0 if counting is not supported.
    /// </summary>
    /// <param name="eventTypeName">Optional filter by event type name</param>
    /// <param name="fromDate">Optional filter for entries after this date</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Count of matching dead letter entries</returns>
    Task<long> GetCountAsync(
        string? eventTypeName = null,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default);
}