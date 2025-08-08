namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Information about a specific version of an event schema.
/// Provides metadata and status for schema version management and history tracking.
/// </summary>
public sealed record SchemaVersionInfo
{
    /// <summary>
    /// The event type this schema version belongs to.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The version number of this schema.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// When this schema version was registered (UTC).
    /// </summary>
    public required DateTime RegisteredAtUtc { get; init; }

    /// <summary>
    /// Who registered this schema version.
    /// </summary>
    public string? RegisteredBy { get; init; }

    /// <summary>
    /// Current status of this schema version.
    /// </summary>
    public SchemaVersionStatus Status { get; init; } = SchemaVersionStatus.Active;

    /// <summary>
    /// Brief description of changes in this version.
    /// </summary>
    public string? ChangeDescription { get; init; }

    /// <summary>
    /// List of breaking changes introduced in this version.
    /// </summary>
    public IReadOnlyList<string>? BreakingChanges { get; init; }

    /// <summary>
    /// Hash of the schema content for integrity verification.
    /// </summary>
    public string? SchemaHash { get; init; }

    /// <summary>
    /// Size of the schema in bytes.
    /// </summary>
    public long SchemaSizeBytes { get; init; }

    /// <summary>
    /// Compatibility level with the previous version.
    /// </summary>
    public CompatibilityLevel? CompatibilityWithPrevious { get; init; }

    /// <summary>
    /// The previous schema version this version is based on.
    /// </summary>
    public int? PreviousVersion { get; init; }

    /// <summary>
    /// Deprecation information if this version is deprecated.
    /// </summary>
    public SchemaDeprecationInfo? DeprecationInfo { get; init; }

    /// <summary>
    /// Usage statistics for this specific schema version.
    /// </summary>
    public SchemaVersionUsageStats? UsageStats { get; init; }

    /// <summary>
    /// Whether this version has migration rules defined.
    /// </summary>
    public bool HasMigrationRules { get; init; }

    /// <summary>
    /// List of versions that can be migrated to from this version.
    /// </summary>
    public IReadOnlyList<int>? MigrationTargets { get; init; }

    /// <summary>
    /// List of versions that can migrate to this version.
    /// </summary>
    public IReadOnlyList<int>? MigrationSources { get; init; }

    /// <summary>
    /// Tags associated with this schema version.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Additional metadata for this schema version.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Create basic schema version info.
    /// </summary>
    public static SchemaVersionInfo Create(
        string eventType,
        int version,
        string? registeredBy = null,
        string? changeDescription = null,
        SchemaVersionStatus status = SchemaVersionStatus.Active,
        string? schemaHash = null,
        long schemaSizeBytes = 0,
        int? previousVersion = null)
    {
        return new SchemaVersionInfo
        {
            EventType = eventType,
            Version = version,
            RegisteredAtUtc = DateTime.UtcNow,
            RegisteredBy = registeredBy,
            Status = status,
            ChangeDescription = changeDescription,
            SchemaHash = schemaHash,
            SchemaSizeBytes = schemaSizeBytes,
            PreviousVersion = previousVersion
        };
    }

    /// <summary>
    /// Create schema version info with compatibility analysis.
    /// </summary>
    public static SchemaVersionInfo CreateWithCompatibility(
        string eventType,
        int version,
        int previousVersion,
        CompatibilityLevel compatibilityWithPrevious,
        string? registeredBy = null,
        string? changeDescription = null,
        IReadOnlyList<string>? breakingChanges = null,
        SchemaVersionStatus status = SchemaVersionStatus.Active,
        bool hasMigrationRules = false)
    {
        return new SchemaVersionInfo
        {
            EventType = eventType,
            Version = version,
            RegisteredAtUtc = DateTime.UtcNow,
            RegisteredBy = registeredBy,
            Status = status,
            ChangeDescription = changeDescription,
            BreakingChanges = breakingChanges,
            CompatibilityWithPrevious = compatibilityWithPrevious,
            PreviousVersion = previousVersion,
            HasMigrationRules = hasMigrationRules
        };
    }

    /// <summary>
    /// Create deprecated schema version info.
    /// </summary>
    public static SchemaVersionInfo CreateDeprecated(
        string eventType,
        int version,
        SchemaDeprecationInfo deprecationInfo,
        string? registeredBy = null,
        DateTime? registeredAtUtc = null,
        SchemaVersionUsageStats? usageStats = null)
    {
        return new SchemaVersionInfo
        {
            EventType = eventType,
            Version = version,
            RegisteredAtUtc = registeredAtUtc ?? DateTime.UtcNow.AddDays(-30), // Assume older registration
            RegisteredBy = registeredBy,
            Status = SchemaVersionStatus.Deprecated,
            DeprecationInfo = deprecationInfo,
            UsageStats = usageStats
        };
    }

    /// <summary>
    /// Check if this schema version is currently active.
    /// </summary>
    public bool IsActive() => Status == SchemaVersionStatus.Active;

    /// <summary>
    /// Check if this schema version is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == SchemaVersionStatus.Deprecated;

    /// <summary>
    /// Check if this schema version is archived.
    /// </summary>
    public bool IsArchived() => Status == SchemaVersionStatus.Archived;

    /// <summary>
    /// Get the age of this schema version.
    /// </summary>
    public TimeSpan GetAge() => DateTime.UtcNow - RegisteredAtUtc;

    /// <summary>
    /// Check if this version has breaking changes from the previous version.
    /// </summary>
    public bool HasBreakingChanges() => BreakingChanges?.Any() == true;
}

/// <summary>
/// Status of a schema version in its lifecycle.
/// </summary>
public enum SchemaVersionStatus
{
    /// <summary>
    /// Schema version is under development - not yet ready for production.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Schema version is active and ready for use.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Schema version is deprecated but still supported.
    /// </summary>
    Deprecated = 2,

    /// <summary>
    /// Schema version is archived - read-only historical record.
    /// </summary>
    Archived = 3,

    /// <summary>
    /// Schema version has been removed from active service.
    /// </summary>
    Removed = 4
}

/// <summary>
/// Usage statistics for a specific schema version.
/// </summary>
public sealed record SchemaVersionUsageStats
{
    /// <summary>
    /// Number of events published using this schema version.
    /// </summary>
    public long EventsPublished { get; init; }

    /// <summary>
    /// Number of events published in the last 24 hours.
    /// </summary>
    public long RecentEventsCount { get; init; }

    /// <summary>
    /// Average events per day over the last 30 days.
    /// </summary>
    public double AverageEventsPerDay { get; init; }

    /// <summary>
    /// Peak usage date for this schema version.
    /// </summary>
    public DateTime? PeakUsageDate { get; init; }

    /// <summary>
    /// Maximum events published in a single day.
    /// </summary>
    public long PeakEventsPerDay { get; init; }

    /// <summary>
    /// Date of the first event published with this schema version.
    /// </summary>
    public DateTime? FirstUsageDate { get; init; }

    /// <summary>
    /// Date of the most recent event published with this schema version.
    /// </summary>
    public DateTime? LastUsageDate { get; init; }

    /// <summary>
    /// Number of unique publishers using this schema version.
    /// </summary>
    public int UniquePublishers { get; init; }

    /// <summary>
    /// Number of unique consumers reading this schema version.
    /// </summary>
    public int UniqueConsumers { get; init; }

    /// <summary>
    /// Percentage of total events for this event type using this version.
    /// </summary>
    public double UsagePercentage { get; init; }

    /// <summary>
    /// When these statistics were last calculated (UTC).
    /// </summary>
    public DateTime LastCalculatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Create basic usage statistics for a schema version.
    /// </summary>
    public static SchemaVersionUsageStats Create(
        long eventsPublished,
        long recentEventsCount = 0,
        double averageEventsPerDay = 0,
        int uniquePublishers = 0,
        int uniqueConsumers = 0,
        double usagePercentage = 0)
    {
        return new SchemaVersionUsageStats
        {
            EventsPublished = eventsPublished,
            RecentEventsCount = recentEventsCount,
            AverageEventsPerDay = averageEventsPerDay,
            UniquePublishers = uniquePublishers,
            UniqueConsumers = uniqueConsumers,
            UsagePercentage = usagePercentage
        };
    }

    /// <summary>
    /// Create comprehensive usage statistics with historical data.
    /// </summary>
    public static SchemaVersionUsageStats CreateDetailed(
        long eventsPublished,
        long recentEventsCount,
        double averageEventsPerDay,
        DateTime? peakUsageDate,
        long peakEventsPerDay,
        DateTime? firstUsageDate,
        DateTime? lastUsageDate,
        int uniquePublishers,
        int uniqueConsumers,
        double usagePercentage)
    {
        return new SchemaVersionUsageStats
        {
            EventsPublished = eventsPublished,
            RecentEventsCount = recentEventsCount,
            AverageEventsPerDay = averageEventsPerDay,
            PeakUsageDate = peakUsageDate,
            PeakEventsPerDay = peakEventsPerDay,
            FirstUsageDate = firstUsageDate,
            LastUsageDate = lastUsageDate,
            UniquePublishers = uniquePublishers,
            UniqueConsumers = uniqueConsumers,
            UsagePercentage = usagePercentage
        };
    }

    /// <summary>
    /// Check if this schema version is currently being used.
    /// </summary>
    public bool IsCurrentlyActive()
    {
        return RecentEventsCount > 0 && 
               LastUsageDate.HasValue && 
               (DateTime.UtcNow - LastUsageDate.Value).TotalDays < 7;
    }

    /// <summary>
    /// Check if this schema version shows declining usage.
    /// </summary>
    public bool IsUsageDeclining()
    {
        return AverageEventsPerDay > 0 && RecentEventsCount < (AverageEventsPerDay * 0.5);
    }
}