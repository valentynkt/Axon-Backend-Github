namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Comprehensive result of schema compatibility analysis between two versions.
/// Provides detailed information about compatibility level and specific issues.
/// </summary>
public sealed record CompatibilityResult
{
    /// <summary>
    /// Whether the schemas are compatible based on the analysis.
    /// </summary>
    public required bool IsCompatible { get; init; }

    /// <summary>
    /// The level of compatibility between the schemas.
    /// </summary>
    public required CompatibilityLevel Level { get; init; }

    /// <summary>
    /// Detailed list of compatibility issues found during analysis.
    /// Empty list indicates full compatibility.
    /// </summary>
    public required IReadOnlyList<CompatibilityIssue> Issues { get; init; }

    /// <summary>
    /// Recommended migration steps to handle compatibility issues.
    /// Ordered by priority and dependencies.
    /// </summary>
    public required IReadOnlyList<string> MigrationSteps { get; init; }

    /// <summary>
    /// Overall confidence score for the compatibility analysis (0-100).
    /// Lower scores indicate uncertainty in the analysis.
    /// </summary>
    public int ConfidenceScore { get; init; } = 100;

    /// <summary>
    /// Additional metadata about the compatibility analysis.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Performance metrics of the compatibility check.
    /// </summary>
    public CompatibilityAnalysisMetrics? AnalysisMetrics { get; init; }

    /// <summary>
    /// Create a fully compatible result.
    /// </summary>
    public static CompatibilityResult FullyCompatible(
        IReadOnlyDictionary<string, object>? metadata = null,
        CompatibilityAnalysisMetrics? metrics = null)
    {
        return new CompatibilityResult
        {
            IsCompatible = true,
            Level = CompatibilityLevel.Full,
            Issues = Array.Empty<CompatibilityIssue>(),
            MigrationSteps = Array.Empty<string>(),
            ConfidenceScore = 100,
            Metadata = metadata,
            AnalysisMetrics = metrics
        };
    }

    /// <summary>
    /// Create a backward compatible result.
    /// </summary>
    public static CompatibilityResult BackwardCompatible(
        IReadOnlyList<string>? migrationSteps = null,
        IReadOnlyList<CompatibilityIssue>? issues = null,
        int confidenceScore = 95,
        IReadOnlyDictionary<string, object>? metadata = null,
        CompatibilityAnalysisMetrics? metrics = null)
    {
        return new CompatibilityResult
        {
            IsCompatible = true,
            Level = CompatibilityLevel.Backward,
            Issues = issues ?? Array.Empty<CompatibilityIssue>(),
            MigrationSteps = migrationSteps ?? Array.Empty<string>(),
            ConfidenceScore = confidenceScore,
            Metadata = metadata,
            AnalysisMetrics = metrics
        };
    }

    /// <summary>
    /// Create a forward compatible result.
    /// </summary>
    public static CompatibilityResult ForwardCompatible(
        IReadOnlyList<string>? migrationSteps = null,
        IReadOnlyList<CompatibilityIssue>? issues = null,
        int confidenceScore = 90,
        IReadOnlyDictionary<string, object>? metadata = null,
        CompatibilityAnalysisMetrics? metrics = null)
    {
        return new CompatibilityResult
        {
            IsCompatible = true,
            Level = CompatibilityLevel.Forward,
            Issues = issues ?? Array.Empty<CompatibilityIssue>(),
            MigrationSteps = migrationSteps ?? Array.Empty<string>(),
            ConfidenceScore = confidenceScore,
            Metadata = metadata,
            AnalysisMetrics = metrics
        };
    }

    /// <summary>
    /// Create an incompatible result with breaking changes.
    /// </summary>
    public static CompatibilityResult BreakingChanges(
        IReadOnlyList<CompatibilityIssue> issues,
        IReadOnlyList<string> migrationSteps,
        int confidenceScore = 100,
        IReadOnlyDictionary<string, object>? metadata = null,
        CompatibilityAnalysisMetrics? metrics = null)
    {
        return new CompatibilityResult
        {
            IsCompatible = false,
            Level = CompatibilityLevel.Breaking,
            Issues = issues,
            MigrationSteps = migrationSteps,
            ConfidenceScore = confidenceScore,
            Metadata = metadata,
            AnalysisMetrics = metrics
        };
    }

    /// <summary>
    /// Create result with no compatibility (completely different schemas).
    /// </summary>
    public static CompatibilityResult NoCompatibility(
        IReadOnlyList<CompatibilityIssue> issues,
        IReadOnlyList<string> migrationSteps,
        int confidenceScore = 100,
        IReadOnlyDictionary<string, object>? metadata = null,
        CompatibilityAnalysisMetrics? metrics = null)
    {
        return new CompatibilityResult
        {
            IsCompatible = false,
            Level = CompatibilityLevel.None,
            Issues = issues,
            MigrationSteps = migrationSteps,
            ConfidenceScore = confidenceScore,
            Metadata = metadata,
            AnalysisMetrics = metrics
        };
    }
}

/// <summary>
/// Compatibility levels between schema versions.
/// Ordered from most to least compatible.
/// </summary>
public enum CompatibilityLevel
{
    /// <summary>
    /// No compatibility - completely different schemas that cannot be migrated.
    /// </summary>
    None = 0,

    /// <summary>
    /// Breaking changes - migration required and may result in data loss.
    /// </summary>
    Breaking = 1,

    /// <summary>
    /// Forward compatible - new version can read old events, but not vice versa.
    /// Old consumers cannot handle new events.
    /// </summary>
    Forward = 2,

    /// <summary>
    /// Backward compatible - new version can read old events safely.
    /// New events may not be readable by old consumers.
    /// </summary>
    Backward = 3,

    /// <summary>
    /// Full compatibility - schemas are fully compatible in both directions.
    /// Events can flow between versions without any issues.
    /// </summary>
    Full = 4
}

/// <summary>
/// Specific compatibility issue identified during schema analysis.
/// </summary>
public sealed record CompatibilityIssue
{
    /// <summary>
    /// The type of compatibility issue.
    /// </summary>
    public required CompatibilityIssueType IssueType { get; init; }

    /// <summary>
    /// Severity level of this compatibility issue.
    /// </summary>
    public required CompatibilityIssueSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable description of the issue.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Field or path in the schema where the issue was detected.
    /// Uses JSON path notation for nested fields.
    /// </summary>
    public string? FieldPath { get; init; }

    /// <summary>
    /// Expected value or constraint that was violated.
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Actual value found that caused the issue.
    /// </summary>
    public object? ActualValue { get; init; }

    /// <summary>
    /// Suggested resolution or workaround for this issue.
    /// </summary>
    public string? SuggestedResolution { get; init; }

    /// <summary>
    /// Additional context or metadata about the issue.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Create a field type mismatch issue.
    /// </summary>
    public static CompatibilityIssue TypeMismatch(
        string fieldPath,
        string expectedType,
        string actualType,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Error)
    {
        return new CompatibilityIssue
        {
            IssueType = CompatibilityIssueType.TypeMismatch,
            Severity = severity,
            Description = $"Field '{fieldPath}' type changed from '{expectedType}' to '{actualType}'",
            FieldPath = fieldPath,
            ExpectedValue = expectedType,
            ActualValue = actualType,
            SuggestedResolution = "Consider adding migration rule for type conversion"
        };
    }

    /// <summary>
    /// Create a missing field issue.
    /// </summary>
    public static CompatibilityIssue MissingField(
        string fieldPath,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Warning)
    {
        return new CompatibilityIssue
        {
            IssueType = CompatibilityIssueType.MissingField,
            Severity = severity,
            Description = $"Field '{fieldPath}' is missing in the new schema version",
            FieldPath = fieldPath,
            SuggestedResolution = "Add default value for backward compatibility or mark field as optional"
        };
    }

    /// <summary>
    /// Create an added field issue.
    /// </summary>
    public static CompatibilityIssue AddedField(
        string fieldPath,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Info)
    {
        return new CompatibilityIssue
        {
            IssueType = CompatibilityIssueType.AddedField,
            Severity = severity,
            Description = $"New field '{fieldPath}' added in the new schema version",
            FieldPath = fieldPath,
            SuggestedResolution = "Ensure field is optional for backward compatibility"
        };
    }

    /// <summary>
    /// Create a constraint violation issue.
    /// </summary>
    public static CompatibilityIssue ConstraintViolation(
        string fieldPath,
        string constraint,
        string description,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Warning)
    {
        return new CompatibilityIssue
        {
            IssueType = CompatibilityIssueType.ConstraintViolation,
            Severity = severity,
            Description = description,
            FieldPath = fieldPath,
            ExpectedValue = constraint,
            SuggestedResolution = "Review constraint changes for compatibility impact"
        };
    }
}

/// <summary>
/// Types of compatibility issues that can be detected.
/// </summary>
public enum CompatibilityIssueType
{
    /// <summary>
    /// Field type has changed between schema versions.
    /// </summary>
    TypeMismatch,

    /// <summary>
    /// Field is missing in the new schema version.
    /// </summary>
    MissingField,

    /// <summary>
    /// New field added in the new schema version.
    /// </summary>
    AddedField,

    /// <summary>
    /// Field constraint has changed (e.g., required to optional).
    /// </summary>
    ConstraintViolation,

    /// <summary>
    /// Enumeration values have changed.
    /// </summary>
    EnumValueChange,

    /// <summary>
    /// Array or object structure has changed.
    /// </summary>
    StructuralChange,

    /// <summary>
    /// Format or validation rules have changed.
    /// </summary>
    ValidationRuleChange,

    /// <summary>
    /// Custom compatibility rule violation.
    /// </summary>
    CustomRuleViolation
}

/// <summary>
/// Severity levels for compatibility issues.
/// </summary>
public enum CompatibilityIssueSeverity
{
    /// <summary>
    /// Informational - no action required but good to know.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - potential issue that should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - significant issue that will likely cause problems.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - breaking change that will definitely cause failures.
    /// </summary>
    Critical
}

/// <summary>
/// Performance metrics for compatibility analysis.
/// </summary>
public sealed record CompatibilityAnalysisMetrics
{
    /// <summary>
    /// Time taken to perform the compatibility analysis.
    /// </summary>
    public required TimeSpan AnalysisDuration { get; init; }

    /// <summary>
    /// Number of schema elements analyzed.
    /// </summary>
    public int ElementsAnalyzed { get; init; }

    /// <summary>
    /// Number of compatibility rules applied.
    /// </summary>
    public int RulesApplied { get; init; }

    /// <summary>
    /// Memory usage during analysis in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Version of the analysis engine used.
    /// </summary>
    public string? AnalysisEngineVersion { get; init; }

    /// <summary>
    /// Create metrics for a completed analysis.
    /// </summary>
    public static CompatibilityAnalysisMetrics Create(
        TimeSpan analysisDuration,
        int elementsAnalyzed = 0,
        int rulesApplied = 0,
        long memoryUsageBytes = 0,
        string? analysisEngineVersion = null)
    {
        return new CompatibilityAnalysisMetrics
        {
            AnalysisDuration = analysisDuration,
            ElementsAnalyzed = elementsAnalyzed,
            RulesApplied = rulesApplied,
            MemoryUsageBytes = memoryUsageBytes,
            AnalysisEngineVersion = analysisEngineVersion
        };
    }
}