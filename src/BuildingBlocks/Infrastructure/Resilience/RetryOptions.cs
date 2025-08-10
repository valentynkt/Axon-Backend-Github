namespace BuildingBlocks.Infrastructure.Resilience;

public class RetryOptions
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMilliseconds { get; set; } = 1000;
    public int MaxDelayMilliseconds { get; set; } = 30000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;

    // Properties expected by RetryPolicyResolver
    public int DefaultMaxAttempts => MaxRetries;
    public TimeSpan DefaultInitialDelay => TimeSpan.FromMilliseconds(InitialDelayMilliseconds);
    public TimeSpan DefaultMaxDelay => TimeSpan.FromMilliseconds(MaxDelayMilliseconds);
}