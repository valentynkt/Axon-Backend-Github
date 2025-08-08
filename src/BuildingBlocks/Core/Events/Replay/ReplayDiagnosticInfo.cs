namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Diagnostic and troubleshooting information for replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides detailed technical information for monitoring, debugging, and optimization.
/// </summary>
public sealed record ReplayDiagnosticInfo
{
    /// <summary>
    /// System environment information when the replay started.
    /// </summary>
    public SystemEnvironmentInfo? EnvironmentInfo { get; init; }

    /// <summary>
    /// Configuration settings used for this replay operation.
    /// </summary>
    public ReplayConfigurationSnapshot? ConfigurationSnapshot { get; init; }

    /// <summary>
    /// Thread and concurrency information.
    /// </summary>
    public ConcurrencyDiagnostics? ConcurrencyInfo { get; init; }

    /// <summary>
    /// Memory usage patterns and garbage collection statistics.
    /// </summary>
    public MemoryDiagnostics? MemoryInfo { get; init; }

    /// <summary>
    /// Network and I/O operation statistics.
    /// </summary>
    public NetworkDiagnostics? NetworkInfo { get; init; }

    /// <summary>
    /// Event store interaction statistics.
    /// </summary>
    public EventStoreDiagnostics? EventStoreInfo { get; init; }

    /// <summary>
    /// Error patterns and failure analysis.
    /// </summary>
    public ErrorPatternAnalysis? ErrorAnalysis { get; init; }

    /// <summary>
    /// Performance bottleneck identification.
    /// </summary>
    public BottleneckAnalysis? BottleneckInfo { get; init; }

    /// <summary>
    /// Resource utilization over time.
    /// </summary>
    public IReadOnlyList<ResourceUtilizationSnapshot> ResourceHistory { get; init; } = [];

    /// <summary>
    /// Custom diagnostic data specific to the implementation.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomDiagnostics { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Timestamp when diagnostic information was collected.
    /// </summary>
    public DateTime CollectedAtUtc { get; init; }
}

/// <summary>
/// System environment information during replay operation.
/// </summary>
public sealed record SystemEnvironmentInfo
{
    public string MachineName { get; init; } = default!;
    public string OperatingSystem { get; init; } = default!;
    public int ProcessorCount { get; init; }
    public long TotalMemoryBytes { get; init; }
    public string RuntimeVersion { get; init; } = default!;
    public bool IsDebugBuild { get; init; }
    public string ApplicationVersion { get; init; } = default!;
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = 
        new Dictionary<string, string>();
}

/// <summary>
/// Snapshot of configuration settings used during replay.
/// </summary>
public sealed record ReplayConfigurationSnapshot
{
    public int BatchSize { get; init; }
    public int MaxConcurrency { get; init; }
    public TimeSpan DelayBetweenBatches { get; init; }
    public int MaxRetryAttempts { get; init; }
    public TimeSpan RetryDelay { get; init; }
    public bool PreventDuplicates { get; init; }
    public bool ContinueOnError { get; init; }
    public ReplayMode Mode { get; init; }
    public ReplayPriority Priority { get; init; }
    public IReadOnlyDictionary<string, object> CustomSettings { get; init; } = 
        new Dictionary<string, object>();
}

/// <summary>
/// Thread and concurrency diagnostic information.
/// </summary>
public sealed record ConcurrencyDiagnostics
{
    public int ActiveThreads { get; init; }
    public int MaxThreadsUsed { get; init; }
    public int ThreadPoolWorkerThreads { get; init; }
    public int ThreadPoolCompletionPortThreads { get; init; }
    public int QueuedWorkItems { get; init; }
    public TimeSpan TotalCpuTime { get; init; }
    public int DeadlockDetectionCount { get; init; }
    public int ThreadContentionCount { get; init; }
    public double AverageThreadUtilization { get; init; }
}

/// <summary>
/// Memory usage and garbage collection diagnostic information.
/// </summary>
public sealed record MemoryDiagnostics
{
    public long WorkingSetBytes { get; init; }
    public long PrivateMemoryBytes { get; init; }
    public long ManagedMemoryBytes { get; init; }
    public long Gen0Collections { get; init; }
    public long Gen1Collections { get; init; }
    public long Gen2Collections { get; init; }
    public TimeSpan TotalGcTime { get; init; }
    public long LargeObjectHeapSize { get; init; }
    public double MemoryPressure { get; init; }
    public int OutOfMemoryExceptions { get; init; }
}

/// <summary>
/// Network and I/O operation diagnostic information.
/// </summary>
public sealed record NetworkDiagnostics
{
    public long TotalNetworkRequests { get; init; }
    public long FailedNetworkRequests { get; init; }
    public TimeSpan AverageRequestDuration { get; init; }
    public TimeSpan MaxRequestDuration { get; init; }
    public long TotalBytesReceived { get; init; }
    public long TotalBytesSent { get; init; }
    public int ActiveConnections { get; init; }
    public int ConnectionTimeouts { get; init; }
    public double NetworkUtilizationPercent { get; init; }
}

/// <summary>
/// Event store interaction diagnostic information.
/// </summary>
public sealed record EventStoreDiagnostics
{
    public long TotalEventReads { get; init; }
    public long FailedEventReads { get; init; }
    public TimeSpan AverageReadDuration { get; init; }
    public TimeSpan MaxReadDuration { get; init; }
    public long TotalEventsDeserializedCount { get; init; }
    public long DeserializationFailureCount { get; init; }
    public TimeSpan AverageDeserializationTime { get; init; }
    public long CacheHitCount { get; init; }
    public long CacheMissCount { get; init; }
    public double CacheHitRatio => CacheHitCount + CacheMissCount > 0 
        ? (double)CacheHitCount / (CacheHitCount + CacheMissCount) * 100.0 
        : 0.0;
}

/// <summary>
/// Error pattern analysis for troubleshooting.
/// </summary>
public sealed record ErrorPatternAnalysis
{
    public IReadOnlyDictionary<string, int> ErrorTypeFrequency { get; init; } = 
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ErrorComponentFrequency { get; init; } = 
        new Dictionary<string, int>();
    public IReadOnlyList<string> MostCommonErrors { get; init; } = [];
    public IReadOnlyList<string> CriticalErrorsDetected { get; init; } = [];
    public double ErrorRate { get; init; }
    public TimeSpan? ErrorBurstDuration { get; init; }
    public bool HasErrorPatterns { get; init; }
    public string? SuggestedResolution { get; init; }
}

/// <summary>
/// Performance bottleneck identification and analysis.
/// </summary>
public sealed record BottleneckAnalysis
{
    public BottleneckType PrimaryBottleneck { get; init; }
    public double BottleneckSeverity { get; init; }
    public string? BottleneckDescription { get; init; }
    public IReadOnlyList<string> PerformanceRecommendations { get; init; } = [];
    public IReadOnlyDictionary<string, double> ResourceUtilizationScores { get; init; } = 
        new Dictionary<string, double>();
    public bool RequiresAttention => BottleneckSeverity > 0.7;
}

/// <summary>
/// Types of performance bottlenecks that can be detected.
/// </summary>
public enum BottleneckType
{
    None = 0,
    CPU = 1,
    Memory = 2,
    Network = 3,
    EventStore = 4,
    Threading = 5,
    GarbageCollection = 6,
    Serialization = 7,
    ExternalServices = 8,
    Configuration = 9
}

/// <summary>
/// Snapshot of resource utilization at a specific point in time.
/// </summary>
public sealed record ResourceUtilizationSnapshot
{
    public DateTime Timestamp { get; init; }
    public double CpuUtilizationPercent { get; init; }
    public long MemoryUsageBytes { get; init; }
    public double NetworkUtilizationPercent { get; init; }
    public int ActiveThreads { get; init; }
    public long EventsPerSecond { get; init; }
    public double ErrorRate { get; init; }
}