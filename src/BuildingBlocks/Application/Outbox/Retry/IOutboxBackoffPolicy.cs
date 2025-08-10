namespace BuildingBlocks.Application.Outbox.Retry;

/// <summary>
/// Policy for computing retry backoff delays and dead-letter decisions.
/// Provides deterministic, configurable retry behavior for outbox processing.
/// </summary>
public interface IOutboxBackoffPolicy
{
    /// <summary>
    /// Computes the backoff decision for a failed outbox entry.
    /// </summary>
    /// <param name="attempt">Current retry attempt (0-based)</param>
    /// <param name="failedAtUtc">When the failure occurred</param>
    /// <param name="options">Outbox configuration options</param>
    /// <param name="errorCategory">Optional error categorization (e.g., "Transient", "Permanent")</param>
    /// <returns>Decision on whether to retry or move to dead letter</returns>
    OutboxBackoffDecision Compute(
        int attempt,
        DateTime failedAtUtc,
        OutboxOptions options,
        string? errorCategory = null);
}

/// <summary>
/// Result of backoff policy computation.
/// </summary>
/// <param name="MoveToDeadLetter">True if the entry should be moved to dead letter queue</param>
/// <param name="NextRetryAtUtc">When the next retry should occur (null if moving to dead letter)</param>
public sealed record OutboxBackoffDecision(
    bool MoveToDeadLetter,
    DateTime? NextRetryAtUtc
);