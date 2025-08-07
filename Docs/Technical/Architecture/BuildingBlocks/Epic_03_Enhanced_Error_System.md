# 🚨 Epic 3: Enhanced Error System - Comprehensive Error Handling Implementation

**Version:** 1.0 - Production-Grade Error Management  
**Scope:** Error handling and diagnostics for BuildingBlocks/Core  
**Approach:** Comprehensive error categorization and metadata  
**Target:** .NET 10, Observability-ready error system

---

## 📋 Executive Summary

This epic establishes a **comprehensive error handling system** that provides:

- ✅ **Categorized Error Types** - Business, validation, technical errors
- ✅ **Rich Error Metadata** - Context, correlation, and debugging info
- ✅ **Exception Integration** - Seamless exception-to-error conversion
- ✅ **HTTP Integration** - Problem Details RFC compliance
- ✅ **Observability Support** - Structured logging and metrics
- ✅ **Guard Clauses** - Defensive programming utilities

---

## 🎯 Target Architecture

### Enhanced Error System Structure
```
Core/Diagnostics/
├── Errors/                  # Core error types
│   ├── Error.cs            # Main Error record
│   ├── ErrorType.cs        # Error categorization
│   ├── ErrorSeverity.cs    # Severity levels
│   └── Extensions/         # Error extensions
├── Exceptions/             # Domain exceptions
│   ├── DomainException.cs  # Base domain exception
│   ├── BusinessRuleException.cs # Business rule violations
│   └── ValidationException.cs  # Validation failures
├── Guards/                 # Guard clauses
│   ├── Guard.cs           # Main guard class
│   └── GuardExtensions.cs # Guard extensions
└── ProblemDetails/        # HTTP problem details
    ├── ProblemDetailsExtensions.cs
    └── ErrorToProblemDetailsMapper.cs
```

---

## 🔧 Implementation Details

### 3.1 Comprehensive Error Implementation

**File:** `Core/Diagnostics/Errors/ErrorType.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error type categorization for different kinds of failures
/// Used for routing, handling, and observability
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Input validation failures (400 Bad Request)
    /// </summary>
    Validation,
    
    /// <summary>
    /// Resource not found (404 Not Found)
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Resource conflicts or optimistic concurrency failures (409 Conflict)
    /// </summary>
    Conflict,
    
    /// <summary>
    /// Business rule violations (422 Unprocessable Entity)
    /// </summary>
    BusinessRule,
    
    /// <summary>
    /// Authentication failures (401 Unauthorized)
    /// </summary>
    Unauthorized,
    
    /// <summary>
    /// Authorization failures (403 Forbidden)
    /// </summary>
    Forbidden,
    
    /// <summary>
    /// Internal system errors (500 Internal Server Error)
    /// </summary>
    Internal,
    
    /// <summary>
    /// External service failures (502 Bad Gateway)
    /// </summary>
    External,
    
    /// <summary>
    /// Operation timeout (504 Gateway Timeout)
    /// </summary>
    Timeout,
    
    /// <summary>
    /// Operation was cancelled (499 Client Closed Request)
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// Rate limiting exceeded (429 Too Many Requests)
    /// </summary>
    RateLimit,
    
    /// <summary>
    /// Database or persistence layer errors (507 Insufficient Storage)
    /// </summary>
    Persistence,
    
    /// <summary>
    /// Aggregated multiple errors (422 Unprocessable Entity)
    /// </summary>
    Aggregate,
    
    /// <summary>
    /// Configuration or setup errors (500 Internal Server Error)
    /// </summary>
    Configuration,
    
    /// <summary>
    /// Network connectivity issues (502 Bad Gateway)
    /// </summary>
    Network,
    
    /// <summary>
    /// Security-related errors (403 Forbidden)
    /// </summary>
    Security
}
```

**File:** `Core/Diagnostics/Errors/ErrorSeverity.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error severity levels for logging and alerting
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational errors (expected conditions)
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// Warning errors (unexpected but recoverable)
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Error conditions (operation failed but system stable)
    /// </summary>
    Error = 3,
    
    /// <summary>
    /// Critical errors (system stability at risk)
    /// </summary>
    Critical = 4,
    
    /// <summary>
    /// Fatal errors (immediate attention required)
    /// </summary>
    Fatal = 5
}
```

**File:** `Core/Diagnostics/Errors/Error.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Comprehensive error model with categorization and metadata
/// Immutable record for functional error handling
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Unique error code for programmatic handling
    /// </summary>
    public string Code { get; }
    
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Error categorization
    /// </summary>
    public ErrorType Type { get; }
    
    /// <summary>
    /// Error severity level
    /// </summary>
    public ErrorSeverity Severity { get; }
    
    /// <summary>
    /// Underlying exception (if any)
    /// </summary>
    public Exception? InnerException { get; }
    
    /// <summary>
    /// Additional metadata for context
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }
    
    /// <summary>
    /// Stack trace for debugging (debug builds only)
    /// </summary>
    public string? StackTrace { get; }
    
    /// <summary>
    /// When the error occurred
    /// </summary>
    public DateTime OccurredAt { get; }
    
    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string? CorrelationId { get; }
    
    /// <summary>
    /// Source component or layer where error originated
    /// </summary>
    public string? Source { get; }
    
    private Error(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity severity = ErrorSeverity.Error,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? stackTrace = null,
        string? correlationId = null,
        string? source = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Severity = severity;
        InnerException = innerException;
        Metadata = metadata;
        StackTrace = stackTrace;
        OccurredAt = DateTime.UtcNow;
        CorrelationId = correlationId;
        Source = source;
    }
    
    #region Factory Methods
    
    /// <summary>
    /// Create a validation error
    /// </summary>
    public static Error Validation(
        string message,
        string code = "VALIDATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Validation, 
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    /// <summary>
    /// Create a not found error
    /// </summary>
    public static Error NotFound(
        string message,
        string code = "NOT_FOUND",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.NotFound,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    /// <summary>
    /// Create a conflict error
    /// </summary>
    public static Error Conflict(
        string message,
        string code = "CONFLICT",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Conflict,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    /// <summary>
    /// Create a business rule error
    /// </summary>
    public static Error BusinessRule(
        string message,
        string code = "BUSINESS_RULE",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.BusinessRule,
            ErrorSeverity.Error, metadata: metadata);
    }
    
    /// <summary>
    /// Create an unauthorized error
    /// </summary>
    public static Error Unauthorized(
        string message = "Unauthorized access",
        string code = "UNAUTHORIZED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Unauthorized,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    /// <summary>
    /// Create a forbidden error
    /// </summary>
    public static Error Forbidden(
        string message = "Access forbidden",
        string code = "FORBIDDEN",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Forbidden,
            ErrorSeverity.Warning, metadata: metadata);
    }
    
    /// <summary>
    /// Create an internal error
    /// </summary>
    public static Error Internal(
        string message,
        string code = "INTERNAL_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        string? stackTrace = null;
        
        #if DEBUG
        // Only capture stack trace in debug mode for performance
        stackTrace = innerException?.StackTrace ?? Environment.StackTrace;
        #endif
        
        return new Error(code, message, ErrorType.Internal,
            ErrorSeverity.Critical, innerException, metadata, stackTrace);
    }
    
    /// <summary>
    /// Create an external service error
    /// </summary>
    public static Error External(
        string message,
        string code = "EXTERNAL_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.External,
            ErrorSeverity.Error, innerException, metadata);
    }
    
    /// <summary>
    /// Create a timeout error
    /// </summary>
    public static Error Timeout(
        string message = "Operation timed out",
        string code = "TIMEOUT",
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var enrichedMetadata = metadata != null 
            ? new Dictionary<string, object>(metadata) 
            : new Dictionary<string, object>();
            
        if (timeout.HasValue)
        {
            enrichedMetadata["TimeoutDuration"] = timeout.Value.ToString();
        }
        
        return new Error(code, message, ErrorType.Timeout,
            ErrorSeverity.Error, metadata: enrichedMetadata);
    }
    
    /// <summary>
    /// Create a cancelled error
    /// </summary>
    public static Error Cancelled(
        string message = "Operation was cancelled",
        string code = "CANCELLED",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Cancelled,
            ErrorSeverity.Info, metadata: metadata);
    }
    
    /// <summary>
    /// Create a rate limit error
    /// </summary>
    public static Error RateLimit(
        string message = "Rate limit exceeded",
        string code = "RATE_LIMIT",
        TimeSpan? retryAfter = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var enrichedMetadata = metadata != null 
            ? new Dictionary<string, object>(metadata) 
            : new Dictionary<string, object>();
            
        if (retryAfter.HasValue)
        {
            enrichedMetadata["RetryAfter"] = retryAfter.Value.TotalSeconds;
        }
        
        return new Error(code, message, ErrorType.RateLimit,
            ErrorSeverity.Warning, metadata: enrichedMetadata);
    }
    
    /// <summary>
    /// Create a persistence error
    /// </summary>
    public static Error Persistence(
        string message,
        string code = "PERSISTENCE_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Persistence,
            ErrorSeverity.Error, innerException, metadata);
    }
    
    /// <summary>
    /// Create a configuration error
    /// </summary>
    public static Error Configuration(
        string message,
        string code = "CONFIGURATION_ERROR",
        string? configurationKey = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var enrichedMetadata = metadata != null 
            ? new Dictionary<string, object>(metadata) 
            : new Dictionary<string, object>();
            
        if (!string.IsNullOrEmpty(configurationKey))
        {
            enrichedMetadata["ConfigurationKey"] = configurationKey;
        }
        
        return new Error(code, message, ErrorType.Configuration,
            ErrorSeverity.Critical, metadata: enrichedMetadata);
    }
    
    /// <summary>
    /// Create a network error
    /// </summary>
    public static Error Network(
        string message,
        string code = "NETWORK_ERROR",
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Network,
            ErrorSeverity.Error, innerException, metadata);
    }
    
    /// <summary>
    /// Create a security error
    /// </summary>
    public static Error Security(
        string message,
        string code = "SECURITY_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new Error(code, message, ErrorType.Security,
            ErrorSeverity.Critical, metadata: metadata);
    }
    
    /// <summary>
    /// Create error from exception with smart categorization
    /// </summary>
    public static Error FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        
        var metadata = new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["Source"] = exception.Source ?? "Unknown"
        };
        
        // Add exception data
        foreach (DictionaryEntry entry in exception.Data)
        {
            if (entry.Key != null && entry.Value != null)
            {
                metadata[$"Data_{entry.Key}"] = entry.Value;
            }
        }
        
        // Smart categorization based on exception type
        return exception switch
        {
            ArgumentException => Validation(exception.Message, "ARGUMENT_ERROR", metadata),
            ArgumentNullException => Validation(exception.Message, "ARGUMENT_NULL", metadata),
            InvalidOperationException => BusinessRule(exception.Message, "INVALID_OPERATION", metadata),
            NotSupportedException => BusinessRule(exception.Message, "NOT_SUPPORTED", metadata),
            UnauthorizedAccessException => Unauthorized(exception.Message, "UNAUTHORIZED_ACCESS", metadata),
            TimeoutException => Timeout(exception.Message, "OPERATION_TIMEOUT", metadata: metadata),
            OperationCanceledException => Cancelled(exception.Message, "OPERATION_CANCELLED", metadata),
            HttpRequestException => External(exception.Message, "HTTP_REQUEST_ERROR", exception, metadata),
            SocketException => Network(exception.Message, "SOCKET_ERROR", exception, metadata),
            SecurityException => Security(exception.Message, "SECURITY_VIOLATION", metadata),
            _ => Internal(exception.Message, exception.GetType().Name.ToUpperInvariant(), exception, metadata)
        };
    }
    
    /// <summary>
    /// Aggregate multiple errors into a composite error
    /// </summary>
    public static Error Aggregate(params Error[] errors)
    {
        if (errors == null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        if (errors.Length == 1)
            return errors[0];
            
        var messages = string.Join("; ", errors.Select(e => e.Message));
        var codes = string.Join(",", errors.Select(e => e.Code));
        var highestSeverity = errors.Max(e => e.Severity);
        
        var metadata = new Dictionary<string, object>
        {
            ["ErrorCount"] = errors.Length,
            ["ErrorCodes"] = codes,
            ["Errors"] = errors.Select(e => new { e.Code, e.Message, e.Type, e.Severity }).ToArray()
        };
        
        return new Error(
            "MULTIPLE_ERRORS",
            $"Multiple errors occurred: {messages}",
            ErrorType.Aggregate,
            highestSeverity,
            metadata: metadata);
    }
    
    #endregion
    
    #region Builder Methods
    
    /// <summary>
    /// Add metadata to the error
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        var newMetadata = Metadata != null 
            ? new Dictionary<string, object>(Metadata) 
            : new Dictionary<string, object>();
            
        newMetadata[key] = value;
        
        return this with { Metadata = newMetadata };
    }
    
    /// <summary>
    /// Add multiple metadata entries
    /// </summary>
    public Error WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
            return this;
            
        var newMetadata = Metadata != null 
            ? new Dictionary<string, object>(Metadata) 
            : new Dictionary<string, object>();
            
        foreach (var kvp in metadata)
        {
            newMetadata[kvp.Key] = kvp.Value;
        }
        
        return this with { Metadata = newMetadata };
    }
    
    /// <summary>
    /// Add correlation ID for tracing
    /// </summary>
    public Error WithCorrelationId(string correlationId)
    {
        return this with { CorrelationId = correlationId };
    }
    
    /// <summary>
    /// Change severity level
    /// </summary>
    public Error WithSeverity(ErrorSeverity severity)
    {
        return this with { Severity = severity };
    }
    
    /// <summary>
    /// Add source information
    /// </summary>
    public Error WithSource(string source)
    {
        return this with { Source = source };
    }
    
    /// <summary>
    /// Add inner exception
    /// </summary>
    public Error WithInnerException(Exception exception)
    {
        return this with { InnerException = exception };
    }
    
    #endregion
    
    #region Conversion Methods
    
    /// <summary>
    /// Convert to HTTP status code
    /// </summary>
    public int ToHttpStatusCode() => Type switch
    {
        ErrorType.Validation => 400,           // Bad Request
        ErrorType.Unauthorized => 401,         // Unauthorized
        ErrorType.Forbidden => 403,            // Forbidden
        ErrorType.NotFound => 404,             // Not Found
        ErrorType.Conflict => 409,             // Conflict
        ErrorType.BusinessRule => 422,         // Unprocessable Entity
        ErrorType.Aggregate => 422,            // Unprocessable Entity
        ErrorType.RateLimit => 429,            // Too Many Requests
        ErrorType.Internal => 500,             // Internal Server Error
        ErrorType.Configuration => 500,        // Internal Server Error
        ErrorType.External => 502,             // Bad Gateway
        ErrorType.Network => 502,              // Bad Gateway
        ErrorType.Timeout => 504,              // Gateway Timeout
        ErrorType.Persistence => 507,          // Insufficient Storage
        ErrorType.Security => 403,             // Forbidden
        ErrorType.Cancelled => 499,            // Client Closed Request
        _ => 500                               // Default to Internal Server Error
    };
    
    /// <summary>
    /// Convert to RFC 7807 Problem Details
    /// </summary>
    public ProblemDetails ToProblemDetails()
    {
        var statusCode = ToHttpStatusCode();
        
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitleForStatusCode(statusCode),
            Status = statusCode,
            Detail = Message,
            Instance = CorrelationId
        };
        
        // Add custom extensions
        problemDetails.Extensions.Add("errorCode", Code);
        problemDetails.Extensions.Add("errorType", Type.ToString());
        problemDetails.Extensions.Add("severity", Severity.ToString());
        problemDetails.Extensions.Add("occurredAt", OccurredAt.ToString("O"));
        
        if (!string.IsNullOrEmpty(Source))
            problemDetails.Extensions.Add("source", Source);
            
        if (Metadata != null)
        {
            foreach (var kvp in Metadata)
            {
                problemDetails.Extensions.Add(kvp.Key.ToCamelCase(), kvp.Value);
            }
        }
        
        return problemDetails;
    }
    
    /// <summary>
    /// Convert to structured log data
    /// </summary>
    public Dictionary<string, object> ToLogData()
    {
        var logData = new Dictionary<string, object>
        {
            ["ErrorCode"] = Code,
            ["ErrorMessage"] = Message,
            ["ErrorType"] = Type.ToString(),
            ["Severity"] = Severity.ToString(),
            ["OccurredAt"] = OccurredAt
        };
        
        if (!string.IsNullOrEmpty(CorrelationId))
            logData["CorrelationId"] = CorrelationId;
            
        if (!string.IsNullOrEmpty(Source))
            logData["Source"] = Source;
            
        if (InnerException != null)
        {
            logData["ExceptionType"] = InnerException.GetType().Name;
            logData["ExceptionMessage"] = InnerException.Message;
            
            #if DEBUG
            if (!string.IsNullOrEmpty(InnerException.StackTrace))
                logData["StackTrace"] = InnerException.StackTrace;
            #endif
        }
        
        if (Metadata != null)
        {
            foreach (var kvp in Metadata)
            {
                logData[$"Metadata_{kvp.Key}"] = kvp.Value;
            }
        }
        
        return logData;
    }
    
    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        504 => "Gateway Timeout",
        507 => "Insufficient Storage",
        _ => "Error"
    };
    
    #endregion
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"[{Type}] {Code}: {Message}");
        
        if (!string.IsNullOrEmpty(CorrelationId))
            sb.Append($" (CorrelationId: {CorrelationId})");
            
        if (!string.IsNullOrEmpty(Source))
            sb.Append($" (Source: {Source})");
        
        return sb.ToString();
    }
}

/// <summary>
/// Extension methods for string operations
/// </summary>
internal static class StringExtensions
{
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;
            
        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
```

### 3.2 Domain Exception Classes

**File:** `Core/Diagnostics/Exceptions/DomainException.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Base exception for domain-related errors
/// Contains structured error information
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// The structured error information
    /// </summary>
    public Error Error { get; }
    
    /// <summary>
    /// Multiple errors (for validation scenarios)
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }
    
    public DomainException(Error error) 
        : base(error.Message)
    {
        Error = error;
        Errors = new[] { error };
    }
    
    public DomainException(string message, Error error) 
        : base(message)
    {
        Error = error;
        Errors = new[] { error };
    }
    
    public DomainException(IEnumerable<Error> errors)
        : base(CreateAggregateMessage(errors))
    {
        var errorList = errors.ToList();
        if (!errorList.Any())
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        Error = errorList.Count == 1 ? errorList[0] : Error.Aggregate(errorList.ToArray());
        Errors = errorList;
    }
    
    public DomainException(string message, IEnumerable<Error> errors)
        : base(message)
    {
        var errorList = errors.ToList();
        if (!errorList.Any())
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        Error = errorList.Count == 1 ? errorList[0] : Error.Aggregate(errorList.ToArray());
        Errors = errorList;
    }
    
    private static string CreateAggregateMessage(IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();
        return errorList.Count == 1 
            ? errorList[0].Message 
            : $"Multiple domain errors occurred: {string.Join("; ", errorList.Select(e => e.Message))}";
    }
}
```

**File:** `Core/Diagnostics/Exceptions/BusinessRuleException.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception for business rule violations
/// </summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(IBusinessRule rule)
        : base(Error.BusinessRule(rule.Message, rule.Code))
    {
        BusinessRule = rule;
    }
    
    public BusinessRuleException(string message, IBusinessRule rule)
        : base(message, Error.BusinessRule(rule.Message, rule.Code))
    {
        BusinessRule = rule;
    }
    
    public BusinessRuleException(IEnumerable<IBusinessRule> rules)
        : base(rules.Select(r => Error.BusinessRule(r.Message, r.Code)))
    {
        BusinessRules = rules.ToList();
    }
    
    /// <summary>
    /// The business rule that was violated (single rule scenarios)
    /// </summary>
    public IBusinessRule? BusinessRule { get; }
    
    /// <summary>
    /// Multiple business rules that were violated
    /// </summary>
    public IReadOnlyList<IBusinessRule>? BusinessRules { get; }
}
```

**File:** `Core/Diagnostics/Exceptions/ValidationException.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception for validation failures
/// Compatible with FluentValidation
/// </summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(string message)
        : base(Error.Validation(message))
    {
        ValidationFailures = new List<ValidationFailure>();
    }
    
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(failures.Select(f => Error.Validation(f.ErrorMessage, f.ErrorCode ?? "VALIDATION_ERROR")
            .WithMetadata("PropertyName", f.PropertyName)
            .WithMetadata("AttemptedValue", f.AttemptedValue)))
    {
        ValidationFailures = failures.ToList();
    }
    
    public ValidationException(string message, IEnumerable<ValidationFailure> failures)
        : base(message, failures.Select(f => Error.Validation(f.ErrorMessage, f.ErrorCode ?? "VALIDATION_ERROR")
            .WithMetadata("PropertyName", f.PropertyName)
            .WithMetadata("AttemptedValue", f.AttemptedValue)))
    {
        ValidationFailures = failures.ToList();
    }
    
    /// <summary>
    /// Validation failures (FluentValidation compatibility)
    /// </summary>
    public IReadOnlyList<ValidationFailure> ValidationFailures { get; }
    
    /// <summary>
    /// Check if a specific property has validation errors
    /// </summary>
    public bool HasErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Any(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Get validation errors for a specific property
    /// </summary>
    public IEnumerable<ValidationFailure> GetErrorsForProperty(string propertyName)
    {
        return ValidationFailures.Where(f => 
            f.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Represents a validation failure (FluentValidation compatibility)
/// </summary>
public sealed class ValidationFailure
{
    public ValidationFailure(string propertyName, string errorMessage)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }
    
    public ValidationFailure(string propertyName, string errorMessage, object attemptedValue)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
        AttemptedValue = attemptedValue;
    }
    
    /// <summary>
    /// The name of the property that failed validation
    /// </summary>
    public string PropertyName { get; }
    
    /// <summary>
    /// The error message
    /// </summary>
    public string ErrorMessage { get; }
    
    /// <summary>
    /// The value that was attempted to be set
    /// </summary>
    public object? AttemptedValue { get; }
    
    /// <summary>
    /// Custom error code
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// Severity of the validation failure
    /// </summary>
    public Severity Severity { get; init; } = Severity.Error;
    
    public override string ToString() => ErrorMessage;
}

/// <summary>
/// Validation failure severity
/// </summary>
public enum Severity
{
    Error,
    Warning,
    Info
}
```

### 3.3 Guard Clauses Implementation

**File:** `Core/Diagnostics/Guards/Guard.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Guards;

/// <summary>
/// Guard clauses for defensive programming
/// Provides fluent API for parameter validation
/// </summary>
public static class Guard
{
    /// <summary>
    /// Start guard validation for a parameter
    /// </summary>
    public static GuardClause<T> Against<T>(T value, [CallerArgumentExpression("value")] string? parameterName = null)
    {
        return new GuardClause<T>(value, parameterName ?? "parameter");
    }
    
    /// <summary>
    /// Guard against null values
    /// </summary>
    public static T AgainstNull<T>(T? value, [CallerArgumentExpression("value")] string? parameterName = null)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName, $"Parameter '{parameterName}' cannot be null");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against null or empty strings
    /// </summary>
    public static string AgainstNullOrEmpty(string? value, [CallerArgumentExpression("value")] string? parameterName = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty", parameterName);
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against null, empty, or whitespace strings
    /// </summary>
    public static string AgainstNullOrWhiteSpace(string? value, [CallerArgumentExpression("value")] string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be null, empty, or whitespace", parameterName);
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against empty collections
    /// </summary>
    public static IEnumerable<T> AgainstEmpty<T>(IEnumerable<T>? collection, [CallerArgumentExpression("collection")] string? parameterName = null)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(parameterName, $"Parameter '{parameterName}' cannot be null");
        }
        
        if (!collection.Any())
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be empty", parameterName);
        }
        
        return collection;
    }
    
    /// <summary>
    /// Guard against negative numbers
    /// </summary>
    public static T AgainstNegative<T>(T value, [CallerArgumentExpression("value")] string? parameterName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default(T)) < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' cannot be negative");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against zero values
    /// </summary>
    public static T AgainstZero<T>(T value, [CallerArgumentExpression("value")] string? parameterName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(default(T)) == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Parameter '{parameterName}' cannot be zero");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against values out of range
    /// </summary>
    public static T AgainstOutOfRange<T>(T value, T min, T max, [CallerArgumentExpression("value")] string? parameterName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, 
                $"Parameter '{parameterName}' must be between {min} and {max}");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard with custom condition
    /// </summary>
    public static T Against<T>(T value, bool condition, string message, [CallerArgumentExpression("value")] string? parameterName = null)
    {
        if (condition)
        {
            throw new ArgumentException(message, parameterName);
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard with custom predicate
    /// </summary>
    public static T Against<T>(T value, Func<T, bool> predicate, string message, [CallerArgumentExpression("value")] string? parameterName = null)
    {
        if (predicate(value))
        {
            throw new ArgumentException(message, parameterName);
        }
        
        return value;
    }
}
```

**File:** `Core/Diagnostics/Guards/GuardClause.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Guards;

/// <summary>
/// Fluent guard clause for chaining validations
/// </summary>
public sealed class GuardClause<T>
{
    private readonly T _value;
    private readonly string _parameterName;
    
    internal GuardClause(T value, string parameterName)
    {
        _value = value;
        _parameterName = parameterName;
    }
    
    /// <summary>
    /// The validated value
    /// </summary>
    public T Value => _value;
    
    /// <summary>
    /// Guard against null
    /// </summary>
    public GuardClause<T> Null() where T : class
    {
        if (_value is null)
        {
            throw new ArgumentNullException(_parameterName, $"Parameter '{_parameterName}' cannot be null");
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against empty strings
    /// </summary>
    public GuardClause<T> Empty() where T : class
    {
        if (_value is string str && string.IsNullOrEmpty(str))
        {
            throw new ArgumentException($"Parameter '{_parameterName}' cannot be empty", _parameterName);
        }
        
        if (_value is IEnumerable enumerable && !enumerable.Cast<object>().Any())
        {
            throw new ArgumentException($"Parameter '{_parameterName}' cannot be empty", _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against whitespace strings
    /// </summary>
    public GuardClause<T> WhiteSpace() where T : class
    {
        if (_value is string str && string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentException($"Parameter '{_parameterName}' cannot be whitespace", _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against negative values
    /// </summary>
    public GuardClause<T> Negative() where T : IComparable<T>
    {
        if (_value.CompareTo(default(T)) < 0)
        {
            throw new ArgumentOutOfRangeException(_parameterName, _value, 
                $"Parameter '{_parameterName}' cannot be negative");
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against zero values
    /// </summary>
    public GuardClause<T> Zero() where T : IComparable<T>
    {
        if (_value.CompareTo(default(T)) == 0)
        {
            throw new ArgumentOutOfRangeException(_parameterName, _value, 
                $"Parameter '{_parameterName}' cannot be zero");
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard against out of range values
    /// </summary>
    public GuardClause<T> OutOfRange(T min, T max) where T : IComparable<T>
    {
        if (_value.CompareTo(min) < 0 || _value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(_parameterName, _value, 
                $"Parameter '{_parameterName}' must be between {min} and {max}");
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard with custom condition
    /// </summary>
    public GuardClause<T> When(bool condition, string message)
    {
        if (condition)
        {
            throw new ArgumentException(message, _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Guard with custom predicate
    /// </summary>
    public GuardClause<T> When(Func<T, bool> predicate, string message)
    {
        if (predicate(_value))
        {
            throw new ArgumentException(message, _parameterName);
        }
        
        return this;
    }
    
    /// <summary>
    /// Implicit conversion to the underlying value
    /// </summary>
    public static implicit operator T(GuardClause<T> guard) => guard._value;
}
```

### 3.4 Problem Details Extensions

**File:** `Core/Diagnostics/ProblemDetails/ProblemDetailsExtensions.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.ProblemDetails;

/// <summary>
/// Extensions for converting errors to Problem Details (RFC 7807)
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Configure Problem Details for the application
    /// </summary>
    public static IServiceCollection AddProblemDetailsSupport(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            // Customize problem details generation
            options.CustomizeProblemDetails = (context) =>
            {
                // Add correlation ID if available
                if (context.HttpContext.TraceIdentifier != null)
                {
                    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
                }
                
                // Add timestamp
                context.ProblemDetails.Extensions.TryAdd("timestamp", DateTime.UtcNow.ToString("O"));
                
                // Add machine name for debugging
                #if DEBUG
                context.ProblemDetails.Extensions.TryAdd("machine", Environment.MachineName);
                #endif
            };
        });
        
        return services;
    }
    
    /// <summary>
    /// Convert Result to ActionResult with proper error handling
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }
        
        var problemDetails = result.Error.ToProblemDetails();
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
    
    /// <summary>
    /// Convert Result to IResult (minimal APIs)
    /// </summary>
    public static IResult ToResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails();
        return Results.Problem(
            detail: problemDetails.Detail,
            instance: problemDetails.Instance,
            status: problemDetails.Status,
            title: problemDetails.Title,
            type: problemDetails.Type,
            extensions: problemDetails.Extensions);
    }
    
    /// <summary>
    /// Convert non-generic Result to IResult
    /// </summary>
    public static IResult ToResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok();
        }
        
        var problemDetails = result.Error.ToProblemDetails();
        return Results.Problem(
            detail: problemDetails.Detail,
            instance: problemDetails.Instance,
            status: problemDetails.Status,
            title: problemDetails.Title,
            type: problemDetails.Type,
            extensions: problemDetails.Extensions);
    }
}
```

---

## 📊 Implementation Roadmap

### Week 1: Core Error System
- [ ] Comprehensive Error record implementation
- [ ] Error categorization and severity levels
- [ ] Exception integration and smart categorization
- [ ] Metadata and observability support

### Week 2: Domain Exceptions
- [ ] DomainException base class
- [ ] BusinessRuleException for rule violations
- [ ] ValidationException with FluentValidation support
- [ ] Exception-to-Error conversion utilities

### Week 3: Guard Clauses and Extensions
- [ ] Guard clause implementation
- [ ] Fluent guard API
- [ ] Problem Details integration
- [ ] ASP.NET Core extensions

---

## 🎯 Success Criteria

Epic 3 is complete when:

1. ✅ **Error categorization** covers all failure scenarios
2. ✅ **Rich metadata** supports debugging and observability
3. ✅ **Exception integration** provides seamless conversion
4. ✅ **Guard clauses** enable defensive programming
5. ✅ **HTTP integration** follows RFC 7807 standards
6. ✅ **Observability support** enables structured logging
7. ✅ **100% test coverage** for error handling paths

---

## 🚀 Usage Examples

### Comprehensive Error Handling
```csharp
public async Task<Result<Order>> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
{
    try
    {
        // Guard clauses
        var userId = Guard.Against(command.UserId, u => u == UserId.Empty, "User ID cannot be empty").Value;
        var items = Guard.AgainstEmpty(command.Items, nameof(command.Items));
        
        // Business logic with error handling
        var order = Order.Create(userId);
        if (order.IsFailure)
            return order;
            
        foreach (var item in items)
        {
            var result = await order.Value.AddItemAsync(item, ct);
            if (result.IsFailure)
                return Result<Order>.Failure(result.Error.WithSource("OrderCreation"));
        }
        
        await _repository.SaveAsync(order.Value, ct);
        return order;
    }
    catch (TimeoutException ex)
    {
        return Result<Order>.Failure(
            Error.Timeout("Order creation timed out", "ORDER_CREATION_TIMEOUT")
                .WithInnerException(ex)
                .WithCorrelationId(_correlationIdProvider.GetCorrelationId())
                .WithMetadata("UserId", command.UserId.ToString()));
    }
    catch (Exception ex)
    {
        return Result<Order>.Failure(Error.FromException(ex));
    }
}
```

### Error Aggregation and Validation
```csharp
public Result<User> ValidateAndCreateUser(CreateUserRequest request)
{
    var validations = new List<Error>();
    
    // Validate email
    var emailResult = Email.Create(request.Email);
    if (emailResult.IsFailure)
        validations.Add(emailResult.Error);
        
    // Validate age
    if (request.Age < 18)
        validations.Add(Error.Validation("User must be 18 or older", "USER_TOO_YOUNG"));
        
    // Validate username
    if (string.IsNullOrWhiteSpace(request.UserName))
        validations.Add(Error.Validation("Username is required", "USERNAME_REQUIRED"));
        
    // Return aggregated errors if any
    if (validations.Any())
    {
        return Result<User>.Failure(Error.Aggregate(validations.ToArray()));
    }
    
    // Create user if all validations pass
    return User.Create(emailResult.Value, request.UserName, request.Age);
}
```

### ASP.NET Core Integration
```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        var command = new CreateOrderCommand(request.UserId, request.Items);
        var result = await _orderService.CreateOrderAsync(command, ct);
        
        // Automatic conversion to proper HTTP response
        return result.ToActionResult();
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(string id, CancellationToken ct)
    {
        var orderIdResult = OrderId.From(id);
        if (orderIdResult.IsFailure)
            return orderIdResult.ToActionResult<OrderDto>();
            
        var result = await _orderService.GetOrderAsync(orderIdResult.Value, ct);
        return result.ToActionResult();
    }
}
```

### Observability Integration
```csharp
public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;
    
    public async Task<Result<Order>> GetOrderAsync(OrderId orderId, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("GetOrder");
        activity?.SetTag("order.id", orderId.ToString());
        
        var result = await _repository.GetByIdAsync(orderId, ct);
        
        if (result.IsFailure)
        {
            // Structured logging with error details
            _logger.LogWarning("Failed to get order {OrderId}: {Error}",
                orderId, 
                result.Error.ToLogData());
                
            // Metrics
            _metrics.IncrementCounter("orders.get.failed", 
                new[] { ("error_type", result.Error.Type.ToString()) });
                
            activity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
        }
        else
        {
            _logger.LogDebug("Successfully retrieved order {OrderId}", orderId);
            _metrics.IncrementCounter("orders.get.success");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        
        return result;
    }
}
```

---

**END OF EPIC 3: ENHANCED ERROR SYSTEM**