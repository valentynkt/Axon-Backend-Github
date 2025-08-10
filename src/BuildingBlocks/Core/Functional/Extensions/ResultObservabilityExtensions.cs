using System.Diagnostics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Minimal observability helpers for Result&lt;T&gt;.
/// - Correlation ID is the current Activity TraceId (W3C).
/// - Tiny Activity enrichment (status + a few tags).
/// - Lightweight helpers to run ops within an Activity.
/// </summary>
public static class ResultObservabilityExtensions
{
    private static readonly ActivitySource ActivitySource = new("Axon.Results");

    /// <summary>
    /// Ensure failed Result carries a correlation id (prefers Activity.TraceId).
    /// No-ops for successful results.
    /// </summary>
    public static Result<T> EnsureCorrelationId<T>(this Result<T> result, string? correlationId = null)
    {
        if (result.IsSuccess) return result;
        if (!string.IsNullOrWhiteSpace(result.Error.CorrelationId)) return result;

        var id = correlationId
                 ?? Activity.Current?.TraceId.ToString()
                 ?? ActivityTraceId.CreateRandom().ToString();

        return Result<T>.Failure(result.Error.WithCorrelationId(id));
    }

    /// <summary>
    /// Add minimal, low-cardinality tags to the current Activity and set status.
    /// Also ensures correlation id on failures.
    /// </summary>
    public static Result<T> EnrichActivity<T>(
        this Result<T> result,
        string? operationName = null,
        [CallerMemberName] string? memberName = null)
    {
        var activity = Activity.Current;
        if (activity is null) return result.EnsureCorrelationId();

        var op = operationName ?? memberName ?? "operation";

        // Common tags (low cardinality)
        activity.SetTag("operation.name", op);
        activity.SetTag("result.type", typeof(T).Name);
        activity.SetTag("result.is_success", result.IsSuccess);
        activity.SetTag("correlation.id", activity.TraceId.ToString()); // equals trace id

        if (result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Ok, "ok");
        }
        else
        {
            var e = result.Error;
            activity.SetStatus(ActivityStatusCode.Error, e.Message);
            activity.SetTag("error.code", e.Code);
            activity.SetTag("error.type", e.Type.ToString());
            result = result.EnsureCorrelationId(activity.TraceId.ToString());
        }

        return result;
    }

    /// <summary>
    /// Execute a synchronous Result operation within an Activity scope.
    /// </summary>
    public static Result<T> WithActivity<T>(
        this Func<Result<T>> operation,
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = ActivitySource.StartActivity(operationName, kind, default(ActivityContext), tags);

        try
        {
            var result = operation();
            return result.EnrichActivity(operationName);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message
            }));
            throw;
        }
    }

    /// <summary>
    /// Execute an async Result operation within an Activity scope.
    /// </summary>
    public static async Task<Result<T>> WithActivityAsync<T>(
        this Task<Result<T>> operation,
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        using var activity = ActivitySource.StartActivity(operationName, kind, default(ActivityContext), tags);

        try
        {
            var result = await operation.ConfigureAwait(false);
            return result.EnrichActivity(operationName);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message
            }));
            throw;
        }
    }

    /// <summary>
    /// Start an Activity and complete it later by calling Complete(result).
    /// </summary>
    public static ResultActivityScope<T> StartActivity<T>(
        string operationName,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        var activity = ActivitySource.StartActivity(operationName, kind, default(ActivityContext), tags);
        return new ResultActivityScope<T>(activity, operationName);
    }
}

/// <summary>
/// Disposable Activity scope for Result operations with minimal enrichment.
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
        return result.EnrichActivity(_operationName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _activity?.Dispose();
        _disposed = true;
    }
}
