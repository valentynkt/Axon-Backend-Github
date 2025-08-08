using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Enhanced immutable error record with comprehensive factory methods, rich metadata support,
/// and full observability integration for railway-oriented programming.
/// </summary>
public sealed record Error
{
    #region Properties
    
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public ErrorSeverity Severity { get; }
    public Exception? InnerException { get; }
    public IReadOnlyDictionary<string, object>? Metadata { get; }
    
#if DEBUG
    public string? StackTrace { get; }
#endif
    
    public DateTime OccurredAt { get; }
    public string? CorrelationId { get; }
    public string? Source { get; }
    
    #endregion    
    #region Static Members
    
    // String interning for common codes (performance optimization)
    private static readonly ConcurrentDictionary<string, string> InternedCodes = new();
    
    // Default severity mappings per ErrorType
    private static readonly Dictionary<ErrorType, ErrorSeverity> DefaultSeverityMappings = new()
    {
        { ErrorType.Validation, ErrorSeverity.Warning },
        { ErrorType.NotFound, ErrorSeverity.Info },
        { ErrorType.Conflict, ErrorSeverity.Warning },
        { ErrorType.BusinessRule, ErrorSeverity.Warning },
        { ErrorType.Unauthorized, ErrorSeverity.Warning },
        { ErrorType.Forbidden, ErrorSeverity.Warning },
        { ErrorType.Internal, ErrorSeverity.Critical },
        { ErrorType.External, ErrorSeverity.Error },
        { ErrorType.Timeout, ErrorSeverity.Warning },
        { ErrorType.Cancelled, ErrorSeverity.Info },
        { ErrorType.RateLimit, ErrorSeverity.Warning },
        { ErrorType.Persistence, ErrorSeverity.Critical },
        { ErrorType.Aggregate, ErrorSeverity.Error },
        { ErrorType.Configuration, ErrorSeverity.Critical },
        { ErrorType.Network, ErrorSeverity.Error },
        { ErrorType.Security, ErrorSeverity.Fatal }
    };
    
    #endregion    
    #region Constructor
    
    private Error(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity? severity = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? stackTrace = null,
        DateTime? occurredAt = null,
        string? correlationId = null,
        string? source = null)
    {
        Code = InternCode(code ?? throw new ArgumentNullException(nameof(code)));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Severity = severity ?? DefaultSeverityMappings.GetValueOrDefault(type, ErrorSeverity.Error);
        InnerException = innerException;
        Metadata = metadata;
        
#if DEBUG
        StackTrace = stackTrace ?? (innerException != null ? innerException.StackTrace : Environment.StackTrace);
#endif
        
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        CorrelationId = correlationId;
        Source = source;
    }
    
    private static string InternCode(string code) => InternedCodes.GetOrAdd(code, c => string.Intern(c));
    
    #endregion    
    #region Factory Methods
    
    /// <summary>
    /// Create a validation error (400 Bad Request)
    /// </summary>
    public static Error Validation(
        string message,
        string code = "VALIDATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Validation, metadata: metadata);
    }
    
    /// <summary>
    /// Create a not found error (404 Not Found)
    /// </summary>
    public static Error NotFound(
        string message,
        string code = "NOT_FOUND",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.NotFound, metadata: metadata);
    }
    
    /// <summary>
    /// Create a conflict error (409 Conflict)
    /// </summary>
    public static Error Conflict(
        string message,
        string code = "CONFLICT",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Conflict, metadata: metadata);
    }    
    /// <summary>
    /// Create a business rule error (422 Unprocessable Entity)
    /// </summary>
    public static Error BusinessRule(
        string message,
        string code = "BUSINESS_RULE_VIOLATION",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.BusinessRule, metadata: metadata);
    }
    
    /// <summary>
    /// Create an unauthorized error (401 Unauthorized)
    /// </summary>
    public static Error Unauthorized(
        string? message = "Access denied - authentication required",
        string code = "UNAUTHORIZED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message ?? "Access denied", ErrorType.Unauthorized, metadata: metadata);
    }
    
    /// <summary>
    /// Create a forbidden error (403 Forbidden)
    /// </summary>
    public static Error Forbidden(
        string? message = "Access denied - insufficient permissions",
        string code = "FORBIDDEN",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message ?? "Access denied", ErrorType.Forbidden, metadata: metadata);
    }
    
    /// <summary>
    /// Create an internal error (500 Internal Server Error)
    /// </summary>
    public static Error Internal(
        string message,
        string code = "INTERNAL_ERROR",
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Internal, innerException: exception, metadata: metadata);
    }    
    /// <summary>
    /// Create an external service error (502 Bad Gateway)
    /// </summary>
    public static Error External(
        string message,
        string code = "EXTERNAL_SERVICE_ERROR",
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.External, innerException: exception, metadata: metadata);
    }
    
    /// <summary>
    /// Create a timeout error (504 Gateway Timeout)
    /// </summary>
    public static Error Timeout(
        string? message = "Operation timed out",
        string code = "TIMEOUT",
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        if (timeout.HasValue)
            meta["TimeoutDuration"] = timeout.Value.ToString();
            
        return new Error(code, message ?? "Operation timed out", ErrorType.Timeout, 
            metadata: meta.Count > 0 ? meta : null);
    }
    
    /// <summary>
    /// Create a cancellation error (499 Client Closed Request)
    /// </summary>
    public static Error Cancelled(
        string? message = "Operation was cancelled",
        string code = "OPERATION_CANCELLED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message ?? "Operation was cancelled", ErrorType.Cancelled, metadata: metadata);
    }    
    /// <summary>
    /// Create a rate limit error (429 Too Many Requests)
    /// </summary>
    public static Error RateLimit(
        string? message = "Rate limit exceeded",
        string code = "RATE_LIMIT_EXCEEDED",
        TimeSpan? retryAfter = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        if (retryAfter.HasValue)
            meta["RetryAfter"] = retryAfter.Value.TotalSeconds;
            
        return new Error(code, message ?? "Rate limit exceeded", ErrorType.RateLimit, 
            metadata: meta.Count > 0 ? meta : null);
    }
    
    /// <summary>
    /// Create a persistence error (507 Insufficient Storage)
    /// </summary>
    public static Error Persistence(
        string message,
        string code = "PERSISTENCE_ERROR",
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Persistence, innerException: exception, metadata: metadata);
    }
    
    /// <summary>
    /// Create a configuration error (500 Internal Server Error)
    /// </summary>
    public static Error Configuration(
        string message,
        string code = "CONFIGURATION_ERROR",
        string? configKey = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(configKey))
            meta["ConfigurationKey"] = configKey;
            
        return new Error(code, message, ErrorType.Configuration, 
            metadata: meta.Count > 0 ? meta : null);
    }    
    /// <summary>
    /// Create a network error (502 Bad Gateway)
    /// </summary>
    public static Error Network(
        string message,
        string code = "NETWORK_ERROR",
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Network, innerException: exception, metadata: metadata);
    }
    
    /// <summary>
    /// Create a security error (403 Forbidden)
    /// </summary>
    public static Error Security(
        string message,
        string code = "SECURITY_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Security, metadata: metadata);
    }
    
    /// <summary>
    /// Create an aggregate error (422 Unprocessable Entity)
    /// </summary>
    public static Error Aggregate(
        string message,
        string code = "AGGREGATE_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Aggregate, metadata: metadata);
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
            metadata: metadata);
    }
    
    /// <summary>
    /// Smart exception categorization and Error creation
    /// </summary>
    public static Error FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            ArgumentException or ArgumentNullException => 
                Validation(exception.Message, "INVALID_ARGUMENT", CreateExceptionMetadata(exception)),
            
            UnauthorizedAccessException => 
                Unauthorized(exception.Message, "ACCESS_DENIED", CreateExceptionMetadata(exception)),
            
            InvalidOperationException => 
                BusinessRule(exception.Message, "INVALID_OPERATION", CreateExceptionMetadata(exception)),
            
            TimeoutException => 
                Timeout(exception.Message, "OPERATION_TIMEOUT", metadata: CreateExceptionMetadata(exception)),
            
            OperationCanceledException => 
                Cancelled(exception.Message, "OPERATION_CANCELLED", CreateExceptionMetadata(exception)),
            
            HttpRequestException => 
                External(exception.Message, "HTTP_REQUEST_FAILED", exception, CreateExceptionMetadata(exception)),
            
            DbUpdateException or DbException => 
                Persistence(exception.Message, "DATABASE_ERROR", exception, CreateExceptionMetadata(exception)),
            
            SocketException or NetworkInformationException => 
                Network(exception.Message, "NETWORK_FAILURE", exception, CreateExceptionMetadata(exception)),
            
            SecurityException => 
                Security(exception.Message, "SECURITY_VIOLATION", CreateExceptionMetadata(exception)),
            
            _ => Internal(exception.Message, "UNHANDLED_EXCEPTION", exception, CreateExceptionMetadata(exception))
        };
    }    
    private static Dictionary<string, object> CreateExceptionMetadata(Exception exception)
    {
        return new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["Source"] = exception.Source ?? "Unknown",
            ["HResult"] = exception.HResult
        };
    }
    
    #endregion
    
    #region Builder Methods
    
    /// <summary>
    /// Add single metadata entry
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        var newMetadata = Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        newMetadata[key] = value;
        
        return this with { Metadata = newMetadata };
    }
    
    /// <summary>
    /// Add multiple metadata entries
    /// </summary>
    public Error WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        
        var newMetadata = Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        foreach (var (key, value) in metadata)
        {
            newMetadata[key] = value;
        }
        
        return this with { Metadata = newMetadata };
    }    
    /// <summary>
    /// Add tracing correlation ID
    /// </summary>
    public Error WithCorrelationId(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        return this with { CorrelationId = correlationId };
    }
    
    /// <summary>
    /// Override default severity
    /// </summary>
    public Error WithSeverity(ErrorSeverity severity)
    {
        return this with { Severity = severity };
    }
    
    /// <summary>
    /// Add source component/layer
    /// </summary>
    public Error WithSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return this with { Source = source };
    }
    
    /// <summary>
    /// Attach inner exception
    /// </summary>
    public Error WithInnerException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return this with { InnerException = exception };
    }
    
    #endregion    
    #region Conversion Methods
    
    /// <summary>
    /// Convert ErrorType to HTTP status code
    /// </summary>
    public int ToHttpStatusCode() => Type switch
    {
        ErrorType.Validation => 400, // Bad Request
        ErrorType.Unauthorized => 401, // Unauthorized
        ErrorType.Forbidden => 403, // Forbidden
        ErrorType.NotFound => 404, // Not Found
        ErrorType.Conflict => 409, // Conflict
        ErrorType.BusinessRule => 422, // Unprocessable Entity
        ErrorType.Aggregate => 422, // Unprocessable Entity
        ErrorType.RateLimit => 429, // Too Many Requests
        ErrorType.Internal => 500, // Internal Server Error
        ErrorType.Configuration => 500, // Internal Server Error
        ErrorType.External => 502, // Bad Gateway
        ErrorType.Network => 502, // Bad Gateway
        ErrorType.Timeout => 504, // Gateway Timeout
        ErrorType.Persistence => 507, // Insufficient Storage
        ErrorType.Security => 403, // Forbidden
        ErrorType.Cancelled => 499, // Client Closed Request (non-standard)
        _ => 500 // Internal Server Error
    };
    
    /// <summary>
    /// Convert to structured logging dictionary
    /// </summary>
    public Dictionary<string, object> ToLogData()
    {
        var logData = new Dictionary<string, object>
        {
            ["ErrorCode"] = Code,
            ["ErrorMessage"] = Message,
            ["ErrorType"] = Type.ToString(),
            ["ErrorSeverity"] = Severity.ToString(),
            ["OccurredAt"] = OccurredAt,
            ["HttpStatusCode"] = ToHttpStatusCode()
        };
        
        if (!string.IsNullOrEmpty(CorrelationId))
            logData["CorrelationId"] = CorrelationId;
            
        if (!string.IsNullOrEmpty(Source))
            logData["Source"] = Source;        
        if (InnerException != null)
        {
            logData["ExceptionType"] = InnerException.GetType().FullName!;
            logData["ExceptionMessage"] = InnerException.Message;
        }
        
        if (Metadata != null)
        {
            foreach (var (key, value) in Metadata)
            {
                logData[$"Metadata_{key}"] = value;
            }
        }
        
        return logData;
    }
    
    /// <summary>
    /// Human-readable string representation
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string> { $"[{Type}]", $"{Code}:", Message };
        
        if (!string.IsNullOrEmpty(Source))
            parts.Insert(1, $"({Source})");
            
        if (!string.IsNullOrEmpty(CorrelationId))
            parts.Add($"[CorrelationId: {CorrelationId}]");
            
        return string.Join(" ", parts);
    }
    
    #endregion
}