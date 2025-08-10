namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// Configuration options for inbox retry and dead letter policies.
/// Controls retry behavior, delays, and dead letter handling for inbound integration events.
/// </summary>
public sealed class InboxOptions
{
    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter.
    /// Default is 5 attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay for first retry attempt.
    /// Subsequent attempts use exponential backoff: BaseDelay * 2^attempt.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum delay between retry attempts to prevent excessive wait times.
    /// Default is 30 minutes.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether to add random jitter to retry delays to prevent thundering herd.
    /// Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Jitter ratio as a percentage of computed delay (0.0 to 1.0).
    /// Applied as ±JitterRatio% of the computed delay.
    /// Default is 0.2 (±20%).
    /// </summary>
    public double JitterRatio { get; set; } = 0.2;

    /// <summary>
    /// Whether to treat unclassified errors as permanent failures.
    /// When true, unclassified errors go directly to dead letter.
    /// When false, unclassified errors are treated as transient and retried.
    /// Default is false (retry unclassified errors).
    /// </summary>
    public bool UnclassifiedIsPermanent { get; set; } = false;

    /// <summary>
    /// Maximum age of a message before it's considered stale and moved to dead letter.
    /// Helps prevent processing very old messages that might be irrelevant.
    /// Default is 7 days.
    /// </summary>
    public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Validates the configuration options for consistency and reasonable values.
    /// </summary>
    /// <returns>Validation error message if invalid, null if valid</returns>
    public string? Validate()
    {
        if (MaxAttempts < 0)
            return "MaxAttempts must be non-negative";

        if (BaseDelay < TimeSpan.Zero)
            return "BaseDelay must be non-negative";

        if (MaxRetryDelay < BaseDelay)
            return "MaxRetryDelay must be greater than or equal to BaseDelay";

        if (JitterRatio < 0.0 || JitterRatio > 1.0)
            return "JitterRatio must be between 0.0 and 1.0";

        if (MaxMessageAge < TimeSpan.Zero)
            return "MaxMessageAge must be non-negative";

        return null;
    }
}