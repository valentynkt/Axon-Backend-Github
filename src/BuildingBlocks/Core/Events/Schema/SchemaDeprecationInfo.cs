namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Information about schema deprecation lifecycle and timeline.
/// Provides structured approach to schema evolution and retirement planning.
/// </summary>
public sealed record SchemaDeprecationInfo
{
    /// <summary>
    /// When this schema version was marked as deprecated (UTC).
    /// </summary>
    public required DateTime DeprecatedAtUtc { get; init; }

    /// <summary>
    /// Planned date for removing support for this schema version (UTC).
    /// After this date, the schema may not be supported.
    /// </summary>
    public DateTime? PlannedRemovalDateUtc { get; init; }

    /// <summary>
    /// Reason for deprecating this schema version.
    /// </summary>
    public required string DeprecationReason { get; init; }

    /// <summary>
    /// Recommended schema version to migrate to.
    /// </summary>
    public int? RecommendedMigrationVersion { get; init; }

    /// <summary>
    /// Current stage in the deprecation lifecycle.
    /// </summary>
    public DeprecationStage Stage { get; init; } = DeprecationStage.Deprecated;

    /// <summary>
    /// Warning message to show when this schema version is used.
    /// </summary>
    public string? WarningMessage { get; init; }

    /// <summary>
    /// Contact information for migration support or questions.
    /// </summary>
    public string? MigrationContact { get; init; }

    /// <summary>
    /// Links to migration guides or documentation.
    /// </summary>
    public IReadOnlyList<string>? MigrationResources { get; init; }

    /// <summary>
    /// Custom metadata about the deprecation.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Create deprecation info for immediate deprecation.
    /// </summary>
    public static SchemaDeprecationInfo Deprecated(
        string reason,
        int? recommendedVersion = null,
        DateTime? plannedRemovalDate = null,
        string? warningMessage = null)
    {
        return new SchemaDeprecationInfo
        {
            DeprecatedAtUtc = DateTime.UtcNow,
            PlannedRemovalDateUtc = plannedRemovalDate,
            DeprecationReason = reason,
            RecommendedMigrationVersion = recommendedVersion,
            Stage = DeprecationStage.Deprecated,
            WarningMessage = warningMessage ?? $"This schema version is deprecated. Reason: {reason}"
        };
    }

    /// <summary>
    /// Create deprecation info with scheduled timeline.
    /// </summary>
    public static SchemaDeprecationInfo Scheduled(
        string reason,
        DateTime plannedRemovalDate,
        int? recommendedVersion = null,
        string? contact = null,
        IReadOnlyList<string>? resources = null)
    {
        var removalTimespan = plannedRemovalDate - DateTime.UtcNow;
        var stage = removalTimespan.TotalDays switch
        {
            > 180 => DeprecationStage.Deprecated,
            > 90 => DeprecationStage.EndOfLife,
            > 30 => DeprecationStage.UrgentMigration,
            _ => DeprecationStage.ImmediateRemoval
        };

        return new SchemaDeprecationInfo
        {
            DeprecatedAtUtc = DateTime.UtcNow,
            PlannedRemovalDateUtc = plannedRemovalDate,
            DeprecationReason = reason,
            RecommendedMigrationVersion = recommendedVersion,
            Stage = stage,
            WarningMessage = $"This schema version will be removed on {plannedRemovalDate:yyyy-MM-dd}. Please migrate to version {recommendedVersion}.",
            MigrationContact = contact,
            MigrationResources = resources
        };
    }

    /// <summary>
    /// Create deprecation info for emergency removal.
    /// </summary>
    public static SchemaDeprecationInfo Emergency(
        string reason,
        int? recommendedVersion = null,
        string? contact = null)
    {
        return new SchemaDeprecationInfo
        {
            DeprecatedAtUtc = DateTime.UtcNow,
            PlannedRemovalDateUtc = DateTime.UtcNow.AddDays(7), // One week notice
            DeprecationReason = reason,
            RecommendedMigrationVersion = recommendedVersion,
            Stage = DeprecationStage.ImmediateRemoval,
            WarningMessage = $"URGENT: This schema version is being removed due to: {reason}. Migrate immediately.",
            MigrationContact = contact
        };
    }

    /// <summary>
    /// Check if the schema should show deprecation warnings.
    /// </summary>
    public bool ShouldShowWarnings()
        => Stage != DeprecationStage.None;

    /// <summary>
    /// Check if the schema is past its planned removal date.
    /// </summary>
    public bool IsPastRemovalDate()
        => PlannedRemovalDateUtc.HasValue && DateTime.UtcNow > PlannedRemovalDateUtc.Value;

    /// <summary>
    /// Get the number of days remaining before removal.
    /// </summary>
    public int? DaysUntilRemoval()
    {
        if (!PlannedRemovalDateUtc.HasValue)
            return null;

        var remaining = PlannedRemovalDateUtc.Value - DateTime.UtcNow;
        return Math.Max(0, (int)remaining.TotalDays);
    }
}

/// <summary>
/// Stages in the schema deprecation lifecycle.
/// Ordered by severity from least to most urgent.
/// </summary>
public enum DeprecationStage
{
    /// <summary>
    /// Schema is not deprecated - normal operation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Schema is marked as deprecated but still fully supported.
    /// Users should plan migration but no immediate action required.
    /// </summary>
    Deprecated = 1,

    /// <summary>
    /// Schema is approaching end-of-life.
    /// Active migration planning should begin.
    /// </summary>
    EndOfLife = 2,

    /// <summary>
    /// Urgent migration required - schema will be removed soon.
    /// Critical priority for migration efforts.
    /// </summary>
    UrgentMigration = 3,

    /// <summary>
    /// Schema removal is imminent or overdue.
    /// Immediate action required to prevent service disruption.
    /// </summary>
    ImmediateRemoval = 4
}