namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Defines retry policy configuration for handling transient failures.
/// Supports exponential backoff, circuit breaker patterns, and jitter.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Initial delay between retry attempts.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay between retry attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use circuit breaker pattern.
    /// </summary>
    public bool UseCircuitBreaker { get; init; }

    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// </summary>
    public int CircuitBreakerThreshold { get; init; } = 5;

    /// <summary>
    /// Duration to keep the circuit open before attempting to close it.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to add jitter to retry delays to prevent thundering herd.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Creates a default retry policy with conservative settings.
    /// </summary>
    public static RetryPolicy Default => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(30),
        UseCircuitBreaker = false,
        UseJitter = true
    };

    /// <summary>
    /// Creates a retry policy suitable for commands with circuit breaker.
    /// </summary>
    public static RetryPolicy ForCommands => new()
    {
        MaxAttempts = 2,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(10),
        UseCircuitBreaker = true,
        CircuitBreakerThreshold = 3,
        CircuitBreakerDuration = TimeSpan.FromSeconds(30),
        UseJitter = true
    };

    /// <summary>
    /// Creates a retry policy suitable for queries.
    /// </summary>
    public static RetryPolicy ForQueries => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(5),
        UseCircuitBreaker = false,
        UseJitter = true
    };
}