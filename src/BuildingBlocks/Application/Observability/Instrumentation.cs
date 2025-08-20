using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Application.Observability;

/// <summary>
/// Static holder for single, process-wide ActivitySource and Meter instances.
/// Prevents multiple instrumentation instances from being created by generic behaviors.
/// </summary>
public static class Instrumentation
{
    /// <summary>Single ActivitySource for the entire application pipeline</summary>
    public static readonly ActivitySource ActivitySource = new("Axon.Application");

    /// <summary>Single Meter for the entire application pipeline</summary> 
    public static readonly Meter Meter = new("Axon.Application");

    /// <summary>Counter for total requests</summary>
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("axon.requests", description: "Total requests");

    /// <summary>Counter for failed requests</summary>
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("axon.requests.failures", description: "Failed requests");

    /// <summary>Counter for cancelled requests</summary>
    public static readonly Counter<long> Cancelled = Meter.CreateCounter<long>("axon.requests.cancelled", description: "Cancelled requests");

    /// <summary>Histogram for request duration</summary>
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("axon.request.duration", unit: "ms", description: "Request duration (ms)");
}