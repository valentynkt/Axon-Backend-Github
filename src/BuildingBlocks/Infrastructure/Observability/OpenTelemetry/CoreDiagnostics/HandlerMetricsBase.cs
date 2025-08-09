using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

/// <summary>
/// Base class for CQRS handler metrics, providing common telemetry functionality.
/// Follows SOLID principles and eliminates code duplication.
/// </summary>
public abstract class HandlerMetricsBase
{
    private readonly UpDownCounter<long> _activeCounter;
    private readonly Counter<long> _totalCounter;
    private readonly Counter<long> _successCounter;
    private readonly Counter<long> _failedCounter;
    private readonly Histogram<double> _handlerDuration;

    private Stopwatch _timer = new();

    protected HandlerMetricsBase(
        IDiagnosticsProvider diagnosticsProvider,
        HandlerMetricsConfiguration configuration)
    {
        _activeCounter = diagnosticsProvider.Meter.CreateUpDownCounter<long>(
            configuration.ActiveCountMetric,
            unit: configuration.ActiveCountUnit,
            description: configuration.ActiveCountDescription
        );

        _totalCounter = diagnosticsProvider.Meter.CreateCounter<long>(
            configuration.TotalCountMetric,
            unit: configuration.TotalCountUnit,
            description: configuration.TotalCountDescription
        );

        _successCounter = diagnosticsProvider.Meter.CreateCounter<long>(
            configuration.SuccessCountMetric,
            unit: configuration.SuccessCountUnit,
            description: configuration.SuccessCountDescription
        );

        _failedCounter = diagnosticsProvider.Meter.CreateCounter<long>(
            configuration.FailedCountMetric,
            unit: configuration.FailedCountUnit,
            description: configuration.FailedCountDescription
        );

        _handlerDuration = diagnosticsProvider.Meter.CreateHistogram<double>(
            configuration.DurationMetric,
            unit: configuration.DurationUnit,
            description: configuration.DurationDescription
        );
    }

    /// <summary>
    /// Starts tracking execution metrics for the given tags.
    /// </summary>
    protected void StartExecution(TagList tags)
    {
        if (_activeCounter.Enabled)
        {
            _activeCounter.Add(1, tags);
        }

        if (_totalCounter.Enabled)
        {
            _totalCounter.Add(1, tags);
        }

        _timer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Finishes tracking execution metrics and records duration.
    /// </summary>
    protected void FinishExecution(TagList tags)
    {
        if (_activeCounter.Enabled)
        {
            _activeCounter.Add(-1, tags);
        }

        if (_handlerDuration.Enabled)
        {
            var elapsedTimeSeconds = _timer.Elapsed.TotalSeconds;
            _handlerDuration.Record(elapsedTimeSeconds, tags);
        }

        if (_successCounter.Enabled)
        {
            _successCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Records a failure metric.
    /// </summary>
    protected void RecordFailure(TagList tags)
    {
        if (_failedCounter.Enabled)
        {
            _failedCounter.Add(1, tags);
        }
    }

    /// <summary>
    /// Converts a dictionary of tags to a TagList.
    /// </summary>
    protected static TagList CreateTagList(Dictionary<string, object?> tags)
    {
        var tagList = new TagList();
        foreach (var (key, value) in tags)
        {
            tagList.Add(key, value);
        }
        return tagList;
    }
}