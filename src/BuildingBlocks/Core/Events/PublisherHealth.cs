namespace BuildingBlocks.Core.Events;

/// <summary>
/// Represents the health status and performance metrics of an external event publisher.
/// Provides comprehensive diagnostics for monitoring and alerting purposes.
/// </summary>
/// <param name="Status">Overall health status of the publisher</param>
/// <param name="IsConnected">Whether the publisher is currently connected to the message broker</param>
/// <param name="ConnectionLatency">Current connection latency to the message broker</param>
/// <param name="LastSuccessfulPublish">UTC timestamp of the last successful event publish</param>
/// <param name="LastFailure">UTC timestamp of the last publish failure, if any</param>
/// <param name="SuccessRate">Success rate percentage over the monitored period (0-100)</param>
/// <param name="AverageLatency">Average publish latency over the monitored period</param>
/// <param name="ThroughputPerSecond">Current throughput in events per second</param>
/// <param name="PendingEventCount">Number of events waiting to be published</param>
/// <param name="ErrorCount">Number of errors in the current monitoring period</param>
/// <param name="MonitoringPeriod">Time window for the metrics calculation</param>
/// <param name="AdditionalMetrics">Additional broker-specific metrics</param>
/// <param name="LastCheckedAtUtc">UTC timestamp when this health check was performed</param>
public sealed record PublisherHealth(
    HealthStatus Status,
    bool IsConnected,
    TimeSpan ConnectionLatency,
    DateTime? LastSuccessfulPublish = null,
    DateTime? LastFailure = null,
    double SuccessRate = 100.0,
    TimeSpan AverageLatency = default,
    double ThroughputPerSecond = 0.0,
    long PendingEventCount = 0,
    int ErrorCount = 0,
    TimeSpan MonitoringPeriod = default,
    IReadOnlyDictionary<string, object>? AdditionalMetrics = null,
    DateTime? LastCheckedAtUtc = null)
{
    /// <summary>
    /// Indicates whether the publisher is considered healthy.
    /// </summary>
    public bool IsHealthy => Status == HealthStatus.Healthy;

    /// <summary>
    /// Indicates whether there are performance concerns.
    /// </summary>
    public bool HasPerformanceConcerns => Status == HealthStatus.Degraded;

    /// <summary>
    /// Indicates whether the publisher is currently unavailable.
    /// </summary>
    public bool IsUnhealthy => Status == HealthStatus.Unhealthy;

    /// <summary>
    /// Time since the last successful publish operation.
    /// </summary>
    public TimeSpan? TimeSinceLastSuccess => LastSuccessfulPublish.HasValue 
        ? DateTime.UtcNow - LastSuccessfulPublish.Value 
        : null;

    /// <summary>
    /// Time since the last failure.
    /// </summary>
    public TimeSpan? TimeSinceLastFailure => LastFailure.HasValue 
        ? DateTime.UtcNow - LastFailure.Value 
        : null;

    /// <summary>
    /// Creates a healthy PublisherHealth instance.
    /// </summary>
    public static PublisherHealth Healthy(
        TimeSpan connectionLatency,
        DateTime? lastSuccessfulPublish = null,
        double successRate = 100.0,
        TimeSpan averageLatency = default,
        double throughputPerSecond = 0.0,
        long pendingEventCount = 0,
        TimeSpan monitoringPeriod = default,
        IReadOnlyDictionary<string, object>? additionalMetrics = null)
    {
        return new PublisherHealth(
            Status: HealthStatus.Healthy,
            IsConnected: true,
            ConnectionLatency: connectionLatency,
            LastSuccessfulPublish: lastSuccessfulPublish,
            LastFailure: null,
            SuccessRate: successRate,
            AverageLatency: averageLatency,
            ThroughputPerSecond: throughputPerSecond,
            PendingEventCount: pendingEventCount,
            ErrorCount: 0,
            MonitoringPeriod: monitoringPeriod,
            AdditionalMetrics: additionalMetrics,
            LastCheckedAtUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a degraded PublisherHealth instance.
    /// </summary>
    public static PublisherHealth Degraded(
        TimeSpan connectionLatency,
        double successRate,
        TimeSpan averageLatency,
        int errorCount,
        DateTime? lastSuccessfulPublish = null,
        DateTime? lastFailure = null,
        double throughputPerSecond = 0.0,
        long pendingEventCount = 0,
        TimeSpan monitoringPeriod = default,
        IReadOnlyDictionary<string, object>? additionalMetrics = null)
    {
        return new PublisherHealth(
            Status: HealthStatus.Degraded,
            IsConnected: true,
            ConnectionLatency: connectionLatency,
            LastSuccessfulPublish: lastSuccessfulPublish,
            LastFailure: lastFailure,
            SuccessRate: successRate,
            AverageLatency: averageLatency,
            ThroughputPerSecond: throughputPerSecond,
            PendingEventCount: pendingEventCount,
            ErrorCount: errorCount,
            MonitoringPeriod: monitoringPeriod,
            AdditionalMetrics: additionalMetrics,
            LastCheckedAtUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Creates an unhealthy PublisherHealth instance.
    /// </summary>
    public static PublisherHealth Unhealthy(
        bool isConnected,
        TimeSpan connectionLatency,
        int errorCount,
        DateTime? lastFailure = null,
        DateTime? lastSuccessfulPublish = null,
        double successRate = 0.0,
        TimeSpan averageLatency = default,
        long pendingEventCount = 0,
        TimeSpan monitoringPeriod = default,
        IReadOnlyDictionary<string, object>? additionalMetrics = null)
    {
        return new PublisherHealth(
            Status: HealthStatus.Unhealthy,
            IsConnected: isConnected,
            ConnectionLatency: connectionLatency,
            LastSuccessfulPublish: lastSuccessfulPublish,
            LastFailure: lastFailure,
            SuccessRate: successRate,
            AverageLatency: averageLatency,
            ThroughputPerSecond: 0.0,
            PendingEventCount: pendingEventCount,
            ErrorCount: errorCount,
            MonitoringPeriod: monitoringPeriod,
            AdditionalMetrics: additionalMetrics,
            LastCheckedAtUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a PublisherHealth instance for a disconnected publisher.
    /// </summary>
    public static PublisherHealth Disconnected(
        int errorCount = 0,
        DateTime? lastFailure = null,
        DateTime? lastSuccessfulPublish = null,
        long pendingEventCount = 0,
        IReadOnlyDictionary<string, object>? additionalMetrics = null)
    {
        return Unhealthy(
            isConnected: false,
            connectionLatency: TimeSpan.MaxValue,
            errorCount: errorCount,
            lastFailure: lastFailure,
            lastSuccessfulPublish: lastSuccessfulPublish,
            successRate: 0.0,
            pendingEventCount: pendingEventCount,
            additionalMetrics: additionalMetrics);
    }
}

/// <summary>
/// Represents the overall health status of a publisher.
/// </summary>
public enum HealthStatus : byte
{
    /// <summary>
    /// Publisher is operating normally with acceptable performance metrics.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Publisher is functional but experiencing performance degradation or intermittent issues.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Publisher is not functional or experiencing critical issues.
    /// </summary>
    Unhealthy = 3
}