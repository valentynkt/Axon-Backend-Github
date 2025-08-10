namespace BuildingBlocks.Application.Outbox.Monitoring;

/// <summary>
/// Abstraction for outbox processing metrics.
/// Implementation should bind to OTEL/Prometheus/other monitoring systems.
/// </summary>
public interface IOutboxMetrics
{
    /// <summary>
    /// Records the number of entries fetched for processing.
    /// </summary>
    /// <param name="count">Number of entries fetched</param>
    void RecordFetched(int count);

    /// <summary>
    /// Records processing batch results.
    /// </summary>
    /// <param name="processed">Total entries processed</param>
    /// <param name="succeeded">Number that succeeded</param>
    /// <param name="failed">Number that failed</param>
    /// <param name="deadLettered">Number moved to dead letter</param>
    /// <param name="duration">Processing duration</param>
    void RecordProcessed(int processed, int succeeded, int failed, int deadLettered, TimeSpan duration);

    /// <summary>
    /// Records when a retry is scheduled with the computed delay.
    /// </summary>
    /// <param name="delay">Delay until next retry</param>
    void RecordRetryScheduled(TimeSpan delay);

    /// <summary>
    /// Records when an entry is permanently failed (moved to dead letter).
    /// </summary>
    void RecordPermanentFailure();

    /// <summary>
    /// Records cleanup operation results.
    /// </summary>
    /// <param name="cleaned">Number of entries cleaned up</param>
    void RecordCleanup(int cleaned);

    /// <summary>
    /// Records processor loop errors (infrastructure failures).
    /// </summary>
    void RecordProcessorLoopError();
}