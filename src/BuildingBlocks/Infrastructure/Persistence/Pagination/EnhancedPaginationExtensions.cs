// File: ResultOpenTelemetryExtensions.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// OpenTelemetry-first extensions for recording <see cref="Result{T}"/> outcomes
/// using W3C trace context and OTel semantic conventions (no Application Insights SDK).
/// </summary>
public static class ResultOpenTelemetryExtensions
{
    // Spans
    private static readonly ActivitySource ActivitySource =
        new(TelemetryTags.Tracing.Application.AppService);

    // Metrics
    private static readonly Meter Meter = new("Axon.Results", "1.0.0");

    private static readonly Counter<long> ResultOperations =
        Meter.CreateCounter<long>("axon.result.operations", unit: "operations",
            description: "Total number of Result operations");

    private static readonly Counter<long> ResultSuccesses =
        Meter.CreateCounter<long>("axon.result.success", unit: "operations",
            description: "Successful Result operations");

    private static readonly Counter<long> ResultFailures =
        Meter.CreateCounter<long>("axon.result.failure", unit: "operations",
            description: "Failed Result operations");

    private static readonly Histogram<double> ResultDurationMs =
        Meter.CreateHistogram<double>("axon.result.duration", unit: "ms",
            description: "Duration of Result operations in milliseconds");

    #region Result operation tracking

    /// <summary>
    /// Record a Result outcome on the current span and emit OTel metrics.
    /// Does not create a new span; instead enriches the current one and adds an event.
    /// </summary>
    public static Result<T> TrackResultOperation<T>(
        this Result<T> result,
        string operationName,
        TimeSpan? duration = null,
        IEnumerable<KeyValuePair<string, object?>>? attributes = null,
        [CallerMemberName] string? memberName = null)
    {
        var activity = Activity.Current;

        // ---- Span enrichment (W3C/OTel) ----
        if (activity is not null)
        {
            // Ensure a display name (helpful for exporters)
            if (string.IsNullOrWhiteSpace(activity.DisplayName))
                activity.DisplayName = operationName;

            activity.SetTag("operation.name", operationName);
            activity.SetTag("code.function", memberName ?? "unknown");
            activity.SetTag("result.type", typeof(T).Name);
            activity.SetTag("result.is_success", result.IsSuccess);

            if (attributes != null)
            {
                foreach (var (k, v) in attributes)
                    activity.SetTag(k, v);
            }

            if (result.IsSuccess)
            {
                activity.SetStatus(ActivityStatusCode.Ok, "Result success");
            }
            else
            {
                var e = result.Error;
                activity.SetStatus(ActivityStatusCode.Error, e.Message);
                activity.SetTag("error.code", e.Code);
                activity.SetTag("error.type", e.Type.ToString());
                activity.SetTag("error.severity", e.Severity.ToString());
                activity.SetTag("http.status_code", e.ToHttpStatusCode());

                if (!string.IsNullOrEmpty(e.CorrelationId))
                    activity.SetTag("correlation.id", e.CorrelationId);
                if (!string.IsNullOrEmpty(e.Source))
                    activity.SetTag("error.source", e.Source);

                // Add exception event if present (OTel exception semantic conv)
                if (e.InnerException is not null)
                {
                    activity.AddEvent(new ActivityEvent(
                        TelemetryTags.Tracing.Exception.EventName,
                        DateTimeOffset.UtcNow,
                        new ActivityTagsCollection
                        {
                            [TelemetryTags.Tracing.Exception.Type] = e.InnerException.GetType().FullName,
                            [TelemetryTags.Tracing.Exception.Message] = e.InnerException.Message,
                            [TelemetryTags.Tracing.Exception.Stacktrace] = e.InnerException.StackTrace ?? string.Empty
                        }));
                }
            }

            // Add a compact structured event for the operation
            var evtTags = new ActivityTagsCollection
            {
                ["operation"] = operationName,
                ["result.is_success"] = result.IsSuccess,
                ["result.type"] = typeof(T).Name
            };
            if (duration.HasValue) evtTags.Add("duration.ms", duration.Value.TotalMilliseconds);
            activity.AddEvent(new ActivityEvent("result.operation", DateTimeOffset.UtcNow, evtTags));
        }

        // ---- Metrics (OTel) ----
        var dims = new KeyValuePair<string, object?>[]
        {
            new("operation.name", operationName),
            new("result.type", typeof(T).Name),
            new("code.function", memberName ?? "unknown")
        };

        ResultOperations.Add(1, dims);
        if (result.IsSuccess) ResultSuccesses.Add(1, dims);
        else ResultFailures.Add(1, dims);

        if (duration.HasValue)
            ResultDurationMs.Record(duration.Value.TotalMilliseconds, dims);

        return result;
    }

    #endregion

    #region Dependency tracking

    /// <summary>
    /// Start a client/dependency span using W3C/OTel conventions.
    /// Call <see cref="ResultDependencyScope{T}.Complete"/> with the final <see cref="Result{T}"/>.
    /// </summary>
    public static ResultDependencyScope<T> StartDependencyTracking<T>(
        string system,          // e.g. "http", "db", "redis", "mq"
        string name,            // peer/service name or DB name
        string command)         // command/statement/operation
    {
        var spanName = $"dep:{system}:{name}";
        var activity = ActivitySource.StartActivity(spanName, ActivityKind.Client);

        if (activity is not null)
        {
            activity.SetTag("dependency.system", system);
            activity.SetTag("dependency.name", name);
            activity.SetTag("dependency.command", command);

            // Map to common semantic attributes when possible
            if (system.Equals("db", StringComparison.OrdinalIgnoreCase))
            {
                activity.SetTag("db.system", name);
                activity.SetTag("db.statement", command);
            }
            else if (system.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                // When used for HTTP, pass the HTTP attributes as part of 'command' or attributes
                activity.SetTag("http.target", command);
            }
        }

        return new ResultDependencyScope<T>(activity, spanName);
    }

    /// <summary>
    /// Record a dependency outcome without managing a scope (manual mode).
    /// </summary>
    public static Result<T> TrackResultDependency<T>(
        this Result<T> result,
        string system,
        string name,
        string command,
        DateTimeOffset startTime,
        TimeSpan duration,
        IEnumerable<KeyValuePair<string, object?>>? attributes = null)
    {
        using var activity = ActivitySource.StartActivity($"dep:{system}:{name}", ActivityKind.Client);
        if (activity is not null)
        {
            activity.SetStartTime(startTime.UtcDateTime);

            activity.SetTag("dependency.system", system);
            activity.SetTag("dependency.name", name);
            activity.SetTag("dependency.command", command);
            activity.SetTag("result.type", typeof(T).Name);

            if (attributes != null)
            {
                foreach (var (k, v) in attributes)
                    activity.SetTag(k, v);
            }

            if (result.IsSuccess)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                var e = result.Error;
                activity.SetStatus(ActivityStatusCode.Error, e.Message);
                activity.SetTag("error.code", e.Code);
                activity.SetTag("error.type", e.Type.ToString());
                activity.SetTag("http.status_code", e.ToHttpStatusCode());
            }

            activity.SetEndTime(startTime.Add(duration).UtcDateTime);
        }

        // Also emit simple counters
        var dims = new KeyValuePair<string, object?>[]
        {
            new("dependency.system", system),
            new("dependency.name", name),
            new("result.type", typeof(T).Name),
        };
        ResultOperations.Add(1, dims);
        if (result.IsSuccess) ResultSuccesses.Add(1, dims);
        else ResultFailures.Add(1, dims);
        ResultDurationMs.Record(duration.TotalMilliseconds, dims);

        return result;
    }

    #endregion

    #region Request tracking

    /// <summary>
    /// Create a server/request span for a completed operation (useful in non-HTTP hosts).
    /// </summary>
    public static Result<T> TrackResultRequest<T>(
        this Result<T> result,
        string requestName,
        DateTimeOffset startTime,
        TimeSpan duration,
        string? responseCode = null)
    {
        using var activity = ActivitySource.StartActivity(requestName, ActivityKind.Server);
        if (activity is not null)
        {
            activity.SetStartTime(startTime.UtcDateTime);
            activity.SetTag("result.type", typeof(T).Name);
            activity.SetTag("http.status_code", result.IsSuccess
                ? responseCode ?? "200"
                : (responseCode ?? result.Error.ToHttpStatusCode().ToString()));

            if (result.IsSuccess)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                var e = result.Error;
                activity.SetStatus(ActivityStatusCode.Error, e.Message);
                activity.SetTag("error.code", e.Code);
                activity.SetTag("error.type", e.Type.ToStri
