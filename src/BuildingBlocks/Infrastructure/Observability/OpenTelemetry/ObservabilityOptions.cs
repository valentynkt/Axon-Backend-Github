namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

/// <summary>
/// Minimal options controlling Azure Monitor (Application Insights) export and basic knobs.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary> Optional override; defaults to ServiceName. Drives ActivitySource/Meter names. </summary>
    public string? InstrumentationName { get; init; }

    /// <summary> Service name reported to Azure Monitor; default is entry assembly name. </summary>
    public string? ServiceName { get; init; }

    /// <summary> Enable/disable telemetry types. </summary>
    public bool TracingEnabled { get; init; } = true;
    public bool MetricsEnabled { get; init; } = true;
    public bool LoggingEnabled { get; init; } = true;

    /// <summary>
    /// Sampling ratio (0.0 - 1.0). Use 1.0 in lower envs; consider 0.1–0.3 in high traffic prod.
    /// </summary>
    public double SamplingRatio { get; init; } = 1.0;

    /// <summary>
    /// Optional override for Azure Monitor connection string.
    /// If not set, SDK will use the APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
    /// </summary>
    public string? AzureMonitorConnectionString { get; init; }
}