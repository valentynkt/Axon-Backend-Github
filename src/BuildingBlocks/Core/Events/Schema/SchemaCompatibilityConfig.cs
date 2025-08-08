namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Configuration for schema compatibility requirements and validation.
/// Defines how strict compatibility checking should be for this schema version.
/// </summary>
public sealed record SchemaCompatibilityConfig
{
    /// <summary>
    /// Required compatibility level for this schema version.
    /// Determines how changes are validated against other versions.
    /// </summary>
    public CompatibilityLevel RequiredCompatibilityLevel { get; init; } = CompatibilityLevel.Backward;

    /// <summary>
    /// Whether to allow breaking changes for this schema version.
    /// Should typically be false for production schemas.
    /// </summary>
    public bool AllowBreakingChanges { get; init; }

    /// <summary>
    /// Specific fields that must remain compatible across versions.
    /// These fields cannot be removed or have their types changed.
    /// </summary>
    public IReadOnlyList<string>? RequiredCompatibleFields { get; init; }

    /// <summary>
    /// Fields that are allowed to have breaking changes.
    /// Useful for phasing out deprecated fields.
    /// </summary>
    public IReadOnlyList<string>? BreakingChangeAllowedFields { get; init; }

    /// <summary>
    /// Custom compatibility validation rules.
    /// Additional business logic for determining compatibility.
    /// </summary>
    public IReadOnlyList<CompatibilityValidationRule>? CustomValidationRules { get; init; }

    /// <summary>
    /// Whether to enforce strict type checking during compatibility validation.
    /// When true, type changes (e.g., int to long) are considered breaking.
    /// </summary>
    public bool StrictTypeValidation { get; init; } = true;

    /// <summary>
    /// Create a simple compatibility config with basic settings.
    /// </summary>
    public static SchemaCompatibilityConfig Simple(
        CompatibilityLevel requiredLevel = CompatibilityLevel.Backward,
        bool allowBreakingChanges = false,
        bool strictTypeValidation = true)
    {
        return new SchemaCompatibilityConfig
        {
            RequiredCompatibilityLevel = requiredLevel,
            AllowBreakingChanges = allowBreakingChanges,
            StrictTypeValidation = strictTypeValidation
        };
    }

    /// <summary>
    /// Create a strict compatibility config for production environments.
    /// </summary>
    public static SchemaCompatibilityConfig Strict(params string[] requiredCompatibleFields)
    {
        return new SchemaCompatibilityConfig
        {
            RequiredCompatibilityLevel = CompatibilityLevel.Full,
            AllowBreakingChanges = false,
            StrictTypeValidation = true,
            RequiredCompatibleFields = requiredCompatibleFields
        };
    }

    /// <summary>
    /// Create a lenient compatibility config for development environments.
    /// </summary>
    public static SchemaCompatibilityConfig Lenient()
    {
        return new SchemaCompatibilityConfig
        {
            RequiredCompatibilityLevel = CompatibilityLevel.None,
            AllowBreakingChanges = true,
            StrictTypeValidation = false
        };
    }
}

/// <summary>
/// Custom validation rule for schema compatibility checking.
/// </summary>
public sealed record CompatibilityValidationRule
{
    /// <summary>
    /// Name of this validation rule for identification.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Description of what this rule validates.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The validation logic to apply.
    /// </summary>
    public required CompatibilityRuleType RuleType { get; init; }

    /// <summary>
    /// Target field or path this rule applies to.
    /// Can use JSON path expressions for complex targeting.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Expected value or condition for validation.
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Custom validation expression or script.
    /// </summary>
    public string? ValidationExpression { get; init; }

    /// <summary>
    /// Severity level if this rule fails validation.
    /// </summary>
    public CompatibilityIssueSeverity Severity { get; init; } = CompatibilityIssueSeverity.Error;

    /// <summary>
    /// Create a field existence validation rule.
    /// </summary>
    public static CompatibilityValidationRule RequiredField(
        string fieldName,
        string? description = null,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Error)
        => new()
        {
            RuleName = $"RequiredField_{fieldName}",
            Description = description ?? $"Field '{fieldName}' is required for compatibility",
            RuleType = CompatibilityRuleType.FieldExists,
            TargetPath = fieldName,
            Severity = severity
        };

    /// <summary>
    /// Create a type consistency validation rule.
    /// </summary>
    public static CompatibilityValidationRule ConsistentType(
        string fieldName,
        string expectedType,
        string? description = null,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Error)
        => new()
        {
            RuleName = $"ConsistentType_{fieldName}",
            Description = description ?? $"Field '{fieldName}' must maintain type '{expectedType}'",
            RuleType = CompatibilityRuleType.TypeConsistency,
            TargetPath = fieldName,
            ExpectedValue = expectedType,
            Severity = severity
        };

    /// <summary>
    /// Create a custom validation rule with expression.
    /// </summary>
    public static CompatibilityValidationRule CustomRule(
        string ruleName,
        string validationExpression,
        string? description = null,
        string? targetPath = null,
        CompatibilityIssueSeverity severity = CompatibilityIssueSeverity.Error)
        => new()
        {
            RuleName = ruleName,
            Description = description,
            RuleType = CompatibilityRuleType.CustomExpression,
            TargetPath = targetPath,
            ValidationExpression = validationExpression,
            Severity = severity
        };
}

/// <summary>
/// Types of compatibility validation rules.
/// </summary>
public enum CompatibilityRuleType
{
    /// <summary>
    /// Validates that a field exists in both schemas.
    /// </summary>
    FieldExists,

    /// <summary>
    /// Validates that field types remain consistent.
    /// </summary>
    TypeConsistency,

    /// <summary>
    /// Validates that field constraints are compatible.
    /// </summary>
    ConstraintCompatibility,

    /// <summary>
    /// Custom validation using expressions or scripts.
    /// </summary>
    CustomExpression,

    /// <summary>
    /// Validates enumeration value compatibility.
    /// </summary>
    EnumCompatibility,

    /// <summary>
    /// Validates nested object structure compatibility.
    /// </summary>
    NestedStructureCompatibility
}