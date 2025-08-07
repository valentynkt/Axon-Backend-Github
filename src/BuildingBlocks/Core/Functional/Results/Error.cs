namespace BuildingBlocks.Core.Functional;

/// <summary>
/// Immutable error record with comprehensive factory methods.
/// Designed for railway-oriented programming and error accumulation.
/// </summary>
public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public string? Details { get; }
    public ErrorType Type { get; }
    public Exception? Exception { get; }
    public Dictionary<string, object> Metadata { get; }

    private Error(
        string code,
        string message,
        ErrorType type,
        string? details = null,
        Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Details = details;
        Exception = exception;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    #region Factory Methods

    /// <summary>
    /// Create a validation error
    /// </summary>
    public static Error Validation(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "VALIDATION_FAILED",
            message,
            ErrorType.Validation,
            details);
    }

    /// <summary>
    /// Create a business rule violation error
    /// </summary>
    public static Error BusinessRule(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "BUSINESS_RULE_VIOLATION",
            message,
            ErrorType.BusinessRule,
            details);
    }

    /// <summary>
    /// Create an aggregate/domain error
    /// </summary>
    public static Error Aggregate(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "AGGREGATE_ERROR",
            message,
            ErrorType.Aggregate,
            details);
    }

    /// <summary>
    /// Create a not found error
    /// </summary>
    public static Error NotFound(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "NOT_FOUND",
            message,
            ErrorType.NotFound,
            details);
    }

    /// <summary>
    /// Create a conflict error
    /// </summary>
    public static Error Conflict(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "CONFLICT",
            message,
            ErrorType.Conflict,
            details);
    }

    /// <summary>
    /// Create a cancellation error
    /// </summary>
    public static Error Cancelled(string message = "Operation was cancelled", string? code = null)
    {
        return new Error(
            code ?? "OPERATION_CANCELLED",
            message,
            ErrorType.Cancellation);
    }

    /// <summary>
    /// Create an authorization error
    /// </summary>
    public static Error Unauthorized(string message = "Access denied", string? code = null, string? details = null)
    {
        return new Error(
            code ?? "UNAUTHORIZED",
            message,
            ErrorType.Authorization,
            details);
    }

    /// <summary>
    /// Create a system/infrastructure error
    /// </summary>
    public static Error System(string message, string? code = null, string? details = null)
    {
        return new Error(
            code ?? "SYSTEM_ERROR",
            message,
            ErrorType.System,
            details);
    }

    /// <summary>
    /// Create error from exception with proper categorization
    /// </summary>
    public static Error FromException(Exception exception, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errorType = exception switch
        {
            ArgumentException or ArgumentNullException => ErrorType.Validation,
            UnauthorizedAccessException => ErrorType.Authorization,
            OperationCanceledException => ErrorType.Cancellation,
            NotImplementedException => ErrorType.System,
            _ => ErrorType.System
        };

        return new Error(
            code ?? exception.GetType().Name.Replace("Exception", "").ToUpperInvariant(),
            exception.Message,
            errorType,
            exception.StackTrace,
            exception);
    }

    /// <summary>
    /// Create an aggregated error from multiple errors
    /// </summary>
    public static Error Aggregate(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));

        if (errors.Length == 1)
            return errors[0];

        var messages = errors.Select(e => e.Message).ToArray();
        var combinedMessage = string.Join("; ", messages);

        var metadata = new Dictionary<string, object>
        {
            ["ErrorCount"] = errors.Length,
            ["Errors"] = errors.Select(e => new { e.Code, e.Message, e.Type }).ToArray()
        };

        return new Error(
            "AGGREGATE_ERROR",
            $"Multiple errors occurred: {combinedMessage}",
            ErrorType.Aggregate,
            string.Join("\n", errors.Select(e => $"- {e.Code}: {e.Message}")),
            metadata: metadata);
    }

    /// <summary>
    /// Create error with custom metadata
    /// </summary>
    public static Error WithMetadata(
        string code,
        string message,
        ErrorType type,
        Dictionary<string, object> metadata)
    {
        return new Error(code, message, type, metadata: metadata);
    }

    #endregion

    #region Fluent Configuration

    /// <summary>
    /// Add details to the error
    /// </summary>
    public Error WithDetails(string details)
    {
        return this with { Details = details };
    }

    /// <summary>
    /// Add metadata to the error
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Add exception context to the error
    /// </summary>
    public Error WithException(Exception exception)
    {
        return this with { Exception = exception };
    }

    #endregion

    public override string ToString() => $"[{Type}] {Code}: {Message}";
}

/// <summary>
/// Error type enumeration for categorization and handling
/// </summary>
public enum ErrorType
{
    Validation = 1,
    BusinessRule = 2,
    Aggregate = 3,
    NotFound = 4,
    Conflict = 5,
    Authorization = 6,
    Cancellation = 7,
    System = 8
}