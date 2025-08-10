namespace BuildingBlocks.Application.Events.Consumption.Retry;

/// <summary>
/// Policy for computing retry delays and determining when to move messages to dead letter.
/// Implements exponential backoff with jitter and configurable limits.
/// </summary>
public interface IInboxRetryPolicy
{
    /// <summary>
    /// Computes retry decision based on attempt count, timing, and policy configuration.
    /// </summary>
    /// <param name="nextAttempt">The next attempt number (1-based, where 1 is first retry)</param>
    /// <param name="producedAtUtc">When the original message was produced</param>
    /// <param name="nowUtc">Current UTC time</param>
    /// <param name="options">Inbox configuration options</param>
    /// <returns>Decision on whether to retry, delay duration, or move to dead letter</returns>
    InboxRetryDecision Compute(int nextAttempt, DateTime producedAtUtc, DateTime nowUtc, InboxOptions options);
}