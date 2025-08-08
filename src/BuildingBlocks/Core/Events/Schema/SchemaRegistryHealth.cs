namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Comprehensive health status and metrics for the schema registry.
/// Provides operational insights and diagnostic information for monitoring.
/// </summary>
public sealed record SchemaRegistryHealth
{
    /// <summary>
    /// Overall health status of the schema registry.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// When this health check was performed (UTC).
    /// </summary>
    public required DateTime CheckedAtUtc { get; init; }

    /// <summary>
    /// Time taken to perform the health check.
    /// </summary>
    public required TimeSpan CheckDuration { get; init; }

    /// <summary>
    /// Detailed health information for various registry components.
    /// </summary>
    public required IReadOnlyDictionary<string, ComponentHealth> ComponentHealth { get; init; }

    /// <summary>
    /// Performance metrics for the schema registry.
    /// </summary>
    public SchemaRegistryMetrics? Metrics { get; init; }

    /// <summary>
    /// Any health issues or warnings detected.
    /// </summary>
    public IReadOnlyList<HealthIssue>? Issues { get; init; }

    /// <summary>
    /// Configuration status and validation results.
    /// </summary>
    public ConfigurationHealth? Configuration { get; init; }

    /// <summary>
    /// Additional metadata about the registry state.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Create a healthy registry status.
    /// </summary>
    public static SchemaRegistryHealth Healthy(
        IReadOnlyDictionary<string, ComponentHealth>? componentHealth = null,
        SchemaRegistryMetrics? metrics = null,
        ConfigurationHealth? configuration = null)
    {
        return new SchemaRegistryHealth
        {
            Status = HealthStatus.Healthy,
            CheckedAtUtc = DateTime.UtcNow,
            CheckDuration = TimeSpan.Zero,
            ComponentHealth = componentHealth ?? new Dictionary<string, ComponentHealth>(),
            Metrics = metrics,
            Configuration = configuration,
            Issues = Array.Empty<HealthIssue>()
        };
    }

    /// <summary>
    /// Create an unhealthy registry status with issues.
    /// </summary>
    public static SchemaRegistryHealth Unhealthy(
        IReadOnlyList<HealthIssue> issues,
        IReadOnlyDictionary<string, ComponentHealth>? componentHealth = null,
        SchemaRegistryMetrics? metrics = null,
        TimeSpan? checkDuration = null)
    {
        return new SchemaRegistryHealth
        {
            Status = HealthStatus.Unhealthy,
            CheckedAtUtc = DateTime.UtcNow,
            CheckDuration = checkDuration ?? TimeSpan.Zero,
            ComponentHealth = componentHealth ?? new Dictionary<string, ComponentHealth>(),
            Metrics = metrics,
            Issues = issues
        };
    }

    /// <summary>
    /// Create a degraded registry status.
    /// </summary>
    public static SchemaRegistryHealth Degraded(
        IReadOnlyList<HealthIssue> issues,
        IReadOnlyDictionary<string, ComponentHealth>? componentHealth = null,
        SchemaRegistryMetrics? metrics = null,
        TimeSpan? checkDuration = null)
    {
        return new SchemaRegistryHealth
        {
            Status = HealthStatus.Degraded,
            CheckedAtUtc = DateTime.UtcNow,
            CheckDuration = checkDuration ?? TimeSpan.Zero,
            ComponentHealth = componentHealth ?? new Dictionary<string, ComponentHealth>(),
            Metrics = metrics,
            Issues = issues
        };
    }
}

/// <summary>
/// Overall health status levels.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// All systems operating normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// Minor issues detected but system is functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Significant issues detected - system may not be fully functional.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Health status could not be determined.
    /// </summary>
    Unknown
}

/// <summary>
/// Health status for individual schema registry components.
/// </summary>
public sealed record ComponentHealth
{
    /// <summary>
    /// Health status of this component.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Human-readable description of the component health.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Response time or latency for this component.
    /// </summary>
    public TimeSpan? ResponseTime { get; init; }

    /// <summary>
    /// Last time this component was checked.
    /// </summary>
    public DateTime? LastCheckedUtc { get; init; }

    /// <summary>
    /// Component-specific metrics or data.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    /// <summary>
    /// Create a healthy component status.
    /// </summary>
    public static ComponentHealth Healthy(
        string? description = null,
        TimeSpan? responseTime = null,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new ComponentHealth
        {
            Status = HealthStatus.Healthy,
            Description = description,
            ResponseTime = responseTime,
            LastCheckedUtc = DateTime.UtcNow,
            Data = data
        };
    }

    /// <summary>
    /// Create an unhealthy component status.
    /// </summary>
    public static ComponentHealth Unhealthy(
        string description,
        IReadOnlyDictionary<string, object>? data = null)
    {
        return new ComponentHealth
        {
            Status = HealthStatus.Unhealthy,
            Description = description,
            LastCheckedUtc = DateTime.UtcNow,
            Data = data
        };
    }
}

/// <summary>
/// Individual health issue or warning.
/// </summary>
public sealed record HealthIssue
{
    /// <summary>
    /// Severity level of this health issue.
    /// </summary>
    public required HealthIssueSeverity Severity { get; init; }

    /// <summary>
    /// Component or area where the issue was detected.
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// Detailed description of the issue.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// When this issue was first detected.
    /// </summary>
    public DateTime DetectedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Recommended action to resolve the issue.
    /// </summary>
    public string? RecommendedAction { get; init; }

    /// <summary>
    /// Additional context or diagnostic information.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Create a critical health issue.
    /// </summary>
    public static HealthIssue Critical(
        string component,
        string description,
        string? recommendedAction = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        return new HealthIssue
        {
            Severity = HealthIssueSeverity.Critical,
            Component = component,
            Description = description,
            RecommendedAction = recommendedAction,
            Context = context
        };
    }

    /// <summary>
    /// Create a warning health issue.
    /// </summary>
    public static HealthIssue Warning(
        string component,
        string description,
        string? recommendedAction = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        return new HealthIssue
        {
            Severity = HealthIssueSeverity.Warning,
            Component = component,
            Description = description,
            RecommendedAction = recommendedAction,
            Context = context
        };
    }
}

/// <summary>
/// Severity levels for health issues.
/// </summary>
public enum HealthIssueSeverity
{
    /// <summary>
    /// Informational - no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - should be investigated but not critical.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - significant issue that should be addressed.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - severe issue requiring immediate attention.
    /// </summary>
    Critical
}

/// <summary>
/// Performance metrics for the schema registry.
/// </summary>
public sealed record SchemaRegistryMetrics
{
    /// <summary>
    /// Total number of registered schemas.
    /// </summary>
    public int TotalSchemas { get; init; }

    /// <summary>
    /// Number of active (non-deprecated) schemas.
    /// </summary>
    public int ActiveSchemas { get; init; }

    /// <summary>
    /// Number of deprecated schemas.
    /// </summary>
    public int DeprecatedSchemas { get; init; }

    /// <summary>
    /// Average schema registration time.
    /// </summary>
    public TimeSpan AverageRegistrationTime { get; init; }

    /// <summary>
    /// Average schema validation time.
    /// </summary>
    public TimeSpan AverageValidationTime { get; init; }

    /// <summary>
    /// Average compatibility check time.
    /// </summary>
    public TimeSpan AverageCompatibilityCheckTime { get; init; }

    /// <summary>
    /// Cache hit ratio for schema lookups (0-100).
    /// </summary>
    public double CacheHitRatio { get; init; }

    /// <summary>
    /// Number of schema operations performed recently.
    /// </summary>
    public long RecentOperations { get; init; }

    /// <summary>
    /// Current memory usage for schema storage.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Storage utilization metrics.
    /// </summary>
    public StorageMetrics? Storage { get; init; }

    /// <summary>
    /// Create basic metrics from operation counts.
    /// </summary>
    public static SchemaRegistryMetrics Create(
        int totalSchemas,
        int activeSchemas,
        int deprecatedSchemas,
        TimeSpan? averageRegistrationTime = null,
        double cacheHitRatio = 0.0,
        long memoryUsageBytes = 0)
    {
        return new SchemaRegistryMetrics
        {
            TotalSchemas = totalSchemas,
            ActiveSchemas = activeSchemas,
            DeprecatedSchemas = deprecatedSchemas,
            AverageRegistrationTime = averageRegistrationTime ?? TimeSpan.Zero,
            CacheHitRatio = cacheHitRatio,
            MemoryUsageBytes = memoryUsageBytes
        };
    }
}

/// <summary>
/// Storage-related metrics for the schema registry.
/// </summary>
public sealed record StorageMetrics
{
    /// <summary>
    /// Total storage space used in bytes.
    /// </summary>
    public long UsedSpaceBytes { get; init; }

    /// <summary>
    /// Total available storage space in bytes.
    /// </summary>
    public long TotalSpaceBytes { get; init; }

    /// <summary>
    /// Storage utilization percentage (0-100).
    /// </summary>
    public double UtilizationPercentage { get; init; }

    /// <summary>
    /// Average I/O response time for storage operations.
    /// </summary>
    public TimeSpan AverageIoTime { get; init; }

    /// <summary>
    /// Number of storage operations performed recently.
    /// </summary>
    public long RecentOperations { get; init; }
}

/// <summary>
/// Configuration health and validation status.
/// </summary>
public sealed record ConfigurationHealth
{
    /// <summary>
    /// Whether the registry configuration is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Configuration validation errors or warnings.
    /// </summary>
    public IReadOnlyList<string>? ValidationMessages { get; init; }

    /// <summary>
    /// When the configuration was last validated.
    /// </summary>
    public DateTime LastValidatedUtc { get; init; }

    /// <summary>
    /// Configuration source information.
    /// </summary>
    public string? ConfigurationSource { get; init; }

    /// <summary>
    /// Version of the configuration schema used.
    /// </summary>
    public string? ConfigurationVersion { get; init; }

    /// <summary>
    /// Create valid configuration health status.
    /// </summary>
    public static ConfigurationHealth Valid(
        string? source = null,
        string? version = null)
    {
        return new ConfigurationHealth
        {
            IsValid = true,
            ValidationMessages = Array.Empty<string>(),
            LastValidatedUtc = DateTime.UtcNow,
            ConfigurationSource = source,
            ConfigurationVersion = version
        };
    }

    /// <summary>
    /// Create invalid configuration health status.
    /// </summary>
    public static ConfigurationHealth Invalid(
        IReadOnlyList<string> validationMessages,
        string? source = null)
    {
        return new ConfigurationHealth
        {
            IsValid = false,
            ValidationMessages = validationMessages,
            LastValidatedUtc = DateTime.UtcNow,
            ConfigurationSource = source
        };
    }
}