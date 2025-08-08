namespace BuildingBlocks.Core.Events;

/// <summary>
/// Represents the result of publishing multiple integration events as a batch operation.
/// Provides comprehensive metrics and individual event results for observability and error handling.
/// </summary>
/// <param name="SuccessCount">Number of events successfully published</param>
/// <param name="FailureCount">Number of events that failed to publish</param>
/// <param name="SuccessfulEvents">Details of successfully published events</param>
/// <param name="FailedEvents">Details of events that failed to publish</param>
/// <param name="TotalLatency">Total time taken for the entire batch operation</param>
/// <param name="BatchId">Unique identifier for this batch operation, if supported by the broker</param>
/// <param name="StartedAtUtc">UTC timestamp when the batch operation started</param>
/// <param name="CompletedAtUtc">UTC timestamp when the batch operation completed</param>
public sealed record BatchPublishResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<EventPublishResult> SuccessfulEvents,
    IReadOnlyList<PublishFailure> FailedEvents,
    TimeSpan TotalLatency,
    string? BatchId = null,
    DateTime? StartedAtUtc = null,
    DateTime? CompletedAtUtc = null)
{
    /// <summary>
    /// Total number of events processed in this batch.
    /// </summary>
    public int TotalCount => SuccessCount + FailureCount;

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;

    /// <summary>
    /// Indicates whether the entire batch was successful.
    /// </summary>
    public bool IsFullSuccess => FailureCount == 0 && SuccessCount > 0;

    /// <summary>
    /// Indicates whether the entire batch failed.
    /// </summary>
    public bool IsFullFailure => SuccessCount == 0 && FailureCount > 0;

    /// <summary>
    /// Indicates whether the batch had partial success/failure.
    /// </summary>
    public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;

    /// <summary>
    /// Average latency per event in the batch.
    /// </summary>
    public TimeSpan AverageLatencyPerEvent => TotalCount > 0 
        ? TimeSpan.FromTicks(TotalLatency.Ticks / TotalCount) 
        : TimeSpan.Zero;

    /// <summary>
    /// Creates a BatchPublishResult for a fully successful batch.
    /// </summary>
    public static BatchPublishResult Success(
        IReadOnlyList<EventPublishResult> successfulEvents,
        TimeSpan totalLatency,
        string? batchId = null,
        DateTime? startedAtUtc = null,
        DateTime? completedAtUtc = null)
    {
        return new BatchPublishResult(
            SuccessCount: successfulEvents.Count,
            FailureCount: 0,
            SuccessfulEvents: successfulEvents,
            FailedEvents: Array.Empty<PublishFailure>(),
            TotalLatency: totalLatency,
            BatchId: batchId,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc);
    }

    /// <summary>
    /// Creates a BatchPublishResult for a fully failed batch.
    /// </summary>
    public static BatchPublishResult Failure(
        IReadOnlyList<PublishFailure> failedEvents,
        TimeSpan totalLatency,
        string? batchId = null,
        DateTime? startedAtUtc = null,
        DateTime? completedAtUtc = null)
    {
        return new BatchPublishResult(
            SuccessCount: 0,
            FailureCount: failedEvents.Count,
            SuccessfulEvents: Array.Empty<EventPublishResult>(),
            FailedEvents: failedEvents,
            TotalLatency: totalLatency,
            BatchId: batchId,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc);
    }

    /// <summary>
    /// Creates a BatchPublishResult for a mixed success/failure batch.
    /// </summary>
    public static BatchPublishResult Mixed(
        IReadOnlyList<EventPublishResult> successfulEvents,
        IReadOnlyList<PublishFailure> failedEvents,
        TimeSpan totalLatency,
        string? batchId = null,
        DateTime? startedAtUtc = null,
        DateTime? completedAtUtc = null)
    {
        return new BatchPublishResult(
            SuccessCount: successfulEvents.Count,
            FailureCount: failedEvents.Count,
            SuccessfulEvents: successfulEvents,
            FailedEvents: failedEvents,
            TotalLatency: totalLatency,
            BatchId: batchId,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc);
    }
}