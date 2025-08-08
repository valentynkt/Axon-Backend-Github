# 🚨 Epic 3: Enhanced Error System - Comprehensive Error Handling Implementation

**Version:** 2.0 - Production-Grade Error Management  
**Epic ID:** Epic_03  
**Epic Priority:** Critical  
**Estimated Duration:** 8-10 days  
**Dependencies:** None (Foundation Epic)  
**Integrations:** Epic_05 (Pipeline Behaviors), Epic_04 (CQRS Foundation)  
**Scope:** Error handling and diagnostics for BuildingBlocks/Core  
**Approach:** Comprehensive error categorization and metadata  
**Target:** .NET 10, Observability-ready error system

---

## 📋 Executive Summary

This epic establishes a **comprehensive, production-grade error handling system** that provides:

- ✅ **Categorized Error Types** - 16+ error categories covering all failure scenarios
- ✅ **Rich Error Metadata** - Context, correlation, and debugging info with telemetry
- ✅ **Exception Integration** - Smart exception-to-error conversion with categorization
- ✅ **HTTP Integration** - RFC 7807 Problem Details compliance with extensions
- ✅ **Result Pattern Integration** - Seamless functional error handling
- ✅ **Pipeline Behaviors Integration** - ValidationBehavior error aggregation
- ✅ **Observability Support** - OpenTelemetry, structured logging, and metrics
- ✅ **Guard Clauses** - Fluent defensive programming utilities
- ✅ **Global Error Handling** - Middleware and configuration management
- ✅ **Performance Optimized** - Minimal allocation, cached metadata
- ✅ **Testing Framework** - Comprehensive test utilities and patterns

## 🎯 Business Value

**Critical Foundation Epic** that eliminates inconsistent error handling across the application while providing:
- **Developer Productivity**: Consistent error patterns reduce debugging time by 60%
- **Production Stability**: Structured error metadata enables faster incident resolution
- **User Experience**: Well-formed error responses improve client-side error handling
- **Observability**: Rich telemetry data enables proactive issue detection and resolution
- **Compliance**: RFC standards compliance enables enterprise API integration

---

## 🎯 Target Architecture

### Enhanced Error System Structure
```
Core/Diagnostics/
├── Errors/                      # Core error types
│   ├── Error.cs                # Main Error record with metadata
│   ├── ErrorType.cs            # Comprehensive error categorization
│   ├── ErrorSeverity.cs        # Severity levels with alerting
│   ├── ErrorMetadata.cs        # Structured metadata support
│   ├── ErrorBuilder.cs         # Fluent error construction
│   └── Extensions/             # Error extensions
│       ├── ErrorResultExtensions.cs    # Result pattern integration
│       ├── ErrorTelemetryExtensions.cs # OpenTelemetry integration
│       └── ErrorSerializationExtensions.cs # Caching support
├── Exceptions/                 # Domain exceptions
│   ├── DomainException.cs      # Base domain exception
│   ├── BusinessRuleException.cs # Business rule violations
│   ├── ValidationException.cs   # Validation failures
│   └── SystemException.cs      # System-level errors
├── Guards/                     # Guard clauses
│   ├── Guard.cs               # Main guard class
│   ├── GuardClause.cs         # Fluent guard API
│   └── Extensions/            # Guard extensions
├── ProblemDetails/            # HTTP problem details
│   ├── ProblemDetailsExtensions.cs     # ASP.NET Core integration
│   ├── ErrorToProblemDetailsMapper.cs  # RFC 7807 mapping
│   └── ProblemDetailsMiddleware.cs     # Global error handling
├── Interfaces/                # Core contracts
│   ├── IBusinessRule.cs       # Business rule contract
│   ├── IErrorHandler.cs       # Error handling strategy
│   └── IErrorAggregator.cs    # Error aggregation contract
├── Configuration/             # Error system configuration
│   ├── ErrorHandlingOptions.cs    # Configuration model
│   ├── ErrorHandlingServiceExtensions.cs # DI setup
│   └── ErrorHandlingMiddlewareExtensions.cs # Middleware setup
├── Testing/                   # Testing utilities
│   ├── ErrorAssertions.cs     # FluentAssertions extensions
│   ├── ErrorTestFixture.cs    # Test data builders
│   └── ErrorTestExtensions.cs # Test helper methods
└── Performance/               # Performance optimizations
    ├── ErrorCache.cs          # Error metadata caching
    ├── ErrorPool.cs           # Object pooling for errors
    └── ErrorMetrics.cs        # Performance metrics collection
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

## 🔗 4. Core Interfaces and Dependencies

### 4.1 Business Rule Interface

**File:** `Core/Diagnostics/Interfaces/IBusinessRule.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Interfaces;

/// <summary>
/// Represents a business rule that can be validated
/// Used by BusinessRuleException and validation scenarios
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// Unique identifier for the business rule
    /// Used for error codes and categorization
    /// </summary>
    string Code { get; }
    
    /// <summary>
    /// Human-readable description of the rule violation
    /// </summary>
    string Message { get; }
    
    /// <summary>
    /// Indicates if the business rule is currently broken
    /// </summary>
    bool IsBroken { get; }
    
    /// <summary>
    /// Additional context about the rule violation
    /// </summary>
    IReadOnlyDictionary<string, object>? Context { get; }
}

/// <summary>
/// Base implementation of IBusinessRule for common scenarios
/// </summary>
public abstract record BusinessRule(string Code, string Message) : IBusinessRule
{
    public abstract bool IsBroken { get; }
    public virtual IReadOnlyDictionary<string, object>? Context => null;
}

/// <summary>
/// Simple predicate-based business rule
/// </summary>
public sealed record PredicateBusinessRule(
    string Code, 
    string Message, 
    Func<bool> Predicate,
    IReadOnlyDictionary<string, object>? Context = null) : BusinessRule(Code, Message)
{
    public override bool IsBroken => Predicate();
    public override IReadOnlyDictionary<string, object>? Context { get; } = Context;
}
```

### 4.2 Error Handler Strategy Interface

**File:** `Core/Diagnostics/Interfaces/IErrorHandler.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Interfaces;

/// <summary>
/// Strategy interface for handling different types of errors
/// Enables custom error processing and transformation
/// </summary>
public interface IErrorHandler<in TError>
{
    /// <summary>
    /// Handle a specific error type
    /// </summary>
    Task<Error> HandleAsync(TError error, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determine if this handler can process the error
    /// </summary>
    bool CanHandle(object error);
}

/// <summary>
/// Composite error handler that delegates to appropriate handlers
/// </summary>
public interface IErrorHandlerService
{
    /// <summary>
    /// Handle any error using registered handlers
    /// </summary>
    Task<Error> HandleAsync(object error, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Register a new error handler
    /// </summary>
    void RegisterHandler<TError>(IErrorHandler<TError> handler);
}
```

### 4.3 Error Aggregation Interface

**File:** `Core/Diagnostics/Interfaces/IErrorAggregator.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Interfaces;

/// <summary>
/// Interface for aggregating multiple errors into composite errors
/// Used by validation behaviors and bulk operations
/// </summary>
public interface IErrorAggregator
{
    /// <summary>
    /// Aggregate multiple errors into a single composite error
    /// </summary>
    Error Aggregate(IEnumerable<Error> errors);
    
    /// <summary>
    /// Group errors by a specific property (e.g., validation property name)
    /// </summary>
    IEnumerable<IGrouping<string, Error>> GroupErrors(IEnumerable<Error> errors, Func<Error, string> groupSelector);
    
    /// <summary>
    /// Create validation-specific error aggregation grouped by property
    /// </summary>
    Error AggregateValidationErrors(IEnumerable<ValidationFailure> failures);
}
```

---

## 🔄 5. Result Pattern Integration

### 5.1 Error to Result Extensions

**File:** `Core/Diagnostics/Extensions/ErrorResultExtensions.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Extensions;

/// <summary>
/// Extensions for seamless integration between Error and Result patterns
/// </summary>
public static class ErrorResultExtensions
{
    /// <summary>
    /// Convert Error to Result<T> failure
    /// </summary>
    public static Result<T> ToResult<T>(this Error error)
    {
        return Result<T>.Failure(error);
    }
    
    /// <summary>
    /// Convert Error to Result failure
    /// </summary>
    public static Result ToResult(this Error error)
    {
        return Result.Failure(error);
    }
    
    /// <summary>
    /// Create successful Result from value with error context
    /// </summary>
    public static Result<T> ToSuccessResult<T>(this T value, Error? warningError = null)
    {
        var result = Result<T>.Success(value);
        
        if (warningError != null)
        {
            // Add warning to result metadata if Result supports it
            // This would require extending the Result pattern
        }
        
        return result;
    }
    
    /// <summary>
    /// Convert exception to Result failure using smart categorization
    /// </summary>
    public static Result<T> ToResult<T>(this Exception exception)
    {
        return Result<T>.Failure(Error.FromException(exception));
    }
    
    /// <summary>
    /// Convert business rule violations to Result failure
    /// </summary>
    public static Result<T> ToResult<T>(this IBusinessRule businessRule)
    {
        if (!businessRule.IsBroken)
            throw new InvalidOperationException("Cannot create failure result from unbroken business rule");
            
        var error = Error.BusinessRule(businessRule.Message, businessRule.Code);
        
        if (businessRule.Context != null)
        {
            error = error.WithMetadata(businessRule.Context);
        }
        
        return Result<T>.Failure(error);
    }
    
    /// <summary>
    /// Convert multiple business rules to aggregated Result failure
    /// </summary>
    public static Result<T> ToResult<T>(this IEnumerable<IBusinessRule> businessRules)
    {
        var brokenRules = businessRules.Where(r => r.IsBroken).ToArray();
        
        if (!brokenRules.Any())
            throw new InvalidOperationException("Cannot create failure result when no business rules are broken");
        
        var errors = brokenRules.Select(rule =>
        {
            var error = Error.BusinessRule(rule.Message, rule.Code);
            return rule.Context != null ? error.WithMetadata(rule.Context) : error;
        }).ToArray();
        
        return Result<T>.Failure(Error.Aggregate(errors));
    }
    
    /// <summary>
    /// Combine multiple Results, succeeding only if all succeed
    /// </summary>
    public static Result<T[]> Combine<T>(this IEnumerable<Result<T>> results)
    {
        var resultArray = results.ToArray();
        var failures = resultArray.Where(r => r.IsFailure).Select(r => r.Error).ToArray();
        
        if (failures.Any())
        {
            return Result<T[]>.Failure(Error.Aggregate(failures));
        }
        
        var values = resultArray.Select(r => r.Value).ToArray();
        return Result<T[]>.Success(values);
    }
    
    /// <summary>
    /// Execute multiple operations and combine results
    /// </summary>
    public static async Task<Result<T[]>> CombineAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks)
    {
        var results = await Task.WhenAll(resultTasks);
        return results.Combine();
    }
    
    /// <summary>
    /// Transform Result value while preserving error state
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper)
    {
        return result.IsSuccess 
            ? Result<TOut>.Success(mapper(result.Value))
            : Result<TOut>.Failure(result.Error);
    }
    
    /// <summary>
    /// Chain multiple Result-returning operations
    /// </summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> binder)
    {
        return result.IsSuccess 
            ? binder(result.Value)
            : Result<TOut>.Failure(result.Error);
    }
    
    /// <summary>
    /// Execute side effect on success without changing Result
    /// </summary>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }
        
        return result;
    }
    
    /// <summary>
    /// Execute side effect on failure without changing Result
    /// </summary>
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }
        
        return result;
    }
}
```

---

## 🌐 6. Global Error Handling and Middleware

### 6.1 Error Handling Middleware

**File:** `Core/Diagnostics/ProblemDetails/ProblemDetailsMiddleware.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.ProblemDetails;

/// <summary>
/// Global error handling middleware that converts unhandled exceptions to Problem Details
/// </summary>
public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private readonly IErrorHandlerService _errorHandler;
    private readonly ErrorHandlingOptions _options;
    private readonly IHostEnvironment _environment;
    
    public ProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsMiddleware> logger,
        IErrorHandlerService errorHandler,
        IOptions<ErrorHandlingOptions> options,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _errorHandler = errorHandler;
        _options = options.Value;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Don't handle if response has already started
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Cannot handle exception after response has started");
            return;
        }
        
        try
        {
            // Convert exception to structured error
            var error = await _errorHandler.HandleAsync(exception, context.RequestAborted);
            
            // Add correlation context
            var correlationId = context.TraceIdentifier;
            var enrichedError = error
                .WithCorrelationId(correlationId)
                .WithSource("GlobalErrorHandler")
                .WithMetadata("RequestPath", context.Request.Path.Value ?? "unknown")
                .WithMetadata("RequestMethod", context.Request.Method)
                .WithMetadata("UserAgent", context.Request.Headers.UserAgent.ToString());
            
            // Log the error with appropriate level
            LogError(enrichedError, exception);
            
            // Convert to Problem Details
            var problemDetails = enrichedError.ToProblemDetails();
            
            // Add environment-specific details
            if (_environment.IsDevelopment() && _options.IncludeExceptionDetails)
            {
                problemDetails.Extensions.Add("exception", new
                {
                    type = exception.GetType().Name,
                    message = exception.Message,
                    stackTrace = exception.StackTrace?.Split('\n').Take(20) // Limit stack trace size
                });
            }
            
            // Set response
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = problemDetails.Status ?? 500;
            
            // Serialize and write response
            var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = _environment.IsDevelopment()
            });
            
            await context.Response.WriteAsync(json, context.RequestAborted);
            
            // Update metrics
            UpdateErrorMetrics(enrichedError);
        }
        catch (Exception handlerException)
        {
            // Last resort error handling
            _logger.LogCritical(handlerException, 
                "Exception occurred in error handling middleware while processing {OriginalException}",
                exception.GetType().Name);
                
            // Send minimal error response
            await SendMinimalErrorResponse(context);
        }
    }
    
    private void LogError(Error error, Exception exception)
    {
        var logData = error.ToLogData();
        
        using var scope = _logger.BeginScope(logData);
        
        switch (error.Severity)
        {
            case ErrorSeverity.Critical:
            case ErrorSeverity.Fatal:
                _logger.LogCritical(exception, "Critical error occurred: {ErrorMessage}", error.Message);
                break;
            case ErrorSeverity.Error:
                _logger.LogError(exception, "Error occurred: {ErrorMessage}", error.Message);
                break;
            case ErrorSeverity.Warning:
                _logger.LogWarning(exception, "Warning: {ErrorMessage}", error.Message);
                break;
            default:
                _logger.LogInformation(exception, "Info: {ErrorMessage}", error.Message);
                break;
        }
    }
    
    private void UpdateErrorMetrics(Error error)
    {
        // Update metrics if available
        try
        {
            var tags = new TagList
            {
                ["error_type"] = error.Type.ToString(),
                ["error_severity"] = error.Severity.ToString(),
                ["error_code"] = error.Code
            };
            
            if (!string.IsNullOrEmpty(error.Source))
            {
                tags["error_source"] = error.Source;
            }
            
            // Increment error counter
            Metrics.ErrorCounter.Add(1, tags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update error metrics");
        }
    }
    
    private async Task SendMinimalErrorResponse(HttpContext context)
    {
        const string minimalError = """
        {
            "type": "https://httpstatuses.com/500",
            "title": "Internal Server Error",
            "status": 500,
            "detail": "An unexpected error occurred"
        }
        """;
        
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = 500;
        
        await context.Response.WriteAsync(minimalError, context.RequestAborted);
    }
}

/// <summary>
/// Metrics for error handling
/// </summary>
public static class Metrics
{
    public static readonly Counter<int> ErrorCounter = 
        Meter.CreateCounter<int>("errors_total", "count", "Total number of errors by type and severity");
        
    private static readonly Meter Meter = new("BuildingBlocks.Core.Diagnostics");
}
```

### 6.2 Error Handling Configuration

**File:** `Core/Diagnostics/Configuration/ErrorHandlingOptions.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Configuration;

/// <summary>
/// Configuration options for error handling behavior
/// </summary>
public sealed class ErrorHandlingOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "ErrorHandling";
    
    /// <summary>
    /// Include full exception details in error responses (development only)
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = false;
    
    /// <summary>
    /// Maximum stack trace lines to include in responses
    /// </summary>
    public int MaxStackTraceLines { get; set; } = 20;
    
    /// <summary>
    /// Enable PII detection and masking in error messages
    /// </summary>
    public bool EnablePiiMasking { get; set; } = true;
    
    /// <summary>
    /// Regex patterns for PII detection
    /// </summary>
    public List<string> PiiPatterns { get; set; } = new()
    {
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // Email
        @"\b\d{3}-\d{2}-\d{4}\b", // SSN
        @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", // Credit card
        @"\b\d{10,11}\b" // Phone number
    };
    
    /// <summary>
    /// Correlation ID header name
    /// </summary>
    public string CorrelationIdHeader { get; set; } = "X-Correlation-ID";
    
    /// <summary>
    /// Log level for different error severities
    /// </summary>
    public Dictionary<ErrorSeverity, LogLevel> SeverityLogLevels { get; set; } = new()
    {
        [ErrorSeverity.Info] = LogLevel.Information,
        [ErrorSeverity.Warning] = LogLevel.Warning,
        [ErrorSeverity.Error] = LogLevel.Error,
        [ErrorSeverity.Critical] = LogLevel.Critical,
        [ErrorSeverity.Fatal] = LogLevel.Critical
    };
    
    /// <summary>
    /// Error codes that should be treated as warnings instead of errors
    /// </summary>
    public HashSet<string> WarningErrorCodes { get; set; } = new()
    {
        "VALIDATION_ERROR",
        "NOT_FOUND",
        "UNAUTHORIZED",
        "FORBIDDEN"
    };
    
    /// <summary>
    /// Maximum metadata size per error (in bytes)
    /// </summary>
    public int MaxMetadataSize { get; set; } = 4096;
    
    /// <summary>
    /// Enable error response caching
    /// </summary>
    public bool EnableResponseCaching { get; set; } = false;
    
    /// <summary>
    /// Cache duration for error responses
    /// </summary>
    public TimeSpan ResponseCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
```

### 6.3 Service Registration Extensions

**File:** `Core/Diagnostics/Configuration/ErrorHandlingServiceExtensions.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Configuration;

/// <summary>
/// Service collection extensions for error handling registration
/// </summary>
public static class ErrorHandlingServiceExtensions
{
    /// <summary>
    /// Register comprehensive error handling services
    /// </summary>
    public static IServiceCollection AddErrorHandling(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ErrorHandlingOptions>? configureOptions = null)
    {
        // Configure options
        var optionsSection = configuration.GetSection(ErrorHandlingOptions.SectionName);
        services.Configure<ErrorHandlingOptions>(optionsSection);
        
        if (configureOptions != null)
        {
            services.Configure<ErrorHandlingOptions>(configureOptions);
        }
        
        // Register core services
        services.AddSingleton<IErrorHandlerService, ErrorHandlerService>();
        services.AddSingleton<IErrorAggregator, ErrorAggregator>();
        
        // Register default error handlers
        services.AddSingleton<IErrorHandler<Exception>, ExceptionErrorHandler>();
        services.AddSingleton<IErrorHandler<DomainException>, DomainExceptionErrorHandler>();
        services.AddSingleton<IErrorHandler<ValidationException>, ValidationExceptionErrorHandler>();
        services.AddSingleton<IErrorHandler<BusinessRuleException>, BusinessRuleExceptionErrorHandler>();
        
        // Register Problem Details support
        services.AddProblemDetailsSupport();
        
        // Register performance services
        services.AddSingleton<ErrorCache>();
        services.AddSingleton<ErrorMetrics>();
        
        return services;
    }
    
    /// <summary>
    /// Add error handling middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProblemDetailsMiddleware>();
    }
    
    /// <summary>
    /// Register custom error handler
    /// </summary>
    public static IServiceCollection AddErrorHandler<TError>(
        this IServiceCollection services,
        IErrorHandler<TError> handler)
    {
        return services.AddSingleton(handler);
    }
    
    /// <summary>
    /// Register custom error handler with factory
    /// </summary>
    public static IServiceCollection AddErrorHandler<TError>(
        this IServiceCollection services,
        Func<IServiceProvider, IErrorHandler<TError>> factory)
    {
        return services.AddSingleton(factory);
    }
}
```

---

## ⚡ 7. Performance Considerations and Optimizations

### 7.1 Error Performance Metrics

**File:** `Core/Diagnostics/Performance/ErrorMetrics.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// Performance metrics collection for error handling system
/// </summary>
public sealed class ErrorMetrics
{
    private readonly Counter<long> _errorCount;
    private readonly Histogram<double> _errorHandlingDuration;
    private readonly Counter<long> _errorCacheHits;
    private readonly Counter<long> _errorCacheMisses;
    private readonly Gauge<long> _errorMemoryUsage;
    
    public ErrorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("BuildingBlocks.Core.Diagnostics");
        
        _errorCount = meter.CreateCounter<long>(
            "errors_total",
            "count",
            "Total number of errors processed");
            
        _errorHandlingDuration = meter.CreateHistogram<double>(
            "error_handling_duration",
            "milliseconds", 
            "Time spent handling errors");
            
        _errorCacheHits = meter.CreateCounter<long>(
            "error_cache_hits_total",
            "count",
            "Error metadata cache hits");
            
        _errorCacheMisses = meter.CreateCounter<long>(
            "error_cache_misses_total", 
            "count",
            "Error metadata cache misses");
            
        _errorMemoryUsage = meter.CreateObservableGauge<long>(
            "error_memory_usage_bytes",
            "bytes",
            "Memory used by error handling system",
            () => GC.GetTotalMemory(false));
    }
    
    public void RecordError(Error error, TimeSpan duration)
    {
        var tags = new TagList
        {
            ["error_type"] = error.Type.ToString(),
            ["error_severity"] = error.Severity.ToString(),
            ["error_code"] = error.Code
        };
        
        _errorCount.Add(1, tags);
        _errorHandlingDuration.Record(duration.TotalMilliseconds, tags);
    }
    
    public void RecordCacheHit(string key)
    {
        _errorCacheHits.Add(1, new TagList { ["cache_key"] = key });
    }
    
    public void RecordCacheMiss(string key)
    {
        _errorCacheMisses.Add(1, new TagList { ["cache_key"] = key });
    }
}
```

### 7.2 Error Caching System

**File:** `Core/Diagnostics/Performance/ErrorCache.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance caching for error metadata and common error instances
/// </summary>
public sealed class ErrorCache : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ErrorMetrics _metrics;
    private readonly ILogger<ErrorCache> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly Timer _cleanupTimer;
    
    // Pre-built common errors for performance
    private static readonly ConcurrentDictionary<string, Error> CommonErrors = new();
    
    static ErrorCache()
    {
        // Pre-populate common errors to avoid repeated allocations
        CommonErrors["VALIDATION_ERROR"] = Error.Validation("Validation failed");
        CommonErrors["NOT_FOUND"] = Error.NotFound("Resource not found");
        CommonErrors["UNAUTHORIZED"] = Error.Unauthorized();
        CommonErrors["FORBIDDEN"] = Error.Forbidden();
        CommonErrors["INTERNAL_ERROR"] = Error.Internal("Internal server error");
        CommonErrors["TIMEOUT"] = Error.Timeout();
        CommonErrors["CANCELLED"] = Error.Cancelled();
    }
    
    public ErrorCache(
        IMemoryCache cache,
        ErrorMetrics metrics,
        ILogger<ErrorCache> logger)
    {
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
        _semaphore = new SemaphoreSlim(1, 1);
        
        // Cleanup timer every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    /// <summary>
    /// Get or create an error with caching for performance
    /// </summary>
    public async Task<Error> GetOrCreateErrorAsync<T>(
        string key,
        Func<Task<Error>> factory,
        TimeSpan? expiration = null)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(key, out Error? cachedError))
        {
            _metrics.RecordCacheHit(key);
            return cachedError;
        }
        
        _metrics.RecordCacheMiss(key);
        
        // Use semaphore to prevent cache stampede
        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(key, out cachedError))
            {
                return cachedError;
            }
            
            // Create error
            var error = await factory();
            
            // Cache with appropriate expiration
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Size = EstimateErrorSize(error)
            };
            
            _cache.Set(key, error, options);
            return error;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Get common pre-built error for maximum performance
    /// </summary>
    public Error GetCommonError(string errorCode, string? customMessage = null)
    {
        if (CommonErrors.TryGetValue(errorCode, out var commonError))
        {
            _metrics.RecordCacheHit($"common_{errorCode}");
            
            // Return original if no customization needed
            if (string.IsNullOrEmpty(customMessage))
                return commonError;
            
            // Return customized version
            return commonError with { Message = customMessage };
        }
        
        _metrics.RecordCacheMiss($"common_{errorCode}");
        return Error.Internal($"Unknown common error: {errorCode}");
    }
    
    /// <summary>
    /// Pre-warm cache with common error patterns
    /// </summary>
    public async Task PreWarmCacheAsync()
    {
        _logger.LogInformation("Pre-warming error cache with common patterns");
        
        var commonPatterns = new[]
        {
            ("USER_NOT_FOUND", () => Task.FromResult(Error.NotFound("User not found"))),
            ("INVALID_EMAIL", () => Task.FromResult(Error.Validation("Invalid email format"))),
            ("EXPIRED_TOKEN", () => Task.FromResult(Error.Unauthorized("Token has expired"))),
            ("INSUFFICIENT_PERMISSIONS", () => Task.FromResult(Error.Forbidden("Insufficient permissions"))),
            ("DATABASE_TIMEOUT", () => Task.FromResult(Error.Timeout("Database operation timed out")))
        };
        
        var tasks = commonPatterns.Select(async pattern =>
        {
            try
            {
                await GetOrCreateErrorAsync(pattern.Item1, pattern.Item2, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-warm cache entry: {Pattern}", pattern.Item1);
            }
        });
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Error cache pre-warming completed");
    }
    
    /// <summary>
    /// Estimate memory size of an error for cache sizing
    /// </summary>
    private static int EstimateErrorSize(Error error)
    {
        var baseSize = 200; // Base object overhead
        baseSize += error.Code.Length * 2; // String UTF-16
        baseSize += error.Message.Length * 2;
        
        if (error.StackTrace != null)
            baseSize += error.StackTrace.Length * 2;
            
        if (error.CorrelationId != null)
            baseSize += error.CorrelationId.Length * 2;
            
        if (error.Source != null)
            baseSize += error.Source.Length * 2;
            
        if (error.Metadata != null)
        {
            foreach (var kvp in error.Metadata)
            {
                baseSize += kvp.Key.Length * 2;
                baseSize += EstimateObjectSize(kvp.Value);
            }
        }
        
        return baseSize;
    }
    
    private static int EstimateObjectSize(object obj)
    {
        return obj switch
        {
            string str => str.Length * 2,
            int => 4,
            long => 8,
            DateTime => 8,
            TimeSpan => 8,
            _ => 100 // Default estimate for complex objects
        };
    }
    
    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            // Trigger memory cache cleanup
            if (_cache is MemoryCache mc)
            {
                mc.Compact(0.1); // Remove 10% of entries
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during cache cleanup");
        }
    }
    
    public void Dispose()
    {
        _semaphore?.Dispose();
        _cleanupTimer?.Dispose();
    }
}
```

---

## 🧪 8. Comprehensive Testing Strategy

### 8.1 Error Testing Utilities

**File:** `Core/Diagnostics/Testing/ErrorAssertions.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Testing;

/// <summary>
/// FluentAssertions extensions for Error testing
/// </summary>
public static class ErrorAssertions
{
    /// <summary>
    /// Assert that an error has specific properties
    /// </summary>
    public static ErrorAssertionWrapper Should(this Error error)
    {
        return new ErrorAssertionWrapper(error);
    }
    
    /// <summary>
    /// Assert that a Result contains a specific error
    /// </summary>
    public static ResultErrorAssertionWrapper<T> ShouldHaveError<T>(this Result<T> result)
    {
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue("Result should be in failure state");
        return new ResultErrorAssertionWrapper<T>(result);
    }
    
    /// <summary>
    /// Assert that a Result is successful
    /// </summary>
    public static ResultSuccessAssertionWrapper<T> ShouldBeSuccessful<T>(this Result<T> result)
    {
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("Result should be in success state");
        return new ResultSuccessAssertionWrapper<T>(result);
    }
}

public class ErrorAssertionWrapper
{
    private readonly Error _error;
    
    public ErrorAssertionWrapper(Error error)
    {
        _error = error;
    }
    
    public ErrorAssertionWrapper HaveType(ErrorType expectedType, string because = "")
    {
        _error.Type.Should().Be(expectedType, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveCode(string expectedCode, string because = "")
    {
        _error.Code.Should().Be(expectedCode, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveMessage(string expectedMessage, string because = "")
    {
        _error.Message.Should().Be(expectedMessage, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveMessageContaining(string expectedSubstring, string because = "")
    {
        _error.Message.Should().Contain(expectedSubstring, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveSeverity(ErrorSeverity expectedSeverity, string because = "")
    {
        _error.Severity.Should().Be(expectedSeverity, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveCorrelationId(string expectedCorrelationId, string because = "")
    {
        _error.CorrelationId.Should().Be(expectedCorrelationId, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveSource(string expectedSource, string because = "")
    {
        _error.Source.Should().Be(expectedSource, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveMetadata(string key, object expectedValue, string because = "")
    {
        _error.Metadata.Should().NotBeNull(because);
        _error.Metadata!.Should().ContainKey(key, because);
        _error.Metadata[key].Should().Be(expectedValue, because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveMetadataKey(string key, string because = "")
    {
        _error.Metadata.Should().NotBeNull(because);
        _error.Metadata!.Should().ContainKey(key, because);
        return this;
    }
    
    public ErrorAssertionWrapper NotHaveMetadataKey(string key, string because = "")
    {
        if (_error.Metadata != null)
        {
            _error.Metadata.Should().NotContainKey(key, because);
        }
        return this;
    }
    
    public ErrorAssertionWrapper HaveInnerException<T>(string because = "") where T : Exception
    {
        _error.InnerException.Should().NotBeNull(because);
        _error.InnerException.Should().BeOfType<T>(because);
        return this;
    }
    
    public ErrorAssertionWrapper HaveHttpStatusCode(int expectedStatusCode, string because = "")
    {
        _error.ToHttpStatusCode().Should().Be(expectedStatusCode, because);
        return this;
    }
}

public class ResultErrorAssertionWrapper<T>
{
    private readonly Result<T> _result;
    
    public ResultErrorAssertionWrapper(Result<T> result)
    {
        _result = result;
    }
    
    public ErrorAssertionWrapper WithError()
    {
        return new ErrorAssertionWrapper(_result.Error);
    }
    
    public ResultErrorAssertionWrapper<T> WithErrorType(ErrorType expectedType, string because = "")
    {
        _result.Error.Type.Should().Be(expectedType, because);
        return this;
    }
    
    public ResultErrorAssertionWrapper<T> WithErrorCode(string expectedCode, string because = "")
    {
        _result.Error.Code.Should().Be(expectedCode, because);
        return this;
    }
}

public class ResultSuccessAssertionWrapper<T>
{
    private readonly Result<T> _result;
    
    public ResultSuccessAssertionWrapper(Result<T> result)
    {
        _result = result;
    }
    
    public ResultSuccessAssertionWrapper<T> WithValue(T expectedValue, string because = "")
    {
        _result.Value.Should().Be(expectedValue, because);
        return this;
    }
    
    public ResultSuccessAssertionWrapper<T> WithValueMatching(Expression<Func<T, bool>> predicate, string because = "")
    {
        _result.Value.Should().Match(predicate, because);
        return this;
    }
}
```

### 8.2 Error Test Fixtures

**File:** `Core/Diagnostics/Testing/ErrorTestFixture.cs`

```csharp
namespace BuildingBlocks.Core.Diagnostics.Testing;

/// <summary>
/// Test data builders for Error testing scenarios
/// </summary>
public static class ErrorTestFixture
{
    /// <summary>
    /// Create a test error with specified properties
    /// </summary>
    public static ErrorBuilder Create(string code = "TEST_ERROR")
    {
        return new ErrorBuilder(code);
    }
    
    /// <summary>
    /// Create a validation error for testing
    /// </summary>
    public static Error ValidationError(string message = "Test validation error", string code = "VALIDATION_TEST")
    {
        return Error.Validation(message, code);
    }
    
    /// <summary>
    /// Create a business rule error for testing
    /// </summary>
    public static Error BusinessRuleError(string message = "Test business rule error", string code = "BUSINESS_RULE_TEST")
    {
        return Error.BusinessRule(message, code);
    }
    
    /// <summary>
    /// Create an internal error for testing
    /// </summary>
    public static Error InternalError(string message = "Test internal error", Exception? exception = null)
    {
        return Error.Internal(message, "INTERNAL_TEST", exception);
    }
    
    /// <summary>
    /// Create multiple errors for aggregation testing
    /// </summary>
    public static Error[] MultipleErrors(int count = 3)
    {
        return Enumerable.Range(1, count)
            .Select(i => Error.Validation($"Error {i}", $"ERROR_{i}"))
            .ToArray();
    }
    
    /// <summary>
    /// Create a test exception with known properties
    /// </summary>
    public static Exception TestException(string message = "Test exception")
    {
        return new InvalidOperationException(message);
    }
    
    /// <summary>
    /// Create a test business rule
    /// </summary>
    public static IBusinessRule TestBusinessRule(bool isBroken = true, string code = "TEST_RULE", string message = "Test rule violation")
    {
        return new TestBusinessRule(code, message, isBroken);
    }
    
    private class TestBusinessRule : IBusinessRule
    {
        public string Code { get; }
        public string Message { get; }
        public bool IsBroken { get; }
        public IReadOnlyDictionary<string, object>? Context { get; }
        
        public TestBusinessRule(string code, string message, bool isBroken, Dictionary<string, object>? context = null)
        {
            Code = code;
            Message = message;
            IsBroken = isBroken;
            Context = context;
        }
    }
}

/// <summary>
/// Fluent builder for creating test errors
/// </summary>
public class ErrorBuilder
{
    private string _code;
    private string _message = "Test error message";
    private ErrorType _type = ErrorType.Internal;
    private ErrorSeverity _severity = ErrorSeverity.Error;
    private Exception? _innerException;
    private Dictionary<string, object>? _metadata;
    private string? _correlationId;
    private string? _source;
    
    public ErrorBuilder(string code)
    {
        _code = code;
    }
    
    public ErrorBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }
    
    public ErrorBuilder WithType(ErrorType type)
    {
        _type = type;
        return this;
    }
    
    public ErrorBuilder WithSeverity(ErrorSeverity severity)
    {
        _severity = severity;
        return this;
    }
    
    public ErrorBuilder WithInnerException(Exception exception)
    {
        _innerException = exception;
        return this;
    }
    
    public ErrorBuilder WithMetadata(string key, object value)
    {
        _metadata ??= new Dictionary<string, object>();
        _metadata[key] = value;
        return this;
    }
    
    public ErrorBuilder WithCorrelationId(string correlationId)
    {
        _correlationId = correlationId;
        return this;
    }
    
    public ErrorBuilder WithSource(string source)
    {
        _source = source;
        return this;
    }
    
    public Error Build()
    {
        var error = _type switch
        {
            ErrorType.Validation => Error.Validation(_message, _code, _metadata),
            ErrorType.NotFound => Error.NotFound(_message, _code, _metadata),
            ErrorType.BusinessRule => Error.BusinessRule(_message, _code, _metadata),
            ErrorType.Internal => Error.Internal(_message, _code, _innerException, _metadata),
            _ => Error.Internal(_message, _code, _innerException, _metadata)
        };
        
        if (!string.IsNullOrEmpty(_correlationId))
        {
            error = error.WithCorrelationId(_correlationId);
        }
        
        if (!string.IsNullOrEmpty(_source))
        {
            error = error.WithSource(_source);
        }
        
        if (_severity != ErrorSeverity.Error)
        {
            error = error.WithSeverity(_severity);
        }
        
        return error;
    }
    
    public static implicit operator Error(ErrorBuilder builder) => builder.Build();
}
```

---

## 🔄 9. Migration Guide

### 9.1 Migration from Legacy Error Handling

**Migration Strategy Document**

```markdown
# Migration Guide: Legacy Error Handling to Enhanced Error System

## Phase 1: Preparation (Week 1)
1. **Audit Current Error Handling**
   - Identify all exception throwing patterns
   - Document current error response formats
   - Map existing error codes to new ErrorType enum

2. **Install Enhanced Error System**
   - Add new error handling packages
   - Configure error handling options
   - Set up middleware (disabled initially)

## Phase 2: Gradual Migration (Weeks 2-4)
1. **Start with New Features**
   - Use Error system for all new code
   - Convert new command/query handlers to Result<T>

2. **Convert Core Domain Logic**
   - Replace domain exceptions with Error returns
   - Update aggregate methods to return Result<T>
   - Migrate business rule validation

3. **Update Application Layer**
   - Convert command/query handlers one by one
   - Update validation to use new Error aggregation
   - Enable Result<T> in MediatR behaviors

## Phase 3: Infrastructure Migration (Week 5)
1. **Update Web Layer**
   - Enable global error middleware
   - Convert controller actions to use Result<T>
   - Update API contracts for new error format

2. **Update Persistence Layer**
   - Convert repository methods to Result<T>
   - Handle database errors with new Error types

## Phase 4: Cleanup and Optimization (Week 6)
1. **Remove Legacy Code**
   - Delete old exception classes
   - Remove legacy error handling middleware
   - Clean up unused error response DTOs

2. **Performance Optimization**
   - Enable error caching
   - Optimize error serialization
   - Tune performance metrics

## Code Transformation Examples

### Before: Legacy Exception Handling
```csharp
public async Task<User> GetUserAsync(UserId id)
{
    var user = await _repository.GetByIdAsync(id);
    if (user == null)
        throw new UserNotFoundException($"User {id} not found");
    
    return user;
}
```

### After: Enhanced Error System
```csharp
public async Task<Result<User>> GetUserAsync(UserId id)
{
    var user = await _repository.GetByIdAsync(id);
    if (user == null)
        return Result<User>.Failure(
            Error.NotFound($"User {id} not found", "USER_NOT_FOUND")
                .WithMetadata("UserId", id.ToString()));
    
    return Result<User>.Success(user);
}
```

### Before: Legacy Validation
```csharp
public void ValidateCreateUser(CreateUserRequest request)
{
    if (string.IsNullOrEmpty(request.Email))
        throw new ValidationException("Email is required");
    
    if (request.Age < 18)
        throw new ValidationException("User must be 18 or older");
}
```

### After: Enhanced Error System
```csharp
public Result ValidateCreateUser(CreateUserRequest request)
{
    var errors = new List<Error>();
    
    if (string.IsNullOrEmpty(request.Email))
        errors.Add(Error.Validation("Email is required", "EMAIL_REQUIRED"));
    
    if (request.Age < 18)
        errors.Add(Error.Validation("User must be 18 or older", "AGE_MINIMUM"));
    
    return errors.Any() 
        ? Result.Failure(Error.Aggregate(errors.ToArray()))
        : Result.Success();
}
```
```

---

## 📊 10. Success Metrics and Monitoring

### 10.1 Key Performance Indicators

**Error Handling System KPIs:**

1. **Performance Metrics**
   - Error processing latency: < 2ms (95th percentile)
   - Memory usage: < 10MB for error cache
   - CPU overhead: < 1% during normal operations

2. **Quality Metrics**
   - Error categorization accuracy: > 95%
   - PII detection rate: > 99%
   - Error correlation success: > 98%

3. **Operational Metrics**
   - Error handling availability: > 99.9%
   - Cache hit rate: > 80%
   - Alert false positive rate: < 5%

### 10.2 Monitoring Dashboard

**Required Monitoring Elements:**

```json
{
  "dashboard": {
    "title": "Enhanced Error System Monitoring",
    "panels": [
      {
        "title": "Error Rate by Type",
        "metric": "errors_total",
        "groupBy": ["error_type", "error_severity"]
      },
      {
        "title": "Error Handling Performance",
        "metric": "error_handling_duration",
        "percentiles": [50, 90, 95, 99]
      },
      {
        "title": "Cache Performance",
        "metrics": ["error_cache_hits_total", "error_cache_misses_total"],
        "calculation": "hit_rate"
      },
      {
        "title": "Memory Usage",
        "metric": "error_memory_usage_bytes",
        "threshold": "10MB"
      }
    ],
    "alerts": [
      {
        "name": "High Error Rate",
        "condition": "rate(errors_total[5m]) > 100",
        "severity": "warning"
      },
      {
        "name": "Error System Unavailable",
        "condition": "up{job='error-system'} == 0",
        "severity": "critical"
      }
    ]
  }
}
```

---

## 🎯 11. Acceptance Criteria and Definition of Done

### **Epic Completion Criteria**

✅ **Core Implementation Complete**
- [ ] All error types and severity levels implemented with comprehensive categorization
- [ ] Rich metadata system operational with telemetry integration  
- [ ] Exception-to-error conversion with smart categorization
- [ ] Result pattern integration with fluent extensions
- [ ] Business rule and validation exception handling

✅ **Integration Points Functional**
- [ ] Pipeline Behaviors integration (ValidationBehavior error aggregation)
- [ ] ASP.NET Core Problem Details middleware with RFC 7807 compliance
- [ ] OpenTelemetry and structured logging integration
- [ ] Global error handling middleware operational

✅ **Performance and Quality Standards Met**
- [ ] Error processing latency < 2ms (95th percentile)
- [ ] Memory usage < 10MB for error cache
- [ ] 100% test coverage with comprehensive scenarios
- [ ] Zero compiler warnings or static analysis issues
- [ ] Performance benchmarks validate < 1% CPU overhead

✅ **Production Readiness Achieved**
- [ ] Configuration system operational with environment-specific settings
- [ ] Security measures (PII masking, sanitization) functional  
- [ ] Monitoring dashboards and alerting configured
- [ ] Migration guide tested with legacy code conversion
- [ ] Documentation complete with troubleshooting guides

✅ **Testing and Validation Complete**
- [ ] Unit tests with 100% coverage including edge cases
- [ ] Integration tests covering full error handling pipeline
- [ ] Load testing validates performance under concurrent usage
- [ ] Error injection testing confirms resilience patterns

---

**END OF EPIC 3: ENHANCED ERROR SYSTEM - VERSION 2.0**