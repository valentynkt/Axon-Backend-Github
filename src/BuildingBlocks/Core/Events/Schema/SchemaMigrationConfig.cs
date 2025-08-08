namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Configuration for migrating events between schema versions.
/// Defines transformation rules and migration strategies for data evolution.
/// </summary>
public sealed record SchemaMigrationConfig
{
    /// <summary>
    /// Collection of transformation rules to apply during migration.
    /// Rules are applied in sequence to transform the event data.
    /// </summary>
    public IReadOnlyList<MigrationRule> TransformationRules { get; init; } = Array.Empty<MigrationRule>();

    /// <summary>
    /// Migration strategy to use for this transformation.
    /// Determines how the migration is applied and validated.
    /// </summary>
    public MigrationStrategy Strategy { get; init; } = MigrationStrategy.Automatic;

    /// <summary>
    /// Whether to validate the migrated data against the target schema.
    /// Recommended for production environments.
    /// </summary>
    public bool ValidateAfterMigration { get; init; } = true;

    /// <summary>
    /// Custom migration script or transformation logic.
    /// Can be JavaScript, C# expression, or other supported formats.
    /// </summary>
    public string? CustomMigrationScript { get; init; }

    /// <summary>
    /// The script language for custom migration logic.
    /// </summary>
    public MigrationScriptLanguage? ScriptLanguage { get; init; }

    /// <summary>
    /// Whether to preserve unknown properties during migration.
    /// Useful for forward compatibility scenarios.
    /// </summary>
    public bool PreserveUnknownProperties { get; init; }

    /// <summary>
    /// Rollback configuration for this migration.
    /// Defines how to reverse the migration if needed.
    /// </summary>
    public RollbackConfig? RollbackConfig { get; init; }

    /// <summary>
    /// Create a simple migration config with basic rules.
    /// </summary>
    public static SchemaMigrationConfig Simple(
        MigrationStrategy strategy = MigrationStrategy.Automatic,
        bool validateAfterMigration = true,
        bool preserveUnknownProperties = false,
        params MigrationRule[] rules)
    {
        return new SchemaMigrationConfig
        {
            TransformationRules = rules.ToArray(),
            Strategy = strategy,
            ValidateAfterMigration = validateAfterMigration,
            PreserveUnknownProperties = preserveUnknownProperties
        };
    }

    /// <summary>
    /// Create a migration config with custom script.
    /// </summary>
    public static SchemaMigrationConfig WithScript(
        string customScript,
        MigrationScriptLanguage scriptLanguage,
        MigrationStrategy strategy = MigrationStrategy.Custom,
        bool validateAfterMigration = true,
        RollbackConfig? rollbackConfig = null)
    {
        return new SchemaMigrationConfig
        {
            CustomMigrationScript = customScript,
            ScriptLanguage = scriptLanguage,
            Strategy = strategy,
            ValidateAfterMigration = validateAfterMigration,
            RollbackConfig = rollbackConfig
        };
    }
}

/// <summary>
/// Individual migration rule for transforming specific parts of event data.
/// </summary>
public sealed record MigrationRule
{
    /// <summary>
    /// Type of transformation to apply.
    /// </summary>
    public required MigrationRuleType RuleType { get; init; }

    /// <summary>
    /// JSON path or property name to target for transformation.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// The transformation operation to perform.
    /// </summary>
    public required MigrationOperation Operation { get; init; }

    /// <summary>
    /// Value to use for the operation (e.g., default value, new name).
    /// </summary>
    public object? OperationValue { get; init; }

    /// <summary>
    /// Condition that must be met for this rule to apply.
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Description of what this rule does for documentation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Create a property rename rule.
    /// </summary>
    public static MigrationRule RenameProperty(string oldName, string newName, string? description = null)
        => new()
        {
            RuleType = MigrationRuleType.PropertyTransformation,
            TargetPath = oldName,
            Operation = MigrationOperation.Rename,
            OperationValue = newName,
            Description = description ?? $"Rename property '{oldName}' to '{newName}'"
        };

    /// <summary>
    /// Create a property removal rule.
    /// </summary>
    public static MigrationRule RemoveProperty(string propertyName, string? description = null)
        => new()
        {
            RuleType = MigrationRuleType.PropertyTransformation,
            TargetPath = propertyName,
            Operation = MigrationOperation.Remove,
            Description = description ?? $"Remove property '{propertyName}'"
        };

    /// <summary>
    /// Create a default value rule for missing properties.
    /// </summary>
    public static MigrationRule AddDefaultValue(string propertyName, object defaultValue, string? description = null)
        => new()
        {
            RuleType = MigrationRuleType.PropertyTransformation,
            TargetPath = propertyName,
            Operation = MigrationOperation.AddDefault,
            OperationValue = defaultValue,
            Description = description ?? $"Add default value for property '{propertyName}'"
        };

    /// <summary>
    /// Create a type conversion rule.
    /// </summary>
    public static MigrationRule ConvertType(string propertyName, string targetType, string? description = null)
        => new()
        {
            RuleType = MigrationRuleType.TypeConversion,
            TargetPath = propertyName,
            Operation = MigrationOperation.Convert,
            OperationValue = targetType,
            Description = description ?? $"Convert property '{propertyName}' to type '{targetType}'"
        };
}

/// <summary>
/// Strategy for applying migration transformations.
/// </summary>
public enum MigrationStrategy
{
    /// <summary>
    /// Apply transformations automatically using predefined rules.
    /// </summary>
    Automatic,

    /// <summary>
    /// Use custom migration script for complex transformations.
    /// </summary>
    Custom,

    /// <summary>
    /// Manual migration - requires explicit handling by application code.
    /// </summary>
    Manual,

    /// <summary>
    /// Strict mode - migration fails if any rule cannot be applied.
    /// </summary>
    Strict,

    /// <summary>
    /// Best effort - continue migration even if some rules fail.
    /// </summary>
    BestEffort
}

/// <summary>
/// Type of migration rule for categorizing transformations.
/// </summary>
public enum MigrationRuleType
{
    /// <summary>
    /// Rule operates on object properties.
    /// </summary>
    PropertyTransformation,

    /// <summary>
    /// Rule performs type conversions.
    /// </summary>
    TypeConversion,

    /// <summary>
    /// Rule validates data integrity.
    /// </summary>
    Validation,

    /// <summary>
    /// Custom rule with user-defined logic.
    /// </summary>
    Custom
}

/// <summary>
/// Specific migration operations that can be performed.
/// </summary>
public enum MigrationOperation
{
    /// <summary>
    /// Add a new property with default value.
    /// </summary>
    AddDefault,

    /// <summary>
    /// Remove an existing property.
    /// </summary>
    Remove,

    /// <summary>
    /// Rename a property.
    /// </summary>
    Rename,

    /// <summary>
    /// Convert property to different type.
    /// </summary>
    Convert,

    /// <summary>
    /// Transform property value using expression.
    /// </summary>
    Transform,

    /// <summary>
    /// Validate property meets certain criteria.
    /// </summary>
    Validate,

    /// <summary>
    /// Custom operation defined by user logic.
    /// </summary>
    Custom
}

/// <summary>
/// Supported scripting languages for custom migrations.
/// </summary>
public enum MigrationScriptLanguage
{
    /// <summary>
    /// JavaScript for flexible transformations.
    /// </summary>
    JavaScript,

    /// <summary>
    /// C# expressions for type-safe transformations.
    /// </summary>
    CSharpExpression,

    /// <summary>
    /// SQL-like transformations for data operations.
    /// </summary>
    SqlLike,

    /// <summary>
    /// JSONPath expressions for JSON transformations.
    /// </summary>
    JsonPath
}

/// <summary>
/// Configuration for rolling back migrations.
/// </summary>
public sealed record RollbackConfig
{
    /// <summary>
    /// Whether rollback is supported for this migration.
    /// </summary>
    public bool IsSupported { get; init; } = true;

    /// <summary>
    /// Reverse transformation rules for rollback.
    /// </summary>
    public IReadOnlyList<MigrationRule>? ReverseRules { get; init; }

    /// <summary>
    /// Custom rollback script if needed.
    /// </summary>
    public string? CustomRollbackScript { get; init; }

    /// <summary>
    /// Maximum time allowed for rollback operation.
    /// </summary>
    public TimeSpan? TimeoutDuration { get; init; }

    /// <summary>
    /// Whether to validate data after rollback.
    /// </summary>
    public bool ValidateAfterRollback { get; init; } = true;
}