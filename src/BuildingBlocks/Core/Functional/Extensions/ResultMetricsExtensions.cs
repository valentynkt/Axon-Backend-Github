using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Minimal metrics for Result&lt;T&gt;:
/// - operations (total/success/failure)
/// - duration_ms
/// - active operations (up/down)
/// Low-cardinality tags only: operation.name, result.type.
/// </summary>
public static class ResultMetricsExtensions
{
    private static readonly Meter Meter = new("Axon.Results", "2.0");

    internal static readonly Counter<long> OpsTotal   = Meter.CreateCounter<long>("axon.result.operations");
    internal static readonly Counter<long> OpsSuccess = Meter.CreateCounter<long>("axon.result.operations.success");
    internal static readonly Counter<long> OpsFailure = Meter.CreateCounter<long>("axon.result.operations.failure");
    internal static readonly UpDownCounter<long> OpsActive = Meter.CreateUpDownCounter<long>("axon.result.operations.active");
    internal static readonly Histogram<double> DurationMs = Meter.CreateHistogram<double>("axon.result.duration_ms", unit: "ms");

    private static KeyValuePair<string, object?>[] Tags(string operationName, Type resultType) => new[]
    {
        new KeyValuePair<string, object?>("operation.name", operationName),
        new KeyValuePair<string, object?>("result.type", resultType.Name)
    };

    /// <summary>
    /// Record counters + duration using an existing stopwatch.
    /// </summary>
    public static Result<T> RecordMetrics<T>(
        this Result<T> result,
        string operationName,
        Stopwatch stopwatch,
        [CallerMemberName] string? _ = null)
    {
        var tags = Tags(operationName, typeof(T));
        OpsTotal.Add(1, tags);
        if (result.IsSuccess) OpsSuccess.Add(1, tags); else OpsFailure.Add(1, tags);
        DurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        return result;
    }

    /// <summary>
    /// Start a metrics scope and complete it later with Complete(result).
    /// </summary>
    public static ResultMetricsScope<T> StartMetrics<T>(string operationName)
        => new(operationName, typeof(T));
}

/// <summary>
/// Disposable metrics scope: increments active on enter, decrements on complete/dispose,
/// and records total/success/failure + duration_ms.
/// </summary>
public sealed class ResultMetricsScope<T> : IDisposable
{
    private readonly string _operationName;
    private readonly Type _resultType;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly KeyValuePair<string, object?>[] _tags;
    private bool _completed;
    private bool _disposed;

    internal ResultMetricsScope(string operationName, Type resultType)
    {
        _operationName = operationName;
        _resultType = resultType;
        _tags = new[]
        {
            new KeyValuePair<string, object?>("operation.name", _operationName),
            new KeyValuePair<string, object?>("result.type", _resultType.Name)
        };

        // Increment active operations
        ResultMetricsExtensions.OpsActive.Add(1, _tags);
    }

    public Result<T> Complete(Result<T> result)
    {
        if (_completed) return result;

        // Record counters
        ResultMetricsExtensions.OpsTotal.Add(1, _tags);

        if (result.IsSuccess)
        {
            ResultMetricsExtensions.OpsSuccess.Add(1, _tags);
        }
        else
        {
            ResultMetricsExtensions.OpsFailure.Add(1, _tags);
        }

        // Record duration
        ResultMetricsExtensions.DurationMs.Record(_sw.Elapsed.TotalMilliseconds, _tags);

        _completed = true;
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sw.Stop();

        // Decrement active operations
        ResultMetricsExtensions.OpsActive.Add(-1, _tags);

        _disposed = true;
    }
}
