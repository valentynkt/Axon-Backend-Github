// This file is DEPRECATED - Use BuildingBlocks.Core.Diagnostics.Errors.Error instead
// Redirecting for backward compatibility

global using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Functional;

/// <summary>
/// DEPRECATED: This class has moved to BuildingBlocks.Core.Diagnostics.Errors.Error
/// This file provides backward compatibility redirects.
/// Please update your using statements to: using BuildingBlocks.Core.Diagnostics.Errors;
/// </summary>
[Obsolete("Use BuildingBlocks.Core.Diagnostics.Errors.Error instead. This redirect will be removed in a future version.", false)]
public sealed record Error
{
    // Redirect all properties to new Error type
    public string Code { get; }
    public string Message { get; }
    public string? Details { get; }
    public BuildingBlocks.Core.Diagnostics.Errors.ErrorType Type { get; }
    public Exception? Exception { get; }
    public Dictionary<string, object> Metadata { get; }

    private Error(
        string code,
        string message,
        BuildingBlocks.Core.Diagnostics.Errors.ErrorType type,
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

    // Factory methods redirecting to new Error class
    public static BuildingBlocks.Core.Diagnostics.Errors.Error Validation(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Validation(message, code ?? "VALIDATION_FAILED");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error BusinessRule(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.BusinessRule(message, code ?? "BUSINESS_RULE_VIOLATION");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error Aggregate(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Aggregate(message, code ?? "AGGREGATE_ERROR");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error NotFound(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.NotFound(message, code ?? "NOT_FOUND");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error Conflict(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Conflict(message, code ?? "CONFLICT");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error Cancelled(string message = "Operation was cancelled", string? code = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Cancelled(message, code ?? "OPERATION_CANCELLED");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error Unauthorized(string message = "Access denied", string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Unauthorized(message, code ?? "UNAUTHORIZED");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error System(string message, string? code = null, string? details = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Internal(message, code ?? "SYSTEM_ERROR");
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error FromException(Exception exception, string? code = null)
    {
        return BuildingBlocks.Core.Diagnostics.Errors.Error.FromException(exception);
    }

    public static BuildingBlocks.Core.Diagnostics.Errors.Error Aggregate(params Error[] errors)
    {
        var newErrors = errors.Select(e => BuildingBlocks.Core.Diagnostics.Errors.Error.Internal(e.Message, e.Code)).ToArray();
        return BuildingBlocks.Core.Diagnostics.Errors.Error.Aggregate(newErrors);
    }
}

/// <summary>
/// DEPRECATED: This enum has moved to BuildingBlocks.Core.Diagnostics.Errors.ErrorType
/// </summary>
[Obsolete("Use BuildingBlocks.Core.Diagnostics.Errors.ErrorType instead", false)]
public enum ErrorType
{
    Validation = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Validation,
    BusinessRule = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.BusinessRule,
    Aggregate = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Aggregate,
    NotFound = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.NotFound,
    Conflict = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Conflict,
    Authorization = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Unauthorized,
    Cancellation = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Cancelled,
    System = BuildingBlocks.Core.Diagnostics.Errors.ErrorType.Internal
}