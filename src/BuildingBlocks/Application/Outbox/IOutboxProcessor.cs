using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Interface for processing outbox events after successful transaction commits.
/// Enhanced for Epic_04 CQRS_Foundation Story_06 - Transaction Management Enhancement.
/// Provides reliable background processing with health monitoring capabilities.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Process all pending outbox events in batches.
    /// Used by background services for reliable event processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxProcessingResult>> ProcessPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Process pending outbox events for a specific transaction.
    /// Used for immediate processing after transaction commit.
    /// </summary>
    /// <param name="transactionId">Transaction ID to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with processing statistics</returns>
    Task<Result<OutboxProcessingResult>> ProcessPendingAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get health status of the outbox processor for health checks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result with processor status</returns>
    Task<Result<OutboxProcessorHealth>> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the background processor if it's not already running.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the background processor gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health information for the outbox processor.
/// </summary>
public sealed record OutboxProcessorHealth(
    bool IsRunning,
    DateTime? LastProcessingRun,
    TimeSpan? TimeSinceLastRun,
    int PendingEventCount,
    int DeadLetterEventCount,
    IReadOnlyList<string> RecentErrors);

/// <summary>
/// Configuration options for transaction behavior outbox integration.
/// Enhanced with comprehensive outbox processing settings.
/// </summary>
public sealed class TransactionOptions
{
    /// <summary>
    /// Whether to enable outbox processing after successful commits.
    /// </summary>
    public bool EnableOutboxProcessing { get; set; } = true;

    /// <summary>
    /// Delay before triggering outbox processing to ensure transaction visibility.
    /// </summary>
    public TimeSpan OutboxProcessingDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Default transaction timeout for command processing.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Default isolation level for transactions.
    /// </summary>
    public System.Data.IsolationLevel DefaultIsolationLevel { get; set; } = System.Data.IsolationLevel.ReadCommitted;

    /// <summary>
    /// Whether to use metadata-aware isolation levels.
    /// </summary>
    public bool UseMetadataAwareIsolation { get; set; } = true;

    /// <summary>
    /// Whether to trigger immediate outbox processing after commit (fire-and-forget).
    /// </summary>
    public bool TriggerImmediateProcessing { get; set; } = true;
}

/// <summary>
/// Configuration options for outbox processing.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Whether outbox processing is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Batch size for processing outbox entries.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum concurrent processing tasks.
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// Base delay in minutes for exponential backoff retry strategy.
    /// </summary>
    public int BaseRetryDelayMinutes { get; set; } = 1;

    /// <summary>
    /// Timeout in minutes for detecting stuck processing entries.
    /// </summary>
    public int ProcessingTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Interval for background processor to check for pending events.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retention period for completed outbox entries before cleanup.
    /// </summary>
    public TimeSpan CompletedRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to enable cleanup of completed entries.
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// Interval for cleanup operations.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Batch size for cleanup operations.
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
}