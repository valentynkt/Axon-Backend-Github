namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Result of validating event data against a schema.
/// Provides detailed information about validation success, failures, and warnings.
/// </summary>
public sealed record SchemaValidationResult
{
    /// <summary>
    /// Whether the event data passed validation.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// The event type that was validated.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The schema version used for validation.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// List of validation errors found in the event data.
    /// Empty if validation passed.
    /// </summary>
    public required IReadOnlyList<ValidationError> Errors { get; init; }

    /// <summary>
    /// List of validation warnings - issues that don't prevent validation
    /// but should be reviewed.
    /// </summary>
    public IReadOnlyList<ValidationWarning>? Warnings { get; init; }

    /// <summary>
    /// Performance metrics for the validation operation.
    /// </summary>
    public ValidationMetrics? Metrics { get; init; }

    /// <summary>
    /// Additional context about the validation process.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ValidationContext { get; init; }

    /// <summary>
    /// When the validation was performed (UTC).
    /// </summary>
    public DateTime ValidatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Version of the validation engine used.
    /// </summary>
    public string? ValidatorVersion { get; init; }

    /// <summary>
    /// Create a successful validation result.
    /// </summary>
    public static SchemaValidationResult Success(
        string eventType,
        int schemaVersion,
        IReadOnlyList<ValidationWarning>? warnings = null,
        ValidationMetrics? metrics = null,
        IReadOnlyDictionary<string, object>? context = null,
        string? validatorVersion = null)
    {
        return new SchemaValidationResult
        {
            IsValid = true,
            EventType = eventType,
            SchemaVersion = schemaVersion,
            Errors = Array.Empty<ValidationError>(),
            Warnings = warnings,
            Metrics = metrics,
            ValidationContext = context,
            ValidatorVersion = validatorVersion
        };
    }

    /// <summary>
    /// Create a failed validation result with errors.
    /// </summary>
    public static SchemaValidationResult Failure(
        string eventType,
        int schemaVersion,
        IReadOnlyList<ValidationError> errors,
        IReadOnlyList<ValidationWarning>? warnings = null,
        ValidationMetrics? metrics = null,
        IReadOnlyDictionary<string, object>? context = null,
        string? validatorVersion = null)
    {
        return new SchemaValidationResult
        {
            IsValid = false,
            EventType = eventType,
            SchemaVersion = schemaVersion,
            Errors = errors,
            Warnings = warnings,
            Metrics = metrics,
            ValidationContext = context,
            ValidatorVersion = validatorVersion
        };
    }

    /// <summary>
    /// Create a validation result from a single error.
    /// </summary>
    public static SchemaValidationResult SingleError(
        string eventType,
        int schemaVersion,
        ValidationError error,
        ValidationMetrics? metrics = null)
    {
        return Failure(
            eventType,
            schemaVersion,
            new[] { error },
            null,
            metrics);
    }

    /// <summary>
    /// Get the count of critical validation errors.
    /// </summary>
    public int CriticalErrorCount => Errors.Count(e => e.Severity == ValidationErrorSeverity.Critical);

    /// <summary>
    /// Get the count of non-critical validation errors.
    /// </summary>
    public int ErrorCount => Errors.Count(e => e.Severity == ValidationErrorSeverity.Error);

    /// <summary>
    /// Get the count of validation warnings.
    /// </summary>
    public int WarningCount => Warnings?.Count ?? 0;

    /// <summary>
    /// Check if there are any critical errors that require immediate attention.
    /// </summary>
    public bool HasCriticalErrors => CriticalErrorCount > 0;
}

/// <summary>
/// Specific validation error found in event data.
/// </summary>
public sealed record ValidationError
{
    /// <summary>
    /// Severity level of this validation error.
    /// </summary>
    public required ValidationErrorSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// JSON path to the field that caused the error.
    /// Uses JSONPath notation for nested fields.
    /// </summary>
    public string? FieldPath { get; init; }

    /// <summary>
    /// The value that failed validation.
    /// </summary>
    public object? InvalidValue { get; init; }

    /// <summary>
    /// The expected value or constraint that was violated.
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Suggested fix for the validation error.
    /// </summary>
    public string? SuggestedFix { get; init; }

    /// <summary>
    /// Additional context about this error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Create a validation error for a missing required field.
    /// </summary>
    public static ValidationError MissingRequiredField(
        string fieldPath,
        string? suggestedFix = null)
    {
        return new ValidationError
        {
            Severity = ValidationErrorSeverity.Error,
            Message = $"Required field '{fieldPath}' is missing",
            FieldPath = fieldPath,
            ErrorCode = "MISSING_REQUIRED_FIELD",
            SuggestedFix = suggestedFix ?? $"Provide a value for the '{fieldPath}' field"
        };
    }

    /// <summary>
    /// Create a validation error for type mismatch.
    /// </summary>
    public static ValidationError TypeMismatch(
        string fieldPath,
        object invalidValue,
        string expectedType,
        ValidationErrorSeverity severity = ValidationErrorSeverity.Error)
    {
        return new ValidationError
        {
            Severity = severity,
            Message = $"Field '{fieldPath}' has incorrect type. Expected {expectedType}, got {invalidValue?.GetType().Name ?? "null"}",
            FieldPath = fieldPath,
            InvalidValue = invalidValue,
            ExpectedValue = expectedType,
            ErrorCode = "TYPE_MISMATCH",
            SuggestedFix = $"Ensure '{fieldPath}' is of type {expectedType}"
        };
    }

    /// <summary>
    /// Create a validation error for constraint violation.
    /// </summary>
    public static ValidationError ConstraintViolation(
        string fieldPath,
        object invalidValue,
        string constraint,
        string message,
        ValidationErrorSeverity severity = ValidationErrorSeverity.Error)
    {
        return new ValidationError
        {
            Severity = severity,
            Message = message,
            FieldPath = fieldPath,
            InvalidValue = invalidValue,
            ExpectedValue = constraint,
            ErrorCode = "CONSTRAINT_VIOLATION",
            SuggestedFix = $"Ensure '{fieldPath}' meets the constraint: {constraint}"
        };
    }

    /// <summary>
    /// Create a validation error for format violation.
    /// </summary>
    public static ValidationError FormatViolation(
        string fieldPath,
        object invalidValue,
        string expectedFormat,
        string? suggestedFix = null)
    {
        return new ValidationError
        {
            Severity = ValidationErrorSeverity.Error,
            Message = $"Field '{fieldPath}' does not match the expected format '{expectedFormat}'",
            FieldPath = fieldPath,
            InvalidValue = invalidValue,
            ExpectedValue = expectedFormat,
            ErrorCode = "FORMAT_VIOLATION",
            SuggestedFix = suggestedFix ?? $"Ensure '{fieldPath}' follows the format: {expectedFormat}"
        };
    }

    /// <summary>
    /// Create a critical validation error.
    /// </summary>
    public static ValidationError Critical(
        string message,
        string? fieldPath = null,
        string? errorCode = null,
        object? invalidValue = null)
    {
        return new ValidationError
        {
            Severity = ValidationErrorSeverity.Critical,
            Message = message,
            FieldPath = fieldPath,
            ErrorCode = errorCode ?? "CRITICAL_ERROR",
            InvalidValue = invalidValue
        };
    }
}

/// <summary>
/// Severity levels for validation errors.
/// </summary>
public enum ValidationErrorSeverity
{
    /// <summary>
    /// Minor issue that doesn't prevent processing.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that should be addressed but doesn't prevent processing.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that prevents processing and requires immediate attention.
    /// </summary>
    Critical
}

/// <summary>
/// Validation warning for non-blocking issues.
/// </summary>
public sealed record ValidationWarning
{
    /// <summary>
    /// Human-readable warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// JSON path to the field that triggered the warning.
    /// </summary>
    public string? FieldPath { get; init; }

    /// <summary>
    /// Warning code for programmatic handling.
    /// </summary>
    public string? WarningCode { get; init; }

    /// <summary>
    /// The value that triggered the warning.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Suggested improvement or fix for the warning.
    /// </summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Additional context about this warning.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Create a deprecation warning.
    /// </summary>
    public static ValidationWarning Deprecation(
        string fieldPath,
        string suggestion,
        object? value = null)
    {
        return new ValidationWarning
        {
            Message = $"Field '{fieldPath}' is deprecated and should be avoided",
            FieldPath = fieldPath,
            WarningCode = "DEPRECATED_FIELD",
            Value = value,
            Suggestion = suggestion
        };
    }

    /// <summary>
    /// Create a warning for unknown properties.
    /// </summary>
    public static ValidationWarning UnknownProperty(
        string fieldPath,
        object value)
    {
        return new ValidationWarning
        {
            Message = $"Unknown property '{fieldPath}' found in event data",
            FieldPath = fieldPath,
            WarningCode = "UNKNOWN_PROPERTY",
            Value = value,
            Suggestion = "Consider removing this property or updating the schema to include it"
        };
    }

    /// <summary>
    /// Create a performance warning.
    /// </summary>
    public static ValidationWarning Performance(
        string message,
        string suggestion,
        string? fieldPath = null)
    {
        return new ValidationWarning
        {
            Message = message,
            FieldPath = fieldPath,
            WarningCode = "PERFORMANCE_WARNING",
            Suggestion = suggestion
        };
    }
}

/// <summary>
/// Performance metrics for validation operations.
/// </summary>
public sealed record ValidationMetrics
{
    /// <summary>
    /// Time taken to perform the validation.
    /// </summary>
    public required TimeSpan ValidationDuration { get; init; }

    /// <summary>
    /// Size of the event data being validated in bytes.
    /// </summary>
    public long EventDataSizeBytes { get; init; }

    /// <summary>
    /// Number of fields validated in the event data.
    /// </summary>
    public int FieldsValidated { get; init; }

    /// <summary>
    /// Number of validation rules applied.
    /// </summary>
    public int RulesApplied { get; init; }

    /// <summary>
    /// Memory usage during validation in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Whether validation result was retrieved from cache.
    /// </summary>
    public bool CacheHit { get; init; }

    /// <summary>
    /// Cache key used for this validation (if applicable).
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// Create basic validation metrics.
    /// </summary>
    public static ValidationMetrics Create(
        TimeSpan validationDuration,
        long eventDataSizeBytes = 0,
        int fieldsValidated = 0,
        int rulesApplied = 0,
        long memoryUsageBytes = 0,
        bool cacheHit = false,
        string? cacheKey = null)
    {
        return new ValidationMetrics
        {
            ValidationDuration = validationDuration,
            EventDataSizeBytes = eventDataSizeBytes,
            FieldsValidated = fieldsValidated,
            RulesApplied = rulesApplied,
            MemoryUsageBytes = memoryUsageBytes,
            CacheHit = cacheHit,
            CacheKey = cacheKey
        };
    }

    /// <summary>
    /// Check if validation performance is within acceptable limits.
    /// </summary>
    public bool IsPerformanceAcceptable(TimeSpan maxDuration = default, long maxMemoryBytes = 0)
    {
        var maxDurationCheck = maxDuration == default || ValidationDuration <= maxDuration;
        var maxMemoryCheck = maxMemoryBytes == 0 || MemoryUsageBytes <= maxMemoryBytes;
        
        return maxDurationCheck && maxMemoryCheck;
    }

    /// <summary>
    /// Get validation throughput in fields per second.
    /// </summary>
    public double GetValidationThroughput()
    {
        if (ValidationDuration.TotalSeconds == 0 || FieldsValidated == 0)
            return 0;

        return FieldsValidated / ValidationDuration.TotalSeconds;
    }
}