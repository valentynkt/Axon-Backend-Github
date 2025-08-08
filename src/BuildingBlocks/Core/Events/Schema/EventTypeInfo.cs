namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Information about a registered event type in the schema registry.
/// Provides summary metadata for event type discovery and maintenance operations.
/// </summary>
public sealed record EventTypeInfo
{
    /// <summary>
    /// The full name of the event type (typically the class name).
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// Human-readable display name for the event type.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Brief description of what this event represents.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Current latest version number for this event type.
    /// </summary>
    public required int LatestVersion { get; init; }

    /// <summary>
    /// Total number of registered versions for this event type.
    /// </summary>
    public int TotalVersions { get; init; } = 1;

    /// <summary>
    /// When this event type was first registered (UTC).
    /// </summary>
    public DateTime FirstRegisteredUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the latest version was registered (UTC).
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this event type has any deprecated versions.
    /// </summary>
    public bool HasDeprecatedVersions { get; init; }

    /// <summary>
    /// Number of deprecated versions for this event type.
    /// </summary>
    public int DeprecatedVersionCount { get; init; }

    /// <summary>
    /// Category or domain this event type belongs to.
    /// Used for organizing and grouping related events.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Tags associated with this event type for categorization and search.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Current health status of this event type's schemas.
    /// </summary>
    public EventTypeHealthStatus HealthStatus { get; init; } = EventTypeHealthStatus.Healthy;

    /// <summary>
    /// Usage statistics for this event type.
    /// </summary>
    public EventTypeUsageStats? UsageStats { get; init; }

    /// <summary>
    /// Additional metadata about this event type.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Create basic event type info for a newly registered event.
    /// </summary>
    public static EventTypeInfo Create(
        string eventTypeName,
        int latestVersion = 1,
        string? displayName = null,
        string? description = null,
        string? category = null,
        IReadOnlyList<string>? tags = null)
    {
        return new EventTypeInfo
        {
            EventTypeName = eventTypeName,
            DisplayName = displayName ?? eventTypeName,
            Description = description,
            LatestVersion = latestVersion,
            TotalVersions = 1,
            Category = category,
            Tags = tags,
            HealthStatus = EventTypeHealthStatus.Healthy
        };
    }

    /// <summary>
    /// Create event type info with complete statistics.
    /// </summary>
    public static EventTypeInfo CreateWithStats(
        string eventTypeName,
        int latestVersion,
        int totalVersions,
        int deprecatedVersionCount,
        DateTime firstRegisteredUtc,
        DateTime lastUpdatedUtc,
        EventTypeUsageStats? usageStats = null,
        string? displayName = null,
        string? description = null,
        string? category = null,
        EventTypeHealthStatus healthStatus = EventTypeHealthStatus.Healthy)
    {
        return new EventTypeInfo
        {
            EventTypeName = eventTypeName,
            DisplayName = displayName ?? eventTypeName,
            Description = description,
            LatestVersion = latestVersion,
            TotalVersions = totalVersions,
            FirstRegisteredUtc = firstRegisteredUtc,
            LastUpdatedUtc = lastUpdatedUtc,
            HasDeprecatedVersions = deprecatedVersionCount > 0,
            DeprecatedVersionCount = deprecatedVersionCount,
            Category = category,
            HealthStatus = healthStatus,
            UsageStats = usageStats
        };
    }
}

/// <summary>
/// Health status for an event type across all its versions.
/// </summary>
public enum EventTypeHealthStatus
{
    /// <summary>
    /// All versions are healthy and operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Some versions have warnings but are still functional.
    /// </summary>
    Warning,

    /// <summary>
    /// Some versions have errors or compatibility issues.
    /// </summary>
    Degraded,

    /// <summary>
    /// Critical issues detected - may impact functionality.
    /// </summary>
    Critical,

    /// <summary>
    /// Health status could not be determined.
    /// </summary>
    Unknown
}

/// <summary>
/// Usage statistics for an event type.
/// Provides insights into event popularity and adoption.
/// </summary>
public sealed record EventTypeUsageStats
{
    /// <summary>
    /// Total number of events published for this type (all versions).
    /// </summary>
    public long TotalEventsPublished { get; init; }

    /// <summary>
    /// Number of events published in the last 24 hours.
    /// </summary>
    public long RecentEventsCount { get; init; }

    /// <summary>
    /// Average events published per day over the last 30 days.
    /// </summary>
    public double AverageEventsPerDay { get; init; }

    /// <summary>
    /// Number of distinct publishers/producers for this event type.
    /// </summary>
    public int UniquePublishers { get; init; }

    /// <summary>
    /// Number of distinct consumers/subscribers for this event type.
    /// </summary>
    public int UniqueConsumers { get; init; }

    /// <summary>
    /// Most frequently used version of this event type.
    /// </summary>
    public int? MostUsedVersion { get; init; }

    /// <summary>
    /// Percentage of usage for the most popular version (0-100).
    /// </summary>
    public double MostUsedVersionPercentage { get; init; }

    /// <summary>
    /// When these statistics were last calculated (UTC).
    /// </summary>
    public DateTime LastCalculatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Create basic usage statistics.
    /// </summary>
    public static EventTypeUsageStats Create(
        long totalEventsPublished,
        long recentEventsCount = 0,
        double averageEventsPerDay = 0,
        int uniquePublishers = 0,
        int uniqueConsumers = 0,
        int? mostUsedVersion = null,
        double mostUsedVersionPercentage = 0)
    {
        return new EventTypeUsageStats
        {
            TotalEventsPublished = totalEventsPublished,
            RecentEventsCount = recentEventsCount,
            AverageEventsPerDay = averageEventsPerDay,
            UniquePublishers = uniquePublishers,
            UniqueConsumers = uniqueConsumers,
            MostUsedVersion = mostUsedVersion,
            MostUsedVersionPercentage = mostUsedVersionPercentage
        };
    }
}