using System.Diagnostics.Metrics;

namespace BuildingBlocks.Core.Diagnostics.Performance;

public sealed class ErrorTelemetry : IErrorTelemetry
{
    private static readonly Meter Meter = new("Axon.Errors", "2.0");
    private static readonly Counter<long> ErrorsCreated = Meter.CreateCounter<long>("errors.created");
    private static readonly UpDownCounter<long> ErrorsActive = Meter.CreateUpDownCounter<long>("errors.active");
    private static readonly Histogram<double> HandleMs = Meter.CreateHistogram<double>("errors.handle_ms");

    public void Created(string code, string type, string severity)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", code),
            new("error.type", type),
            new("error.severity", severity)
        };
        ErrorsCreated.Add(1, tags);
        ErrorsActive.Add(1, tags);
    }

    public void Resolved(string code)
    {
        ErrorsActive.Add(-1, new KeyValuePair<string, object?>[] { new("error.code", code) });
    }

    public void HandleDuration(TimeSpan duration, string code, string? stage = null)
    {
        HandleMs.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>[]
            {
                new("error.code", code),
                new("stage", stage ?? "unknown")
            });
    }
}