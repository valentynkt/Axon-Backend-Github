namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Performance metrics and statistics for replay operations.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Provides comprehensive performance monitoring and analysis capabilities.
/// </summary>
public sealed record ReplayPerformanceMetrics
{
    /// <summary>
    /// Overall throughput in events processed per second.
    /// Calculated based on total events and elapsed time.
    /// </summary>
    public double EventsPerSecond { get; init; }

    /// <summary>
    /// Average processing time per event in milliseconds.
    /// </summary>
    public double AverageEventProcessingTimeMs { get; init; }

    /// <summary>
    /// Minimum processing time for a single event in milliseconds.
    /// </summary>
    public double MinEventProcessingTimeMs { get; init; }

    /// <summary>
    /// Maximum processing time for a single event in milliseconds.
    /// </summary>
    public double MaxEventProcessingTimeMs { get; init; }

    /// <summary>
    /// 95th percentile processing time in milliseconds.
    /// Useful for understanding performance distribution.
    /// </summary>
    public double P95ProcessingTimeMs { get; init; }

    /// <summary>
    /// 99th percentile processing time in milliseconds.
    /// </summary>
    public double P99ProcessingTimeMs { get; init; }

    /// <summary>
    /// Average batch processing time in milliseconds.
    /// </summary>
    public double AverageBatchProcessingTimeMs { get; init; }

    /// <summary>
    /// Average delay between batches in milliseconds.
    /// </summary>
    public double AverageDelayBetweenBatchesMs { get; init; }

    /// <summary>
    /// Peak memory usage during replay in bytes.
    /// </summary>
    public long PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Average memory usage during replay in bytes.
    /// </summary>
    public long AverageMemoryUsageBytes { get; init; }

    /// <summary>
    /// Peak CPU usage percentage during replay.
    /// </summary>
    public double PeakCpuUsagePercent { get; init; }

    /// <summary>
    /// Average CPU usage percentage during replay.
    /// </summary>
    public double AverageCpuUsagePercent { get; init; }

    /// <summary>
    /// Total number of network requests made during replay.
    /// Includes event store reads, external service calls, etc.
    /// </summary>
    public long TotalNetworkRequests { get; init; }

    /// <summary>
    /// Number of network requests that failed.
    /// </summary>
    public long FailedNetworkRequests { get; init; }

    /// <summary>
    /// Average network request duration in milliseconds.
    /// </summary>
    public double AverageNetworkRequestTimeMs { get; init; }

    /// <summary>
    /// Total amount of data read during replay in bytes.
    /// </summary>
    public long TotalDataReadBytes { get; init; }

    /// <summary>
    /// Total amount of data written during replay in bytes.
    /// </summary>
    public long TotalDataWrittenBytes { get; init; }

    /// <summary>
    /// Number of garbage collection cycles during replay.
    /// Useful for memory optimization analysis.
    /// </summary>
    public int GarbageCollectionCount { get; init; }

    /// <summary>
    /// Total time spent in garbage collection in milliseconds.
    /// </summary>
    public double TotalGcTimeMs { get; init; }

    /// <summary>
    /// Number of times the replay had to wait for resources.
    /// Includes thread pool exhaustion, memory pressure, etc.
    /// </summary>
    public int ResourceWaitCount { get; init; }

    /// <summary>
    /// Total time spent waiting for resources in milliseconds.
    /// </summary>
    public double TotalResourceWaitTimeMs { get; init; }

    /// <summary>
    /// Current concurrency level (active parallel operations).
    /// </summary>
    public int CurrentConcurrencyLevel { get; init; }

    /// <summary>
    /// Maximum concurrency level reached during replay.
    /// </summary>
    public int MaxConcurrencyLevel { get; init; }

    /// <summary>
    /// Number of cache hits during event processing.
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    /// Number of cache misses during event processing.
    /// </summary>
    public long CacheMisses { get; init; }

    /// <summary>
    /// Cache hit ratio as a percentage (0-100).
    /// </summary>
    public double CacheHitRatio => CacheHits + CacheMisses > 0 
        ? (double)CacheHits / (CacheHits + CacheMisses) * 100.0 
        : 0.0;

    /// <summary>
    /// Total number of retries attempted during replay.
    /// </summary>
    public long TotalRetryAttempts { get; init; }

    /// <summary>
    /// Number of successful retries.
    /// </summary>
    public long SuccessfulRetries { get; init; }

    /// <summary>
    /// Retry success rate as a percentage (0-100).
    /// </summary>
    public double RetrySuccessRate => TotalRetryAttempts > 0 
        ? (double)SuccessfulRetries / TotalRetryAttempts * 100.0 
        : 0.0;

    /// <summary>
    /// Performance efficiency score (0-100).
    /// Calculated based on throughput vs resource utilization.
    /// </summary>
    public double EfficiencyScore { get; init; }

    /// <summary>
    /// Custom performance metrics specific to the replay operation.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomMetrics { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Timestamp when metrics were last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; }

    /// <summary>
    /// Creates a summary of key performance indicators.
    /// </summary>
    /// <returns>String summary of important metrics</returns>
    public string CreateSummary()
    {
        return $"""
            Performance Summary:
            - Throughput: {EventsPerSecond:F2} events/sec
            - Avg Processing Time: {AverageEventProcessingTimeMs:F2}ms
            - P95 Processing Time: {P95ProcessingTimeMs:F2}ms
            - Peak Memory: {PeakMemoryUsageBytes / (1024 * 1024):F1} MB
            - Avg CPU: {AverageCpuUsagePercent:F1}%
            - Cache Hit Ratio: {CacheHitRatio:F1}%
            - Retry Success Rate: {RetrySuccessRate:F1}%
            - Efficiency Score: {EfficiencyScore:F1}/100
            """;
    }
}