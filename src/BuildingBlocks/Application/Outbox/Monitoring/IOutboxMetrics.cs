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

    // Inbound integration event metrics (Story 9)

    /// <summary>
    /// Records when an inbound integration event is received for processing.
    /// </summary>
    void RecordInboundReceived();

    /// <summary>
    /// Records when an inbound integration event is successfully handled.
    /// </summary>
    void RecordInboundHandled();

    /// <summary>
    /// Records when an inbound integration event is skipped due to duplicate IdempotencyKey.
    /// </summary>
    void RecordInboundDuplicate();

    /// <summary>
    /// Records when an inbound integration event handler fails during execution.
    /// </summary>
    void RecordInboundHandlerFailed();

    // Inbound retry and dead letter metrics (Story 10)

    /// <summary>
    /// Records when an inbound retry is scheduled with the computed delay.
    /// </summary>
    /// <param name="delay">Delay until next retry</param>
    void RecordInboundRetryScheduled(TimeSpan delay);

    /// <summary>
    /// Records when an inbound message is moved to dead letter store.
    /// </summary>
    void RecordInboundMovedToDeadLetter();

    /// <summary>
    /// Records when an inbound message fails with a permanent error (immediate dead letter).
    /// </summary>
    void RecordInboundPermanentFailure();
}