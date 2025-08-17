using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Immutable, observability-friendly Error with rich factory methods,
/// accurate categorization, retry/transient hints, and RFC7807 conversion.
/// </summary>
public sealed record Error
{
    #region Properties

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public ErrorSeverity Severity { get; init; }
    public Exception? InnerException { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>Optional nested causes when the error aggregates multiple failures.</summary>
    public IReadOnlyList<Error>? Causes { get; init; }

#if DEBUG
    public string? StackTrace { get; }
#endif

    public DateTime OccurredAt { get; }
    public string? CorrelationId { get; init; }
    public string? Source { get; init; }

    /// <summary>Whether the error is likely transient (good candidate for retry).</summary>
    public bool IsTransient =>
        Type is ErrorType.Timeout or ErrorType.Network or ErrorType.External or ErrorType.Unavailable 
            or ErrorType.ExternalService or ErrorType.System;

    /// <summary>Whether retrying is recommended (transient and not user-caused).</summary>
    public bool IsRetryable => IsTransient && Severity <= ErrorSeverity.Error;

    #endregion

    #region Internals

    private static readonly ConcurrentDictionary<string, string> InternedCodes = new();

    private static readonly Dictionary<ErrorType, ErrorSeverity> DefaultSeverity = new()
    {
        { ErrorType.Validation, ErrorSeverity.Warning },
        { ErrorType.Serialization, ErrorSeverity.Warning },
        { ErrorType.NotFound, ErrorSeverity.Info },
        { ErrorType.Conflict, ErrorSeverity.Warning },
        { ErrorType.Concurrency, ErrorSeverity.Warning },
        { ErrorType.PreconditionFailed, ErrorSeverity.Warning },
        { ErrorType.BusinessRule, ErrorSeverity.Warning },
        { ErrorType.Unauthorized, ErrorSeverity.Warning },
        { ErrorType.Forbidden, ErrorSeverity.Warning },
        { ErrorType.RateLimit, ErrorSeverity.Warning },
        { ErrorType.Cancelled, ErrorSeverity.Info },
        { ErrorType.Cancellation, ErrorSeverity.Info },

        { ErrorType.Internal, ErrorSeverity.Critical },
        { ErrorType.Configuration, ErrorSeverity.Critical },
        { ErrorType.External, ErrorSeverity.Error },
        { ErrorType.Network, ErrorSeverity.Error },
        { ErrorType.Timeout, ErrorSeverity.Warning },
        { ErrorType.Unavailable, ErrorSeverity.Error },
        { ErrorType.Persistence, ErrorSeverity.Critical },

        // Additional types for compatibility
        { ErrorType.InternalError, ErrorSeverity.Critical },
        { ErrorType.ExternalService, ErrorSeverity.Error },
        { ErrorType.System, ErrorSeverity.Critical },

        { ErrorType.Aggregate, ErrorSeverity.Error },
        { ErrorType.Security, ErrorSeverity.Fatal }
    };

    private Error(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity? severity = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        IReadOnlyList<Error>? causes = null,
        string? stackTrace = null,
        DateTime? occurredAt = null,
        string? correlationId = null,
        string? source = null)
    {
        Code = Intern(code);
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Severity = severity ?? DefaultSeverity.GetValueOrDefault(type, ErrorSeverity.Error);
        InnerException = innerException;
        Metadata = metadata;
        Causes = causes;

#if DEBUG
        StackTrace = stackTrace ?? innerException?.StackTrace ?? Environment.StackTrace;
#endif

        OccurredAt = occurredAt ?? DateTime.UtcNow;
        CorrelationId = correlationId;
        Source = source;
    }

    private static string Intern(string code) => InternedCodes.GetOrAdd(code, c => string.Intern(c));

    #endregion

    #region Factory: 4xx

    public static Error Validation(string message, string code = "VALIDATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Validation, metadata: metadata);

    public static Error Serialization(string message, string code = "SERIALIZATION_ERROR",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Serialization, metadata: metadata);

    public static Error NotFound(string message, string code = "NOT_FOUND",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.NotFound, metadata: metadata);

    public static Error Conflict(string message, string code = "CONFLICT",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Conflict, metadata: metadata);

    public static Error Concurrency(string message, string code = "CONCURRENCY_CONFLICT",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Concurrency, metadata: metadata);

    public static Error PreconditionFailed(string message, string code = "PRECONDITION_FAILED",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.PreconditionFailed, metadata: metadata);

    public static Error BusinessRule(string message, string code = "BUSINESS_RULE_VIOLATION",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.BusinessRule, metadata: metadata);

    public static Error Unauthorized(string? message = "Access denied - authentication required",
        string code = "UNAUTHORIZED",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message ?? "Access denied", ErrorType.Unauthorized, metadata: metadata);

    public static Error Forbidden(string? message = "Access denied - insufficient permissions",
        string code = "FORBIDDEN",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message ?? "Access denied", ErrorType.Forbidden, metadata: metadata);

    public static Error RateLimit(string? message = "Rate limit exceeded",
        string code = "RATE_LIMIT_EXCEEDED", TimeSpan? retryAfter = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = Merge(metadata, retryAfter is null ? null : new Dictionary<string, object>
        {
            ["RetryAfter"] = retryAfter.Value.TotalSeconds
        });
        return new(code, message ?? "Rate limit exceeded", ErrorType.RateLimit, metadata: meta);
    }

    public static Error Cancelled(string? message = "Operation was cancelled",
        string code = "OPERATION_CANCELLED",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message ?? "Operation was cancelled", ErrorType.Cancelled, metadata: metadata);

    #endregion

    #region Factory: Security

    public static Error Security(string message, string code = "SECURITY_VIOLATION",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Security, metadata: metadata);

    #endregion

    #region Factory: 5xx

    public static Error Internal(string message, string code = "INTERNAL_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Internal, innerException: exception, metadata: metadata);

    public static Error Configuration(string message, string code = "CONFIGURATION_ERROR",
        string? configKey = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = Merge(metadata, configKey is null ? null : new Dictionary<string, object>
        {
            ["ConfigurationKey"] = configKey
        });
        return new(code, message, ErrorType.Configuration, metadata: meta);
    }

    public static Error External(string message, string code = "EXTERNAL_SERVICE_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.External, innerException: exception, metadata: metadata);

    public static Error Network(string message, string code = "NETWORK_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Network, innerException: exception, metadata: metadata);

    public static Error Timeout(string? message = "Operation timed out", string code = "TIMEOUT",
        TimeSpan? timeout = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        var meta = Merge(metadata, timeout is null ? null : new Dictionary<string, object>
        {
            ["TimeoutDuration"] = timeout.Value.ToString()
        });
        return new(code, message ?? "Operation timed out", ErrorType.Timeout, metadata: meta);
    }

    public static Error Unavailable(string? message = "Dependency/service unavailable",
        string code = "SERVICE_UNAVAILABLE",
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message ?? "Service unavailable", ErrorType.Unavailable, metadata: metadata);

    public static Error Persistence(string message, string code = "PERSISTENCE_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Persistence, innerException: exception, metadata: metadata);

    #endregion

    #region Factory: General

    /// <summary>General failure factory method that defaults to Internal error type.</summary>
    public static Error Failure(string code, string message, 
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Internal, innerException: exception, metadata: metadata);

    #endregion

    #region Factory: Compatibility Aliases

    /// <summary>Alias for Error.Internal() for backward compatibility.</summary>
    public static Error InternalError(string message, string code = "INTERNAL_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.InternalError, innerException: exception, metadata: metadata);

    /// <summary>Alias for Error.External() for backward compatibility.</summary>
    public static Error ExternalService(string message, string code = "EXTERNAL_SERVICE_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.ExternalService, innerException: exception, metadata: metadata);

    /// <summary>System-level error factory method.</summary>
    public static Error System(string message, string code = "SYSTEM_ERROR",
        Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.System, innerException: exception, metadata: metadata);

    #endregion

    #region Factory: Aggregate

    public static Error Aggregate(string message, string code = "AGGREGATE_ERROR",
        IEnumerable<Error>? causes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var causeList = causes?.ToList();
        var meta = new Dictionary<string, object>
        {
            ["ErrorCount"] = causeList?.Count ?? 0
        };
        if (causeList?.Count > 0)
        {
            meta["Errors"] = causeList.Select(e => new { e.Code, e.Message, Type = e.Type.ToString() }).ToArray();
        }
        MergeInto(meta, metadata);
        return new(code, message, ErrorType.Aggregate, metadata: meta, causes: causeList);
    }

    public static Error Aggregate(params Error[] errors)
    {
        if (errors is null || errors.Length == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));
        if (errors.Length == 1) return errors[0];

        var combined = string.Join("; ", errors.Select(e => e.Message));
        return Aggregate($"Multiple errors occurred: {combined}", causes: errors);
    }

    #endregion

    #region Exception Mapping

    /// <summary>
    /// Smart exception categorization and Error creation (state of the art).
    /// </summary>
    public static Error FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Dedicated cases (order matters)
        switch (exception)
        {
            case ArgumentNullException or ArgumentOutOfRangeException or FormatException:
                return Validation(exception.Message, "INVALID_ARGUMENT", ExceptionMeta(exception));

            case JsonException:
                return Serialization(exception.Message, "JSON_SERIALIZATION_ERROR", ExceptionMeta(exception));

            case HttpRequestException httpEx:
            {
                // Map by StatusCode when available (net5+)
                var status = httpEx.StatusCode;
                if (status is HttpStatusCode.Unauthorized) return Unauthorized(httpEx.Message, "HTTP_401", ExceptionMeta(httpEx));
                if (status is HttpStatusCode.Forbidden)    return Forbidden(httpEx.Message, "HTTP_403", ExceptionMeta(httpEx));
                if (status is HttpStatusCode.NotFound)     return NotFound(httpEx.Message, "HTTP_404", ExceptionMeta(httpEx));
                if (status is HttpStatusCode.TooManyRequests) return RateLimit(httpEx.Message, "HTTP_429", null, ExceptionMeta(httpEx));
                if (status is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout) return Timeout(httpEx.Message, "HTTP_TIMEOUT", null, ExceptionMeta(httpEx));
                if (status is HttpStatusCode.ServiceUnavailable) return Unavailable(httpEx.Message, "HTTP_503", ExceptionMeta(httpEx));
                if (status is HttpStatusCode.BadGateway)   return External(httpEx.Message, "HTTP_502", httpEx, ExceptionMeta(httpEx));
                if (status is HttpStatusCode.Conflict)     return Conflict(httpEx.Message, "HTTP_409", ExceptionMeta(httpEx));
                if (status is HttpStatusCode.PreconditionFailed) return PreconditionFailed(httpEx.Message, "HTTP_412", ExceptionMeta(httpEx));

                // Fallback: external dependency problem
                return External(httpEx.Message, "HTTP_REQUEST_FAILED", httpEx, ExceptionMeta(httpEx));
            }

            case DbUpdateConcurrencyException:
                return Concurrency(exception.Message, "DB_CONCURRENCY", ExceptionMeta(exception));

            case DbUpdateException or DbException:
                return Persistence(exception.Message, "DATABASE_ERROR", exception, ExceptionMeta(exception));

            case TaskCanceledException tce when tce.CancellationToken.IsCancellationRequested:
                return Cancelled(exception.Message, "TASK_CANCELLED", ExceptionMeta(exception));

            case OperationCanceledException oce when oce.CancellationToken.IsCancellationRequested:
                return Cancelled(exception.Message, "OPERATION_CANCELLED", ExceptionMeta(exception));

            case TimeoutException:
                return Timeout(exception.Message, "OPERATION_TIMEOUT", null, ExceptionMeta(exception));

            case SocketException or NetworkInformationException or IOException:
                return Network(exception.Message, "NETWORK_FAILURE", exception, ExceptionMeta(exception));

            case SecurityException:
                return Security(exception.Message, "SECURITY_VIOLATION", ExceptionMeta(exception));

            case InvalidOperationException:
                return BusinessRule(exception.Message, "INVALID_OPERATION", ExceptionMeta(exception));

            case UnauthorizedAccessException:
                return Unauthorized(exception.Message, "ACCESS_DENIED", ExceptionMeta(exception));
        }

        // Catch-all
        return Internal(exception.Message, "UNHANDLED_EXCEPTION", exception, ExceptionMeta(exception));
    }

    private static Dictionary<string, object> ExceptionMeta(Exception exception) =>
        new()
        {
            ["ExceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["Source"] = exception.Source ?? "Unknown",
            ["HResult"] = exception.HResult
        };

    #endregion

    #region Builders

    public Error WithMetadata(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var meta = Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        meta[key] = value;
        return this with { Metadata = meta };
    }

    public Error WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var meta = Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        foreach (var (k, v) in metadata) meta[k] = v;
        return this with { Metadata = meta };
    }

    public Error WithCorrelationId(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        return this with { CorrelationId = correlationId };
    }

    /// <summary>Sets CorrelationId from Activity.Current.TraceId (W3C); no-op if no Activity.</summary>
    public Error WithCorrelationFromActivity()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId) ? this : this with { CorrelationId = traceId };
    }

    public Error WithSeverity(ErrorSeverity severity) => this with { Severity = severity };

    public Error WithSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return this with { Source = source };
    }

    public Error WithInnerException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return this with { InnerException = exception };
    }

    /// <summary>Returns a copy with sensitive metadata keys redacted (token, password, secret, authorization, cookie).</summary>
    public Error Redact(params string[]? extraKeys)
    {
        if (Metadata is null || Metadata.Count == 0) return this;

        var sensitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password","token","access_token","refresh_token","secret","client_secret","authorization","cookie","set-cookie","api_key","apikey","connectionstring"
        };
        if (extraKeys is { Length: > 0 }) foreach (var k in extraKeys) sensitive.Add(k);

        var meta = new Dictionary<string, object>();
        foreach (var (k, v) in Metadata)
        {
            meta[k] = sensitive.Contains(k) ? "***REDACTED***" : v;
        }
        return this with { Metadata = meta };
    }

    #endregion

    #region Conversions

    /// <summary>HTTP status as enum.</summary>
    public HttpStatusCode ToHttpStatus() => Type switch
    {
        ErrorType.Validation        => HttpStatusCode.BadRequest,          // 400
        ErrorType.Serialization     => HttpStatusCode.BadRequest,          // 400
        ErrorType.Unauthorized      => HttpStatusCode.Unauthorized,        // 401
        ErrorType.Forbidden         => HttpStatusCode.Forbidden,           // 403
        ErrorType.NotFound          => HttpStatusCode.NotFound,            // 404
        ErrorType.Conflict          => HttpStatusCode.Conflict,            // 409
        ErrorType.Concurrency       => HttpStatusCode.Conflict,            // or 412 depending on API policy
        ErrorType.PreconditionFailed=> HttpStatusCode.PreconditionFailed,  // 412
        ErrorType.BusinessRule      => (HttpStatusCode)422,                // Unprocessable Entity
        ErrorType.Aggregate         => (HttpStatusCode)422,
        ErrorType.RateLimit         => (HttpStatusCode)429,
        ErrorType.Configuration     => HttpStatusCode.InternalServerError, // 500
        ErrorType.Internal          => HttpStatusCode.InternalServerError, // 500
        ErrorType.External          => HttpStatusCode.BadGateway,          // 502
        ErrorType.Network           => HttpStatusCode.BadGateway,          // 502
        ErrorType.Unavailable       => HttpStatusCode.ServiceUnavailable,  // 503
        ErrorType.Timeout           => HttpStatusCode.GatewayTimeout,      // 504
        ErrorType.Persistence       => (HttpStatusCode)507,                // Insufficient Storage
        ErrorType.Security          => HttpStatusCode.Forbidden,           // 403
        ErrorType.Cancelled         => (HttpStatusCode)499,                // Client Closed Request (non-standard)
        ErrorType.Cancellation      => (HttpStatusCode)499,                // Client Closed Request (non-standard)
        
        // Compatibility aliases
        ErrorType.InternalError      => HttpStatusCode.InternalServerError, // 500
        ErrorType.ExternalService    => HttpStatusCode.BadGateway,          // 502
        ErrorType.System             => HttpStatusCode.InternalServerError, // 500
        
        _                           => HttpStatusCode.InternalServerError
    };

    /// <summary>HTTP status as int (back-compat).</summary>
    public int ToHttpStatusCode() => (int)ToHttpStatus();

    /// <summary>Structured payload for logging/analytics.</summary>
    public Dictionary<string, object> ToLogData()
    {
        var data = new Dictionary<string, object>
        {
            ["ErrorCode"] = Code,
            ["ErrorMessage"] = Message,
            ["ErrorType"] = Type.ToString(),
            ["ErrorSeverity"] = Severity.ToString(),
            ["OccurredAt"] = OccurredAt,
            ["HttpStatusCode"] = ToHttpStatusCode()
        };
        if (!string.IsNullOrWhiteSpace(CorrelationId)) data["CorrelationId"] = CorrelationId;
        if (!string.IsNullOrWhiteSpace(Source)) data["Source"] = Source;

        if (InnerException is not null)
        {
            data["ExceptionType"] = InnerException.GetType().FullName!;
            data["ExceptionMessage"] = InnerException.Message;
        }

        if (Metadata is not null)
        {
            foreach (var (k, v) in Metadata) data[$"Metadata_{k}"] = v!;
        }

        if (Causes is { Count: > 0 })
        {
            data["Causes"] = Causes.Select(c => new
            {
                c.Code, c.Message, Type = c.Type.ToString(), Status = c.ToHttpStatusCode()
            }).ToArray();
        }

        return data;
    }

    /// <summary>
    /// RFC 7807 Problem Details (framework-agnostic DTO).
    /// </summary>
    public Problem ToProblem(string? instance = null, string? type = null, string? title = null)
    {
        var status = ToHttpStatusCode();
        var problem = new Problem
        {
            Type = type ?? $"https://httpstatuses.com/{status}",
            Title = title ?? Type.ToString(),
            Status = status,
            Detail = Message,
            Instance = instance,
            TraceId = CorrelationId // W3C: correlation == trace id
        };

        // Attach extension fields (code, severity, source, metadata, causes)
        problem.Extensions["code"] = Code;
        problem.Extensions["severity"] = Severity.ToString();
        if (!string.IsNullOrWhiteSpace(Source)) problem.Extensions["source"] = Source;
        if (Metadata is not null && Metadata.Count > 0) problem.Extensions["metadata"] = Metadata;
        if (Causes is { Count: > 0 })
        {
            problem.Extensions["causes"] = Causes.Select(c => new
            {
                c.Code, c.Message, type = c.Type.ToString(), status = c.ToHttpStatusCode()
            }).ToArray();
        }

        return problem;
    }

    public override string ToString()
    {
        var parts = new List<string> { $"[{Type}]", $"{Code}:", Message };
        if (!string.IsNullOrEmpty(Source)) parts.Insert(1, $"({Source})");
        if (!string.IsNullOrEmpty(CorrelationId)) parts.Add($"[CorrelationId: {CorrelationId}]");
        return string.Join(" ", parts);
    }

    #endregion

    #region Helpers

    private static IReadOnlyDictionary<string, object>? Merge(
        IReadOnlyDictionary<string, object>? a,
        Dictionary<string, object>? b)
    {
        if ((a is null || a.Count == 0) && (b is null || b.Count == 0)) return a ?? b;
        var m = new Dictionary<string, object>();
        if (a is { Count: > 0 }) foreach (var (k, v) in a) m[k] = v;
        if (b is { Count: > 0 }) foreach (var (k, v) in b) m[k] = v;
        return m;
    }

    private static void MergeInto(Dictionary<string, object> target, IReadOnlyDictionary<string, object>? source)
    {
        if (source is null) return;
        foreach (var (k, v) in source) target[k] = v;
    }

    #endregion

    #region DTOs

    /// <summary>
    /// Minimal RFC 7807 ProblemDetails-like DTO (no ASP.NET dependency).
    /// </summary>
    public sealed class Problem
    {
        public string? Type { get; init; }
        public string? Title { get; init; }
        public int Status { get; init; }
        public string? Detail { get; init; }
        public string? Instance { get; init; }

        /// <summary>W3C trace id == correlation id.</summary>
        public string? TraceId { get; init; }

        /// <summary>Additional fields (code, severity, metadata, causes, etc.).</summary>
        public Dictionary<string, object> Extensions { get; } = new();
    }

    #endregion
}
