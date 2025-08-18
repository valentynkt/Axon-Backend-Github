using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;
using Azure.Monitor.OpenTelemetry.AspNetCore; // from Azure.Monitor.OpenTelemetry.AspNetCore

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Minimal OpenTelemetry wiring that exports to Azure Monitor (Application Insights).
/// No self-hosted exporters. No custom processors. Just the essentials:
/// - Resource with service.name/version/environment
/// - Traces: ASP.NET Core, HttpClient, EF Core + your ActivitySource
/// - Metrics: ASP.NET Core, HttpClient, runtime/process + your Meter
/// - Logs: via Azure Monitor distro
/// </summary>
public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddAppInsightsOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ObservabilityOptions>? configure = null)
    {
        var opts = new ObservabilityOptions();
        configuration.GetSection("Observability").Bind(opts);
        configure?.Invoke(opts);

        // Resolve service name/version
        var entryAsm = Assembly.GetEntryAssembly();
        var serviceName = opts.ServiceName ?? entryAsm?.GetName().Name ?? "Axon.Service";
        var serviceVersion = entryAsm?.GetName().Version?.ToString() ?? "1.0.0";

        // Ensure a single instrumentation prefix shared by App + Infra
        ObservabilityConstant.InstrumentationName =
            string.IsNullOrWhiteSpace(opts.InstrumentationName) ? serviceName : opts.InstrumentationName;

        var activitySourceName = TelemetryTags.Tracing.Application.AppService;
        var meterName          = TelemetryTags.Metrics.Application.AppService;

        // Build common Resource
        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>(
                    "deployment.environment",
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            });

        // Add OpenTelemetry and Azure Monitor exporter in one go.
        // The Azure Monitor distro wires Traces, Metrics, and Logs exporters.
        services.AddOpenTelemetry()
            .ConfigureResource(rb =>
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

                rb.AddService(
                        serviceName: serviceName,
                        serviceVersion: serviceVersion)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("deployment.environment", env)
                    });
            })
            .UseAzureMonitor(azure =>
            {
                // Prefer explicit options, else rely on APPLICATIONINSIGHTS_CONNECTION_STRING env var
                var conn = opts.AzureMonitorConnectionString
                           ?? configuration["AzureMonitor:ConnectionString"]
                           ?? configuration["ApplicationInsights:ConnectionString"];
                if (!string.IsNullOrWhiteSpace(conn))
                    azure.ConnectionString = conn;
            })
            // Tracing
            .WithTracing(b =>
            {
                if (!opts.TracingEnabled) return;

                b.AddSource(activitySourceName)           // your Application layer ActivitySource
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                var ratio = Math.Clamp(opts.SamplingRatio, 0.0, 1.0);
                b.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)));
            })
            // Metrics
            .WithMetrics(b =>
            {
                if (!opts.MetricsEnabled) return;

                b.AddMeter(meterName)                     // your Application layer Meter
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();
            });

        // Logging:
        // Azure Monitor distro takes care of ILogger -> AI export.
        // No extra code required; keep your existing logging configuration.

        return services;
    }
}
