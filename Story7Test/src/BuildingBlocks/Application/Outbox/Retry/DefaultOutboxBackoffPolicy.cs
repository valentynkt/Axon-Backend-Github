namespace BuildingBlocks.Application.Outbox.Retry;

/// <summary>
/// Default exponential backoff policy with jitter and configurable caps.
/// Provides deterministic, production-ready retry behavior.
/// </summary>
public sealed class DefaultOutboxBackoffPolicy : IOutboxBackoffPolicy
{
    public OutboxBackoffDecision Compute(
        int attempt,
        DateTime failedAtUtc,
        OutboxOptions options,
        string? errorCategory = null)
    {
        // Permanent error shortcut - move to dead letter immediately
        if (string.Equals(errorCategory, "Permanent", StringComparison.OrdinalIgnoreCase))
            return new(true, null);

        // Dead-letter threshold - exceeded max retries
        if (attempt + 1 >= options.MaxRetries)
            return new(true, null);

        // Exponential backoff calculation
        var baseDelay = TimeSpan.FromMinutes(options.BaseRetryDelayMinutes);
        var rawDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));

        // Cap at max delay
        var capped = rawDelay > options.MaxRetryDelay ? options.MaxRetryDelay : rawDelay;

        // Apply jitter if enabled
        var finalDelay = options.UseJitter && options.JitterRatio > 0
            ? ApplyJitter(capped, options.JitterRatio)
            : capped;

        return new(false, failedAtUtc + finalDelay);
    }

    /// <summary>
    /// Applies full jitter to the delay using the configured ratio.
    /// Jitter is applied as +/- ratio% of the original delay.
    /// </summary>
    private static TimeSpan ApplyJitter(TimeSpan delay, double ratio)
    {
        var maxDriftMs = delay.TotalMilliseconds * ratio;
        var drift = Random.Shared.NextDouble() * (maxDriftMs * 2) - maxDriftMs; // [-max, +max]
        var jittered = delay + TimeSpan.FromMilliseconds(drift);
        
        // Ensure non-negative result
        return jittered < TimeSpan.Zero ? TimeSpan.Zero : jittered;
    }
}