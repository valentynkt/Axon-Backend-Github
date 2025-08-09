using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// OpenTelemetry Activity and telemetry integration extensions for Result&lt;T&gt;.
/// W3C mode: correlation.id is always the Activity TraceId.
/// </summary>
public static class ResultTelemetryExtensions
{
    private static readonly ActivitySource ActivitySource = new(TelemetryTags.Tracing.Application.AppService);

    #region Activity Integration Extensions

    /// <summary>
    /// Enrich current Activity with Result outcome and metadata.
    /// Ensures failures carry correlation id equal to the Activity TraceId.
    /// </summary>
    public static Result<T> EnrichActivity<T>(
        this Result<T> result,
        string? operationName = null,
        [CallerMemberName] string? memberName = null)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        var opName = operationName ?? memberName ?? "UnknownOperation";

        // Make sure failures have correlation id set to the current trace id
        if (result.IsFailure && string.IsNullOrWhiteSpace(result.Error.CorrelationId))
        {
            var traceId = activity.TraceId.ToString();
            result = Result<T>.Failure(result.Error.WithCorrelationId(traceId));
        }

        // Set operation name if not already set
        if (string.IsNullOrEmpty(activity.DisplayName))
        {
            activity.SetTag("operation.name", opName);
            activity.DisplayName = opName;
        }

        // Add W3C correlation + result tags
        activity.SetTag("correlation.id", activity.TraceId.ToString()); // equals trace id
        activity.SetTag("trace.id", activity.TraceId.ToString());
        activity.SetTag("result.type", typeof(T).Name);
        activity.SetTag("result.is_success", result.IsSuccess);
        activity.SetTag("result.is_failure", result.IsFailure);

        if (result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Ok, "Operation completed successfully");
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, "OK");
        }
        else
        {
            var error = result.Error;
            activity.SetStatus(ActivityStatusCode.Error, error.Message);
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, "ERROR");
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusDescription, error.Message);

            // Add error details
            activity.SetTag("error.code", error.Code);
            activity.SetTag("error.type", error.Type.ToString());
            activity.SetTag("error.severity", error.Severity.ToString());
            activity.SetTag("http.status_code", error.ToHttpStatusCode());

            if (!string.IsNullOrEmpty(error.Source))
            {
                activity.SetTag("error.source", error.Source);
            }

            // Add error metadata as tags (with size limits)
            if (error.Metadata != null)
            {
                foreach (var (key, value) in error.Metadata.Take(10)) // Limit to 10 metadata items
                {
                    var valueStr = value?.ToString();
                    if (!string.IsNullOrEmpty(valueStr) && valueStr.Length <= 256) // Limit tag value length
                    {
                        activity.SetTag($"error.metadata.{key.ToLowerInvariant()}", valueStr);
                    }
                }
            }

            // Add exception event if inner exception exists
            if (error.InnerException != null)
            {
                activity.AddEvent(new ActivityEvent(TelemetryTags.Tracing.Exception.EventName, DateTimeOffset.UtcNow, new ActivityTagsCollection
                {
                    [TelemetryTags.Tracing.Exception.Type] = error.InnerException.GetType().FullName,
                    [TelemetryTags.Tracing.Exception.Message] = error.InnerException.Message,
                    [TelemetryTags.Tracing.Exception.Stacktrace] = error.InnerException.StackTrace
                }));
            }
        }

        return result;
    }

    /// <summary>
    /// Create a child Activity for a Result operation with automatic completion
    /// </summary>
    public static ResultActivityScope<T> StartActivity<T>(
        this ActivitySource activitySource,
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var activity = activitySource.StartActivity(operationName, kind, parentContext, tags);
        return new ResultActivityScope<T>(activity, operationName);
    }

    /// <summary>
    /// Execute operation within an Activity scope with automatic Result enrichment
    /// </summary>
    public static async Task<Result<T>> WithActivityAsync<T>(
        this Task<Result<T>> operation,
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = ActivitySource.StartActivity(operationName, kind, tags: tags);

        try
        {
            var result = await operation.ConfigureAwait(false);
            return result.EnrichActivity(operationName);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent(TelemetryTags.Tracing.Exception.EventName, DateTimeOffset.UtcNow, new ActivityTagsCollection
            {
                [TelemetryTags.Tracing.Exception.Type] = ex.GetType().FullName,
                [TelemetryTags.Tracing.Exception.Message] = ex.Message,
                [TelemetryTags.Tracing.Exception.Stacktrace] = ex.StackTrace
            }));
            throw;
        }
    }

    /// <summary>
    /// Execute synchronous operation within an Activity scope
    /// </summary>
    public static Result<T> WithActivity<T>(
        this Func<Result<T>> operation,
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = ActivitySource.StartActivity(operationName, kind, tags: tags);

        try
        {
            var result = operation();
            return result.EnrichActivity(operationName);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent(TelemetryTags.Tracing.Exception.EventName, DateTimeOffset.UtcNow, new ActivityTagsCollection
            {
                [TelemetryTags.Tracing.Exception.Type] = ex.GetType().FullName,
                [TelemetryTags.Tracing.Exception.Message] = ex.Message,
                [TelemetryTags.Tracing.Exception.Stacktrace] = ex.StackTrace
            }));
            throw;
        }
    }

    #endregion

    #region Baggage and Context Extensions

    /// <summary>
    /// Add Result context to Activity baggage for cross-service correlation.
    /// Always sets correlation.id to Activity.TraceId.
    /// </summary>
    public static Result<T> AddToBaggage<T>(
        this Result<T> result,
        string? operationName = null,
        [CallerMemberName] string? memberName = null)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        var opName = operationName ?? memberName ?? "UnknownOperation";

        activity.SetBaggage("operation.name", opName);
        activity.SetBaggage("operation.result", result.IsSuccess ? "success" : "failure");
        activity.SetBaggage("correlation.id", activity.TraceId.ToString());

        if (result.IsFailure)
        {
            var error = result.Error;
            activity.SetBaggage("error.code", error.Code);
            activity.SetBaggage("error.type", error.Type.ToString());
        }

        return result;
    }

    /// <summary>
    /// Create correlation context from Result for downstream operations
    /// </summary>
    public static ActivityContext CreateCorrelationContext<T>(this Result<T> result)
    {
        var activity = Activity.Current;
        if (activity == null) return default;

        var context = activity.Context;

        // Add Result-specific trace state
        var traceState = context.TraceState ?? string.Empty;
        var resultState = result.IsSuccess ? "success" : "failure";

        if (!traceState.Contains("result="))
        {
            traceState = string.IsNullOrEmpty(traceState)
                ? $"result={resultState}"
                : $"{traceState},result={resultState}";
        }

        return new ActivityContext(
            context.TraceId,
            context.SpanId,
            context.TraceFlags,
            traceState);
    }

    #endregion

    #region Custom Event Extensions

    public static Result<T> AddEvent<T>(
        this Result<T> result,
        string eventName,
        object? eventData = null,
        DateTimeOffset? timestamp = null)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        var tags = new ActivityTagsCollection
        {
            ["correlation.id"] = activity.TraceId.ToString(),
            ["result.is_success"] = result.IsSuccess,
            ["result.type"] = typeof(T).Name
        };

        if (result.IsFailure)
        {
            var error = result.Error;
            tags.Add("error.code", error.Code);
            tags.Add("error.message", error.Message);
            tags.Add("error.type", error.Type.ToString());
        }

        if (eventData != null)
        {
            var properties = eventData.GetType().GetProperties();
            foreach (var prop in properties.Take(10))
            {
                try
                {
                    var value = prop.GetValue(eventData);
                    if (value != null)
                    {
                        tags.Add($"event.{prop.Name.ToLowerInvariant()}", value.ToString());
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch { }
#pragma warning restore CA1031
            }
        }

        activity.AddEvent(new ActivityEvent(eventName, timestamp ?? DateTimeOffset.UtcNow, tags));
        return result;
    }

    public static Result<T> AddBusinessEvent<T>(
        this Result<T> result,
        string businessEventName,
        object? businessData = null)
    {
        if (result.IsSuccess)
        {
            result.AddEvent($"business.{businessEventName}", businessData);
        }

        return result;
    }

    public static Result<T> AddErrorEvent<T>(
        this Result<T> result,
        string errorEventName = "operation.error",
        object? errorData = null)
    {
        if (result.IsFailure)
        {
            result.AddEvent(errorEventName, errorData);
        }

        return result;
    }

    #endregion

    #region Activity Link Extensions

    public static Result<T> LinkToCorrelatedOperations<T>(
        this Result<T> result,
        IEnumerable<ActivityContext> relatedContexts)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        foreach (var context in relatedContexts)
        {
            var linkTags = new ActivityTagsCollection
            {
                ["link.type"] = "correlation",
                ["result.is_success"] = result.IsSuccess,
                ["correlation.id"] = activity.TraceId.ToString()
            };

            if (result.IsFailure)
            {
                linkTags.Add("error.code", result.Error.Code);
            }

            // Note: Links must be added when starting an Activity; here for parity/documentation.
        }

        return result;
    }

    #endregion

    #region Sampling and Performance

    public static Result<T> ApplyConditionalSampling<T>(
        this Result<T> result,
        double successSampleRate = 0.1,
        double failureSampleRate = 1.0)
    {
        var activity = Activity.Current;
        if (activity == null) return result;

        var shouldSample = result.IsSuccess
            ? Random.Shared.NextDouble() < successSampleRate
            : Random.Shared.NextDouble() < failureSampleRate;

        if (!shouldSample)
        {
            activity.IsAllDataRequested = false;
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
        else
        {
            activity.SetTag("sampling.applied", true);
            activity.SetTag("sampling.rate", result.IsSuccess ? successSampleRate : failureSampleRate);
        }

        return result;
    }

    #endregion
}

/// <summary>
/// Disposable Activity scope for Result operations with automatic completion
/// </summary>
public sealed class ResultActivityScope<T> : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _operationName;
    private bool _disposed;

    internal ResultActivityScope(Activity? activity, string operationName)
    {
        _activity = activity;
        _operationName = operationName;
    }

    public Result<T> Complete(Result<T> result)
    {
        if (_disposed) return result;

        result.EnrichActivity(_operationName);
        return result;
    }

    public Activity? Activity => _activity;

    public void Dispose()
    {
        if (_disposed) return;

        if (_activity != null && _activity.IsAllDataRequested)
        {
            _activity.SetTag("operation.completed", false);
            _activity.SetStatus(ActivityStatusCode.Error, "Operation was not completed properly");
        }

        _activity?.Dispose();
        _disposed = true;
    }
}
