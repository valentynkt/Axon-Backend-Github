using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Application.Validation;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Lightweight OpenTelemetry metrics for validation. Optional at DI time.
/// </summary>
public sealed class ValidationMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestsCounter;
    private readonly Histogram<double> _durationHistogram;
    private readonly Counter<long> _errorsCounter;

    public ValidationMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Axon.Validation");

        _requestsCounter = _meter.CreateCounter<long>(
            "axon.validation.requests.total",
            description: "Total number of validation requests");

        _durationHistogram = _meter.CreateHistogram<double>(
            "axon.validation.duration.ms",
            unit: "ms",
            description: "Validation duration in milliseconds");

        _errorsCounter = _meter.CreateCounter<long>(
            "axon.validation.errors.total",
            description: "Total validation errors by type");
    }

    public void RecordValidation(
        string requestType,
        bool isValid,
        int validatorCount,
        int errorCount,
        long durationMs,
        ValidationResult? result = null)
    {
        var tags = new TagList
        {
            { "request.type", requestType },
            { "outcome", isValid ? "success" : "failure" },
            { "validator.count", validatorCount }
        };

        _requestsCounter.Add(1, tags);
        _durationHistogram.Record(durationMs, tags);

        if (!isValid && result is not null)
            RecordErrors(requestType, result.Errors);

        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("axon.validation.outcome", isValid ? "success" : "failure");
            activity.SetTag("axon.validation.error_count", errorCount);
            activity.SetTag("axon.validation.duration_ms", durationMs);

            if (!isValid && result is not null)
            {
                var topErrors = result.Errors.Take(3).ToList();
                for (var i = 0; i < topErrors.Count; i++)
                {
                    activity.SetTag($"axon.validation.error[{i}].code", topErrors[i].Code);
                    activity.SetTag($"axon.validation.error[{i}].field", topErrors[i].Field ?? "_global");
                }
            }
        }
    }

    private void RecordErrors(string requestType, IReadOnlyList<ValidationError> errors)
    {
        var groups = errors
            .GroupBy(e => new { Field = e.Field ?? "_global", e.Code })
            .Take(10); // cap to avoid high cardinality

        foreach (var g in groups)
        {
            var tags = new TagList
            {
                { "request.type", requestType },
                { "field", g.Key.Field },
                { "code", g.Key.Code }
            };

            _errorsCounter.Add(g.Count(), tags);
        }
    }
}
