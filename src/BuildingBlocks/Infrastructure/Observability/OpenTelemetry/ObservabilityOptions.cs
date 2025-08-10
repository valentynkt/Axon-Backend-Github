namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

public sealed class ObservabilityOptions
{
    public string InstrumentationName { get; init; } = string.Empty;
    public string? ServiceName { get; init; }
    public bool MetricsEnabled { get; init; } = true;
    public bool TracingEnabled { get; init; } = true;
    public bool LoggingEnabled { get; init; } = true;
    public bool UsePrometheusExporter { get; init; } = true;
    public bool UseOTLPExporter { get; init; } = true;
    public bool UseAspireOTLPExporter { get; init; } = true;
    public bool UseGrafanaExporter { get; init; }
    public bool UseConsoleExporter { get; init; }
    public bool UseJaegerExporter { get; init; }
    public bool UseZipkinExporter { get; init; }
    public ZipkinOptions ZipkinOptions { get; init; } = new();
    public JaegerOptions JaegerOptions { get; init; } = new();
    public OTLPOptions OTLPOptions { get; init; } = new();
    public AspireDashboardOTLPOptions AspireDashboardOTLPOptions { get; init; } = new();
}

// https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Zipkin/README.md
public sealed record ZipkinOptions
{
    /// <summary>
    /// Gets or sets endpoint address to receive telemetry
    /// </summary>
    public string HttpExporterEndpoint { get; init; } = "http://localhost:9411/api/v2/spans";
}

public sealed record JaegerOptions
{
    public string OTLPGrpcExporterEndpoint { get; init; } = "http://localhost:14317";
    public string HttpExporterEndpoint { get; init; } = "http://localhost:14268/api/traces";
}

public sealed record OTLPOptions
{
    public string OTLPGrpcExporterEndpoint { get; init; } = "http://localhost:4317";
    public string OTLPHttpExporterEndpoint { get; init; } = "http://localhost:4318";
}

public sealed record AspireDashboardOTLPOptions
{
    public string OTLPGrpcExporterEndpoint { get; init; } = "http://localhost:4319";
}