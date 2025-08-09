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
                activity.SetTag("error.type", e.Type.ToString());
            }

            activity.SetEndTime(startTime.Add(duration).UtcDateTime);
        }

        var dims = new KeyValuePair<string, object?>[]
        {
            new("request.name", requestName),
            new("result.type", typeof(T).Name)
        };
        ResultOperations.Add(1, dims);
        if (result.IsSuccess) ResultSuccesses.Add(1, dims);
        else ResultFailures.Add(1, dims);
        ResultDurationMs.Record(duration.TotalMilliseconds, dims);

        return result;
    }

    #endregion

    #region Business events & traces

    /// <summary>
    /// Add a business event to the current span. Numeric properties are recorded as metrics.
    /// </summary>
    public static Result<T> TrackBusinessEvent<T>(
        this Result<T> result,
        string eventName,
        object? eventData = null)
    {
        var activity = Activity.Current;
        if (activity is null || !result.IsSuccess)
            return result;

        var tags = new ActivityTagsCollection
        {
            ["event.type"] = "business",
            ["result.type"] = typeof(T).Name
        };

        if (eventData != null)
        {
            foreach (var p in eventData.GetType().GetProperties().Take(20))
            {
                try
                {
                    var value = p.GetValue(eventData);
                    if (value == null) continue;

                    if (IsNumeric(p.PropertyType))
                    {
                        // metric per property
                        Meter.CreateHistogram<double>($"axon.business.{eventName}.{p.Name}", unit: "value")
                             .Record(Convert.ToDouble(value));
                    }
                    else
                    {
                        tags[$"event.{p.Name}"] = value.ToString();
                    }
                }
                catch
                {
                    // ignore reflection problems
                }
            }
        }

        activity.AddEvent(new ActivityEvent($"business.{eventName}", DateTimeOffset.UtcNow, tags));
        return result;
    }

    /// <summary>
    /// Adds a lightweight log/trace event to the current span using OTel log semantic attrs.
    /// </summary>
    public static Result<T> TrackTrace<T>(
        this Result<T> result,
        string message,
        string severity = "INFO",
        IEnumerable<KeyValuePair<string, object?>>? attributes = null)
    {
        var activity = Activity.Current;
        if (activity is null) return result;

        var tags = new ActivityTagsCollection
        {
            ["log.severity"] = severity,
            ["log.message"] = message,
            ["result.is_success"] = result.IsSuccess,
            ["result.type"] = typeof(T).Name
        };

        if (!result.IsSuccess)
        {
            var e = result.Error;
            tags["error.code"] = e.Code;
            tags["error.message"] = e.Message;
        }

        if (attributes != null)
        {
            foreach (var (k, v) in attributes)
                tags[k] = v;
        }

        activity.AddEvent(new ActivityEvent("log", DateTimeOffset.UtcNow, tags));
        return result;
    }

    #endregion

    #region Batch

    /// <summary>
    /// Emit a single aggregated event + metrics for a batch of Result items.
    /// </summary>
    public static IEnumerable<Result<T>> TrackBatchResults<T>(
        this IEnumerable<Result<T>> results,
        string batchName,
        TimeSpan? totalDuration = null)
    {
        var list = results.ToList();
        var ok = list.Count(x => x.IsSuccess);
        var fail = list.Count - ok;
        var rate = list.Count == 0 ? 0 : (double)ok / list.Count;

        var activity = Activity.Current;
        if (activity is not null)
        {
            var tags = new ActivityTagsCollection
            {
                ["batch.name"] = batchName,
                ["result.type"] = typeof(T).Name,
                ["batch.total"] = list.Count,
                ["batch.success"] = ok,
                ["batch.failure"] = fail,
                ["batch.success_rate"] = rate
            };
            if (totalDuration.HasValue)
            {
                tags["batch.duration.ms"] = totalDuration.Value.TotalMilliseconds;
                tags["batch.avg_item.ms"] =
                    list.Count == 0 ? 0 : totalDuration.Value.TotalMilliseconds / list.Count;
            }

            activity.AddEvent(new ActivityEvent("result.batch", DateTimeOffset.UtcNow, tags));
        }

        // Metrics
        var dims = new KeyValuePair<string, object?>[]
        {
            new("batch.name", batchName),
            new("result.type", typeof(T).Name)
        };
        ResultOperations.Add(list.Count, dims);
        ResultSuccesses.Add(ok, dims);
        ResultFailures.Add(fail, dims);
        if (totalDuration.HasValue)
            ResultDurationMs.Record(totalDuration.Value.TotalMilliseconds, dims);

        return list;
    }

    #endregion

    private static bool IsNumeric(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) ||
               t == typeof(decimal);
    }
}

/// <summary>
/// Disposable scope that creates a client span for a dependency and completes it with a <see cref="Result{T}"/>.
/// </summary>
public sealed class ResultDependencyScope<T> : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _spanName;
    private bool _completed;

    internal ResultDependencyScope(Activity? activity, string spanName)
    {
        _activity = activity;
        _spanName = spanName;
    }

    /// <summary>Stop the span and record the outcome.</summary>
    public Result<T> Complete(Result<T> result)
    {
        if (_completed) return result;

        if (_activity is not null)
        {
            _activity.SetTag("result.type", typeof(T).Name);
            if (result.IsSuccess)
            {
                _activity.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                var e = result.Error;
                _activity.SetStatus(ActivityStatusCode.Error, e.Message);
                _activity.SetTag("error.code", e.Code);
                _activity.SetTag("error.type", e.Type.ToString());
                _activity.SetTag("http.status_code", e.ToHttpStatusCode());
            }

            _activity.Dispose(); // stops the span
        }

        _completed = true;
        return result;
    }

    public void Dispose()
    {
        if (_completed) return;

        // If not completed explicitly, mark as error to surface incomplete dependency
        if (_activity is not null)
        {
            _activity.SetStatus(ActivityStatusCode.Error, "Dependency operation not completed");
            _activity.AddEvent(new ActivityEvent(
                "dependency.incomplete",
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection { ["span.name"] = _spanName }));
            _activity.Dispose();
        }

        _completed = true;
    }
}
