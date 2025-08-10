namespace BuildingBlocks.Application.Outbox.Monitoring;

/// <summary>
/// No-operation implementation of IOutboxMetrics.
/// Used as default when no monitoring is configured.
/// </summary>
public sealed class NoOpOutboxMetrics : IOutboxMetrics
{
    public void RecordFetched(int count) { }

    public void RecordProcessed(int processed, int succeeded, int failed, int deadLettered, TimeSpan duration) { }

    public void RecordRetryScheduled(TimeSpan delay) { }

    public void RecordPermanentFailure() { }

    public void RecordCleanup(int cleaned) { }

    public void RecordProcessorLoopError() { }

    // Inbound integration event metrics (Story 9)
    public void RecordInboundReceived() { }
    public void RecordInboundHandled() { }
    public void RecordInboundDuplicate() { }
    public void RecordInboundHandlerFailed() { }

    // Inbound retry and dead letter metrics (Story 10)
    public void RecordInboundRetryScheduled(TimeSpan delay) { }
    public void RecordInboundMovedToDeadLetter() { }
    public void RecordInboundPermanentFailure() { }
}