namespace BuildingBlocks.Application.Events.Consumption.Retry;

/// <summary>
/// Decision result from evaluating retry policy for a failed integration event.
/// Encapsulates the retry delay, whether to retry, and if moved to dead letter.
/// </summary>
/// <param name="Delay">Time to wait before next retry attempt</param>
/// <param name="ShouldRetry">Whether the message should be retried</param>
/// <param name="MovedToDeadLetter">Whether the message was moved to dead letter due to exhausted retries</param>
public sealed record InboxRetryDecision(
    TimeSpan Delay,
    bool ShouldRetry,
    bool MovedToDeadLetter
)
{
    /// <summary>
    /// Creates a decision to retry with the specified delay.
    /// </summary>
    /// <param name="delay">Time to wait before retry</param>
    /// <returns>Retry decision with specified delay</returns>
    public static InboxRetryDecision Retry(TimeSpan delay) => new(delay, ShouldRetry: true, MovedToDeadLetter: false);

    /// <summary>
    /// Creates a decision to move the message to dead letter (no more retries).
    /// </summary>
    /// <returns>Dead letter decision</returns>
    public static InboxRetryDecision DeadLetter() => new(TimeSpan.Zero, ShouldRetry: false, MovedToDeadLetter: true);

    /// <summary>
    /// Creates a decision to not retry (permanent failure, immediate dead letter).
    /// </summary>
    /// <returns>No retry decision</returns>
    public static InboxRetryDecision NoRetry() => new(TimeSpan.Zero, ShouldRetry: false, MovedToDeadLetter: false);
}