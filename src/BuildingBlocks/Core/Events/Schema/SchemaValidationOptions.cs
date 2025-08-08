namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Configuration options for schema validation behavior.
/// Controls how strictly schemas are validated and what violations are allowed.
/// </summary>
public sealed record SchemaValidationOptions
{
    /// <summary>
    /// Whether to validate event data against the schema.
    /// When false, schemas are stored but not enforced.
    /// </summary>
    public bool ValidateEventData { get; init; } = true;

    /// <summary>
    /// Whether to allow unknown properties in event data.
    /// When true, extra properties are ignored rather than causing validation failure.
    /// </summary>
    public bool AllowUnknownProperties { get; init; }

    /// <summary>
    /// Whether to perform strict type validation.
    /// When false, some type coercions are allowed (e.g., string to number).
    /// </summary>
    public bool StrictTypeValidation { get; init; } = true;

    /// <summary>
    /// Maximum depth for nested object validation.
    /// Prevents stack overflow on deeply nested structures.
    /// </summary>
    public int MaxValidationDepth { get; init; } = 10;

    /// <summary>
    /// Validation mode for arrays and collections.
    /// </summary>
    public ArrayValidationMode ArrayValidationMode { get; init; } = ArrayValidationMode.ValidateAllItems;

    /// <summary>
    /// Custom validation rules to apply in addition to schema validation.
    /// </summary>
    public IReadOnlyList<CustomValidationRule>? CustomValidationRules { get; init; }

    /// <summary>
    /// Whether to cache validation results for performance.
    /// Recommended for high-throughput scenarios.
    /// </summary>
    public bool EnableValidationCaching { get; init; }

    /// <summary>
    /// How long to cache validation results (if caching is enabled).
    /// </summary>
    public TimeSpan ValidationCacheDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Action to take when validation fails.
    /// </summary>
    public ValidationFailureAction FailureAction { get; init; } = ValidationFailureAction.ReturnError;

    /// <summary>
    /// Create validation options for strict production environments.
    /// </summary>
    public static SchemaValidationOptions Strict()
    {
        return new SchemaValidationOptions
        {
            ValidateEventData = true,
            AllowUnknownProperties = false,
            StrictTypeValidation = true,
            MaxValidationDepth = 10,
            ArrayValidationMode = ArrayValidationMode.ValidateAllItems,
            EnableValidationCaching = true,
            ValidationCacheDuration = TimeSpan.FromMinutes(15),
            FailureAction = ValidationFailureAction.ReturnError
        };
    }

    /// <summary>
    /// Create validation options for lenient development environments.
    /// </summary>
    public static SchemaValidationOptions Lenient()
    {
        return new SchemaValidationOptions
        {
            ValidateEventData = true,
            AllowUnknownProperties = true,
            StrictTypeValidation = false,
            MaxValidationDepth = 5,
            ArrayValidationMode = ArrayValidationMode.ValidateFirstItem,
            EnableValidationCaching = false,
            FailureAction = ValidationFailureAction.LogWarning
        };
    }

    /// <summary>
    /// Create validation options with validation disabled.
    /// </summary>
    public static SchemaValidationOptions Disabled()
    {
        return new SchemaValidationOptions
        {
            ValidateEventData = false,
            AllowUnknownProperties = true,
            StrictTypeValidation = false,
            EnableValidationCaching = false,
            FailureAction = ValidationFailureAction.Ignore
        };
    }
}

/// <summary>
/// Custom validation rule for additional business logic validation.
/// </summary>
public sealed record CustomValidationRule
{
    /// <summary>
    /// Name of this validation rule.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Description of what this rule validates.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON path expression for the field(s) this rule validates.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Validation expression or pattern to apply.
    /// </summary>
    public required string ValidationExpression { get; init; }

    /// <summary>
    /// Error message to show when validation fails.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Severity of validation failure.
    /// </summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

    /// <summary>
    /// Create a regex validation rule.
    /// </summary>
    public static CustomValidationRule Regex(
        string ruleName,
        string targetPath,
        string pattern,
        string errorMessage,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        return new CustomValidationRule
        {
            RuleName = ruleName,
            TargetPath = targetPath,
            ValidationExpression = $"regex:{pattern}",
            ErrorMessage = errorMessage,
            Severity = severity
        };
    }

    /// <summary>
    /// Create a range validation rule for numeric values.
    /// </summary>
    public static CustomValidationRule Range(
        string ruleName,
        string targetPath,
        double min,
        double max,
        string? errorMessage = null,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        return new CustomValidationRule
        {
            RuleName = ruleName,
            TargetPath = targetPath,
            ValidationExpression = $"range:{min},{max}",
            ErrorMessage = errorMessage ?? $"Value must be between {min} and {max}",
            Severity = severity
        };
    }

    /// <summary>
    /// Create a length validation rule for strings.
    /// </summary>
    public static CustomValidationRule StringLength(
        string ruleName,
        string targetPath,
        int minLength,
        int maxLength,
        string? errorMessage = null,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        return new CustomValidationRule
        {
            RuleName = ruleName,
            TargetPath = targetPath,
            ValidationExpression = $"length:{minLength},{maxLength}",
            ErrorMessage = errorMessage ?? $"String length must be between {minLength} and {maxLength} characters",
            Severity = severity
        };
    }
}

/// <summary>
/// How to validate arrays and collections.
/// </summary>
public enum ArrayValidationMode
{
    /// <summary>
    /// Validate all items in arrays (thorough but slower).
    /// </summary>
    ValidateAllItems,

    /// <summary>
    /// Only validate the first item in arrays (faster).
    /// </summary>
    ValidateFirstItem,

    /// <summary>
    /// Only validate array structure, not individual items.
    /// </summary>
    ValidateStructureOnly,

    /// <summary>
    /// Skip array validation entirely.
    /// </summary>
    SkipArrayValidation
}

/// <summary>
/// Action to take when validation fails.
/// </summary>
public enum ValidationFailureAction
{
    /// <summary>
    /// Return validation error in Result.
    /// </summary>
    ReturnError,

    /// <summary>
    /// Log warning and continue.
    /// </summary>
    LogWarning,

    /// <summary>
    /// Throw validation exception.
    /// </summary>
    ThrowException,

    /// <summary>
    /// Ignore validation failure completely.
    /// </summary>
    Ignore
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Information only - no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - should be reviewed but not blocking.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - validation failure that should block processing.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - severe validation failure.
    /// </summary>
    Critical
}