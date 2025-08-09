using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Correlation and distributed tracing extensions for Result&lt;T&gt;.
/// In W3C mode, the correlation id is always the current Activity TraceId.
/// </summary>
public static class ResultCorrelationExtensions
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";
    private const string ResultContextHeaderName = "X-Result-Context";

    #region Correlation ID Management

    /// <summary>
    /// Ensure Result has a correlation ID (W3C trace id). Uses current Activity when available.
    /// </summary>
    public static Result<T> EnsureCorrelationId<T>(
        this Result<T> result,
        string? correlationId = null,
        [CallerMemberName] string? memberName = null)
    {
        if (result.IsFailure && string.IsNullOrWhiteSpace(result.Error.CorrelationId))
        {
            // precedence: explicit -> Activity.TraceId -> random W3C trace id
            var id = correlationId
                     ?? Activity.Current?.TraceId.ToString()
                     ?? GenerateW3CTraceId();

            var updatedError = result.Error.WithCorrelationId(id);
            return Result<T>.Failure(updatedError);
        }

        return result;
    }

    /// <summary>
    /// Set correlation ID from current Activity (W3C trace id).
    /// </summary>
    public static Result<T> WithActivityCorrelationId<T>(this Result<T> result)
    {
        var activity = Activity.Current;
        if (activity == null || result.IsSuccess) return result;

        var correlationId = activity.TraceId.ToString();
        var updatedError = result.Error.WithCorrelationId(correlationId);
        return Result<T>.Failure(updatedError);
    }

    /// <summary>
    /// Extract correlation ID (trace id) from HTTP context and apply to Result.
    /// Prefers W3C traceparent header; falls back to X-Correlation-ID.
    /// </summary>
    public static Result<T> WithHttpCorrelationId<T>(
        this Result<T> result,
        HttpContext? httpContext)
    {
        if (httpContext?.Request?.Headers == null)
            return result;

        var correlationId = ExtractCorrelationIdFromHeaders(httpContext.Request.Headers);
        if (string.IsNullOrWhiteSpace(correlationId) || result.IsSuccess)
            return result;

        var updatedError = result.Error.WithCorrelationId(correlationId);
        return Result<T>.Failure(updatedError);
    }

    #endregion

    #region Trace Context Propagation

    /// <summary>
    /// Create trace context from current Result for downstream calls.
    /// Ensures CorrelationId == TraceId (W3C).
    /// </summary>
    public static TraceContext CreateTraceContext<T>(
        this Result<T> result,
        string? serviceName = null,
        [CallerMemberName] string? operationName = null)
    {
        var activity = Activity.Current;

        // Prefer current Activity trace id; else the Result error correlation; else create one
        var traceId = activity?.TraceId.ToString()
                      ?? (result.IsFailure ? result.Error.CorrelationId : null)
                      ?? GenerateW3CTraceId();

        var spanId = activity?.SpanId.ToString();
        var parentSpanId = activity?.ParentSpanId.ToString();

        return new TraceContext
        {
            CorrelationId = traceId,         // <-- equal to TraceId
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            TraceFlags = activity?.ActivityTraceFlags.ToString(),
            TraceState = activity?.TraceStateString,
            ServiceName = serviceName,
            OperationName = operationName,
            ResultState = result.IsSuccess ? "success" : "failure",
            ErrorCode = result.IsFailure ? result.Error.Code : null,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Apply trace context to outgoing HTTP headers (W3C). Also sets X-Correlation-ID for legacy consumers.
    /// </summary>
    public static void ApplyToHttpHeaders(
        this TraceContext traceContext,
        IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(traceContext);

        var traceId = traceContext.TraceId ?? traceContext.CorrelationId;

        // Ensure we have a SpanId to compose a valid traceparent
        var spanId = !string.IsNullOrEmpty(traceContext.SpanId)
            ? traceContext.SpanId
            : ActivitySpanId.CreateRandom().ToString();

        var traceFlags = string.IsNullOrEmpty(traceContext.TraceFlags) ? "00" : traceContext.TraceFlags;

        // W3C headers
        headers[TraceParentHeaderName] = $"00-{traceId}-{spanId}-{traceFlags}";
        if (!string.IsNullOrEmpty(traceContext.TraceState))
        {
            headers[TraceStateHeaderName] = traceContext.TraceState!;
        }

        // Back-compat header uses the W3C trace id
        headers[CorrelationIdHeaderName] = traceId;

        // Custom result context header
        var resultContext = JsonSerializer.Serialize(new
        {
            service = traceContext.ServiceName,
            operation = traceContext.OperationName,
            result_state = traceContext.ResultState,
            error_code = traceContext.ErrorCode,
            timestamp = traceContext.Timestamp
        });

        headers[ResultContextHeaderName] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(resultContext));
    }

    /// <summary>
    /// Create Activity from trace context for incoming operations.
    /// Correlation id tag is set to the W3C trace id.
    /// </summary>
    public static Activity? CreateActivityFromTraceContext(
        this TraceContext traceContext,
        ActivitySource activitySource,
        string activityName,
        ActivityKind kind = ActivityKind.Server)
    {
        ActivityContext parentContext = default;

        if (!string.IsNullOrEmpty(traceContext.TraceId) && !string.IsNullOrEmpty(traceContext.SpanId) &&
            ActivityTraceId.TryParse(traceContext.TraceId, out var traceId) &&
            ActivitySpanId.TryParse(traceContext.SpanId, out var spanId))
        {
            var traceFlags = Enum.TryParse<ActivityTraceFlags>(traceContext.TraceFlags, out var flags)
                ? flags
                : ActivityTraceFlags.None;

            parentContext = new ActivityContext(traceId, spanId, traceFlags, traceContext.TraceState);
        }

        var activity = activitySource.StartActivity(activityName, kind, parentContext);

        if (activity != null)
        {
            var corr = traceContext.TraceId ?? traceContext.CorrelationId;
            activity.SetTag("correlation.id", corr); // equals trace id
            activity.SetTag("source.service", traceContext.ServiceName);
            activity.SetTag("source.operation", traceContext.OperationName);
            activity.SetTag("source.result_state", traceContext.ResultState);

            if (!string.IsNullOrEmpty(traceContext.ErrorCode))
            {
                activity.SetTag("source.error_code", traceContext.ErrorCode);
            }
        }

        return activity;
    }

    #endregion

    #region Cross-Service Result Propagation

    /// <summary>
    /// Serialize Result context for cross-service communication.
    /// Always includes correlation_id (W3C trace id when available).
    /// </summary>
    public static string SerializeResultContext<T>(
        this Result<T> result,
        string? serviceName = null,
        [CallerMemberName] string? operationName = null)
    {
        var corr = Activity.Current?.TraceId.ToString()
                   ?? (result.IsFailure ? result.Error.CorrelationId : null)
                   ?? GenerateW3CTraceId();

        var context = new
        {
            service_name = serviceName,
            operation_name = operationName,
            result_type = typeof(T).Name,
            is_success = result.IsSuccess,
            timestamp = DateTimeOffset.UtcNow,
            correlation_id = corr,
            error = result.IsFailure ? new
            {
                code = result.Error.Code,
                message = result.Error.Message,
                type = result.Error.Type.ToString(),
                severity = result.Error.Severity.ToString(),
                source = result.Error.Source
            } : null
        };

        var json = JsonSerializer.Serialize(context);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Deserialize and apply Result context from cross-service communication.
    /// </summary>
    public static Result<T> ApplySerializedResultContext<T>(
        this Result<T> result,
        string serializedContext,
        ILogger? logger = null)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(serializedContext));
            var context = JsonSerializer.Deserialize<JsonElement>(json);

            if (context.TryGetProperty("correlation_id", out var correlationIdElement) &&
                correlationIdElement.ValueKind == JsonValueKind.String)
            {
                var correlationId = correlationIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    result = result.EnsureCorrelationId(correlationId);
                }
            }

            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug("Applied cross-service result context: {Context}", json);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize result context: {SerializedContext}", serializedContext);
        }

        return result;
    }

    #endregion

    #region Correlation Chains

    public static CorrelationChain<T> StartCorrelationChain<T>(
        this Result<T> result,
        string chainName,
        [CallerMemberName] string? memberName = null)
    {
        var correlationId = Activity.Current?.TraceId.ToString()
                           ?? (result.IsFailure ? result.Error.CorrelationId : null)
                           ?? GenerateW3CTraceId();

        return new CorrelationChain<T>(chainName, correlationId, memberName);
    }

    public static Result<T> AddToCorrelationChain<T>(
        this Result<T> result,
        CorrelationChain<T> chain,
        string stepName,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(chain);

        chain.AddStep(stepName, result, memberName);

        // Ensure Result has the chain's correlation ID
        return result.EnsureCorrelationId(chain.CorrelationId);
    }

    #endregion

    #region Baggage Propagation

    /// <summary>
    /// Add Result context to Activity baggage for automatic propagation.
    /// Always includes correlation.id = Activity.TraceId when present.
    /// </summary>
    public static Result<T> AddResultBaggage<T>(
        this Result<T> result,
        string? operationName = null,
        [CallerMemberName] string? memberName = null)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        activity.SetBaggage("operation.name", operationName ?? memberName ?? "unknown");
        activity.SetBaggage("result.type", typeof(T).Name);
        activity.SetBaggage("result.is_success", result.IsSuccess.ToString().ToLowerInvariant());
        activity.SetBaggage("correlation.id", activity.TraceId.ToString());

        if (result.IsFailure)
        {
            var error = result.Error;
            activity.SetBaggage("error.code", error.Code);
            activity.SetBaggage("error.type", error.Type.ToString().ToLowerInvariant());
            activity.SetBaggage("error.severity", error.Severity.ToString().ToLowerInvariant());
        }

        return result;
    }

    /// <summary>
    /// Extract Result context from Activity baggage
    /// </summary>
    public static ResultBaggageContext ExtractResultBaggage()
    {
        var activity = Activity.Current;
        if (activity == null) return new ResultBaggageContext();

        return new ResultBaggageContext
        {
            OperationName = activity.GetBaggageItem("operation.name"),
            ResultType = activity.GetBaggageItem("result.type"),
            IsSuccess = bool.TryParse(activity.GetBaggageItem("result.is_success"), out var success) && success,
            ErrorCode = activity.GetBaggageItem("error.code"),
            ErrorType = activity.GetBaggageItem("error.type"),
            ErrorSeverity = activity.GetBaggageItem("error.severity"),
            CorrelationId = activity.GetBaggageItem("correlation.id")
        };
    }

    #endregion

    #region Helper Methods

    private static string GenerateW3CTraceId()
        => ActivityTraceId.CreateRandom().ToString(); // 32-char hex W3C trace id

    private static string? ExtractCorrelationIdFromHeaders(IHeaderDictionary headers)
    {
        // Prefer W3C traceparent
        if (headers.TryGetValue(TraceParentHeaderName, out var traceParentValues))
        {
            var traceParent = traceParentValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(traceParent))
            {
                var parts = traceParent.Split('-');
                if (parts.Length >= 2 && parts[1].Length == 32) // trace-id is 32 hex chars
                {
                    return parts[1];
                }
            }
        }

        // Fallback: legacy X-Correlation-ID
        if (headers.TryGetValue(CorrelationIdHeaderName, out var correlationValues))
        {
            return correlationValues.FirstOrDefault();
        }

        return null;
    }

    #endregion
}

/// <summary>
/// Trace context for cross-service communication
/// </summary>
public sealed record TraceContext
{
    public required string CorrelationId { get; init; } // equals TraceId
    public string? TraceId { get; init; }               // set equal to CorrelationId
    public string? SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? TraceFlags { get; init; }
    public string? TraceState { get; init; }
    public string? ServiceName { get; init; }
    public string? OperationName { get; init; }
    public string? ResultState { get; init; }
    public string? ErrorCode { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Correlation chain for tracking related operations across services
/// </summary>
public sealed class CorrelationChain<T> : IDisposable
{
    private readonly List<CorrelationStep> _steps = new();
    private readonly object _lock = new();
    private bool _disposed;

    internal CorrelationChain(string chainName, string correlationId, string? initiatorMember)
    {
        ChainName = chainName;
        CorrelationId = correlationId;
        StartTime = DateTimeOffset.UtcNow;
        InitiatorMember = initiatorMember;
    }

    public string ChainName { get; }
    public string CorrelationId { get; }
    public DateTimeOffset StartTime { get; }
    public string? InitiatorMember { get; }
    public IReadOnlyList<CorrelationStep> Steps
    {
        get
        {
            lock (_lock)
            {
                return _steps.ToList();
            }
        }
    }

    internal void AddStep(string stepName, Result<T> result, string? memberName)
    {
        if (_disposed) return;

        lock (_lock)
        {
            _steps.Add(new CorrelationStep
            {
                StepName = stepName,
                MemberName = memberName,
                IsSuccess = result.IsSuccess,
                ErrorCode = result.IsFailure ? result.Error.Code : null,
                Timestamp = DateTimeOffset.UtcNow,
                StepNumber = _steps.Count + 1
            });
        }
    }

    public CorrelationChainSummary GetSummary()
    {
        lock (_lock)
        {
            return new CorrelationChainSummary
            {
                ChainName = ChainName,
                CorrelationId = CorrelationId,
                TotalSteps = _steps.Count,
                SuccessfulSteps = _steps.Count(s => s.IsSuccess),
                FailedSteps = _steps.Count(s => !s.IsSuccess),
                Duration = DateTimeOffset.UtcNow - StartTime,
                IsChainSuccessful = _steps.All(s => s.IsSuccess),
                FirstFailureStep = _steps.FirstOrDefault(s => !s.IsSuccess)?.StepName
            };
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Individual step in a correlation chain
/// </summary>
public sealed record CorrelationStep
{
    public required string StepName { get; init; }
    public string? MemberName { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required int StepNumber { get; init; }
}

/// <summary>
/// Summary of a correlation chain
/// </summary>
public sealed record CorrelationChainSummary
{
    public required string ChainName { get; init; }
    public required string CorrelationId { get; init; }
    public required int TotalSteps { get; init; }
    public required int SuccessfulSteps { get; init; }
    public required int FailedSteps { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool IsChainSuccessful { get; init; }
    public string? FirstFailureStep { get; init; }
}

/// <summary>
/// Result context extracted from Activity baggage
/// </summary>
public sealed record ResultBaggageContext
{
    public string? OperationName { get; init; }
    public string? ResultType { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorSeverity { get; init; }
    public string? CorrelationId { get; init; }
}
