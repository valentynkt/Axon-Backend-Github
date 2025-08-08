namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for centralized outbox message processing.
/// Created for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Provides comprehensive configuration for background processing, retry logic, and health monitoring.
/// </summary>
public sealed class OutboxProcessorOptions
{
    /// <summary>
    /// Configuration section name for appsettings.json binding
    /// </summary>
    public const string SectionName = "OutboxProcessor";

    /// <summary>
    /// Whether outbox message processing is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Batch size for processing outbox messages.
    /// Controls how many messages are processed in a single batch operation.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter queue.
    /// Messages exceeding this will be considered permanently failed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum concurrent processing tasks.
    /// Controls the level of parallelism during message processing.
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// Base delay in minutes for exponential backoff retry strategy.
    /// Actual delay will be: BaseRetryDelayMinutes * 2^(attempts-1)
    /// </summary>
    public int BaseRetryDelayMinutes { get; set; } = 1;

    /// <summary>
    /// Timeout in minutes for detecting stuck processing entries.
    /// Messages in processing state longer than this will be considered stale.
    /// </summary>
    public int ProcessingTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Interval for background processor to check for pending messages.
    /// Controls how frequently the processor scans for new messages.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retention period for completed outbox messages before cleanup.
    /// Messages older than this will be eligible for deletion.
    /// </summary>
    public TimeSpan CompletedRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to enable cleanup of completed messages.
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// Interval for cleanup operations.
    /// Controls how frequently cleanup operations are performed.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Batch size for cleanup operations.
    /// Controls how many completed messages are deleted in a single operation.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;

    /// <summary>
    /// Whether to enable detailed performance metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Whether to enable health checks for outbox processing.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Maximum age for messages before they trigger health check warnings.
    /// </summary>
    public TimeSpan HealthCheckMaxAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum number of failed messages before triggering health check warnings.
    /// </summary>
    public int HealthCheckMaxFailedMessages { get; set; } = 50;

    /// <summary>
    /// Whether to log detailed processing information for debugging.
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Circuit breaker settings for processing operations.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration for outbox processing resilience.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Whether to enable circuit breaker for processing operations.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration to keep the circuit open before attempting to close it.
    /// </summary>
    public TimeSpan OpenCircuitDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Number of successful operations required to close the circuit.
    /// </summary>
    public int SuccessThreshold { get; set; } = 3;
}

/// <summary>
/// Extension methods for validating OutboxProcessorOptions
/// </summary>
public static class OutboxProcessorOptionsExtensions
{
    /// <summary>
    /// Validate the configuration options and return validation results
    /// </summary>
    public static IEnumerable<string> Validate(this OutboxProcessorOptions options)
    {
        var errors = new List<string>();

        if (options.BatchSize <= 0)
            errors.Add("BatchSize must be greater than 0");

        if (options.BatchSize > 10000)
            errors.Add("BatchSize should not exceed 10000 for performance reasons");

        if (options.MaxRetries < 0)
            errors.Add("MaxRetries cannot be negative");

        if (options.MaxRetries > 10)
            errors.Add("MaxRetries should not exceed 10 to avoid infinite retry loops");

        if (options.MaxConcurrency <= 0)
            errors.Add("MaxConcurrency must be greater than 0");

        if (options.MaxConcurrency > 100)
            errors.Add("MaxConcurrency should not exceed 100 to avoid resource exhaustion");

        if (options.BaseRetryDelayMinutes <= 0)
            errors.Add("BaseRetryDelayMinutes must be greater than 0");

        if (options.ProcessingTimeoutMinutes <= 0)
            errors.Add("ProcessingTimeoutMinutes must be greater than 0");

        if (options.ProcessingInterval <= TimeSpan.Zero)
            errors.Add("ProcessingInterval must be greater than 0");

        if (options.ProcessingInterval > TimeSpan.FromHours(1))
            errors.Add("ProcessingInterval should not exceed 1 hour");

        if (options.CompletedRetentionPeriod <= TimeSpan.Zero)
            errors.Add("CompletedRetentionPeriod must be greater than 0");

        if (options.CleanupInterval <= TimeSpan.Zero)
            errors.Add("CleanupInterval must be greater than 0");

        if (options.CleanupBatchSize <= 0)
            errors.Add("CleanupBatchSize must be greater than 0");

        if (options.HealthCheckMaxAge <= TimeSpan.Zero)
            errors.Add("HealthCheckMaxAge must be greater than 0");

        if (options.HealthCheckMaxFailedMessages < 0)
            errors.Add("HealthCheckMaxFailedMessages cannot be negative");

        // Circuit breaker validation
        if (options.CircuitBreaker.Enabled)
        {
            if (options.CircuitBreaker.FailureThreshold <= 0)
                errors.Add("CircuitBreaker FailureThreshold must be greater than 0");

            if (options.CircuitBreaker.OpenCircuitDuration <= TimeSpan.Zero)
                errors.Add("CircuitBreaker OpenCircuitDuration must be greater than 0");

            if (options.CircuitBreaker.SuccessThreshold <= 0)
                errors.Add("CircuitBreaker SuccessThreshold must be greater than 0");
        }

        return errors;
    }

    /// <summary>
    /// Check if the configuration is valid
    /// </summary>
    public static bool IsValid(this OutboxProcessorOptions options)
    {
        return !options.Validate().Any();
    }
}