# Observability Guide

**Comprehensive guide to OpenTelemetry, structured logging, metrics, and health checks in Axon.**

---

## Overview

**Tracing**: OpenTelemetry with Azure Monitor (Application Insights)
**Logging**: Console + structured logs with correlation IDs
**Metrics**: OpenTelemetry Meter (Prometheus-compatible)
**Health**: ASP.NET Core health checks

---

## OpenTelemetry Architecture

### Configuration

```csharp
// In ServiceRegistration.cs
services.AddAppInsightsOpenTelemetry(configuration, opts =>
{
    opts.ServiceName = "Axon.Api";
    opts.TracingEnabled = true;
    opts.MetricsEnabled = true;
    opts.SamplingRatio = 0.1; // 10% sampling
    opts.AzureMonitorConnectionString = configuration["ApplicationInsights:ConnectionString"];
});
```

### Implementation (OpenTelemetryRegistration.cs)

```csharp
public static IServiceCollection AddAppInsightsOpenTelemetry(
    this IServiceCollection services,
    IConfiguration configuration,
    Action<ObservabilityOptions>? configure = null)
{
    var opts = new ObservabilityOptions();
    configuration.GetSection("Observability").Bind(opts);
    configure?.Invoke(opts);

    var serviceName = opts.ServiceName ?? "Axon.Service";
    var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
    var activitySourceName = "Axon.Application";
    var meterName = "Axon.Application";

    // Build resource with service metadata
    services.AddOpenTelemetry()
        .ConfigureResource(rb =>
        {
            rb.AddService(serviceName, serviceVersion)
              .AddAttributes(new[]
              {
                  new KeyValuePair<string, object>(
                      "deployment.environment",
                      Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
              });
        })
        .UseAzureMonitor(azure =>
        {
            var conn = opts.AzureMonitorConnectionString
                    ?? configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(conn))
                azure.ConnectionString = conn;
        })
        // Tracing
        .WithTracing(b =>
        {
            if (!opts.TracingEnabled) return;

            b.AddSource(activitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();

            // Sampling: 10% of traces
            var ratio = Math.Clamp(opts.SamplingRatio, 0.0, 1.0);
            b.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)));
        })
        // Metrics
        .WithMetrics(b =>
        {
            if (!opts.MetricsEnabled) return;

            b.AddMeter(meterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation();
        });

    return services;
}
```

---

## Distributed Tracing

### ActivitySource Setup (Instrumentation.cs)

```csharp
public static class Instrumentation
{
    /// <summary>Single ActivitySource for the entire application pipeline</summary>
    public static readonly ActivitySource ActivitySource = new("Axon.Application");

    /// <summary>Single Meter for the entire application pipeline</summary>
    public static readonly Meter Meter = new("Axon.Application");
}
```

### Creating Traces

```csharp
// In handler or service
using var activity = Instrumentation.ActivitySource.StartActivity("ExchangeCredential");
activity?.SetTag("principal.id", principalId.ToString());
activity?.SetTag("provider", providerType.Value);

try
{
    // Business logic
    var result = await _service.ExchangeAsync(token, ct);

    activity?.SetTag("result.success", result.IsSuccess);
    if (result.IsFailure)
        activity?.SetTag("error.code", result.Error.Code);

    return result;
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

### Observability Behavior (MediatR Pipeline)

```csharp
public sealed class ObservabilityBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;

        using var activity = Instrumentation.ActivitySource.StartActivity($"Request.{requestName}");
        activity?.SetTag("request.type", requestName);
        activity?.SetTag("request.id", Guid.NewGuid().ToString());

        var sw = Stopwatch.StartNew();
        Instrumentation.Requests.Add(1, new KeyValuePair<string, object?>("request.type", requestName));

        try
        {
            var response = await next();

            sw.Stop();
            Instrumentation.Duration.Record(sw.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("request.type", requestName));

            activity?.SetTag("request.duration_ms", sw.ElapsedMilliseconds);

            // Check if response is Result<T, Error>
            if (response is IResult result && !result.IsSuccess)
            {
                Instrumentation.Failures.Add(1,
                    new KeyValuePair<string, object?>("request.type", requestName));
                activity?.SetTag("result.failure", true);
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            Instrumentation.Cancelled.Add(1,
                new KeyValuePair<string, object?>("request.type", requestName));
            activity?.SetStatus(ActivityStatusCode.Error, "Request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Instrumentation.Failures.Add(1,
                new KeyValuePair<string, object?>("request.type", requestName));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

---

## Metrics

### Standard Metrics (Instrumentation.cs)

```csharp
public static class Instrumentation
{
    /// <summary>Counter for total requests</summary>
    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("axon.requests", description: "Total requests");

    /// <summary>Counter for failed requests</summary>
    public static readonly Counter<long> Failures =
        Meter.CreateCounter<long>("axon.requests.failures", description: "Failed requests");

    /// <summary>Counter for cancelled requests</summary>
    public static readonly Counter<long> Cancelled =
        Meter.CreateCounter<long>("axon.requests.cancelled", description: "Cancelled requests");

    /// <summary>Histogram for request duration</summary>
    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("axon.request.duration", unit: "ms", description: "Request duration (ms)");

    /// <summary>Counter for ETag cache hits</summary>
    public static readonly Counter<long> ETagHits =
        Meter.CreateCounter<long>("axon.etag.hits", description: "ETag cache hits (304 Not Modified)");

    /// <summary>Counter for rate limit violations</summary>
    public static readonly Counter<long> RateLimitHits =
        Meter.CreateCounter<long>("identity.rate_limit.hits_total", description: "Rate limit violations by IP");
}
```

### Recording Metrics

```csharp
// Increment counter
Instrumentation.Requests.Add(1,
    new KeyValuePair<string, object?>("endpoint", "/api/v1/auth/exchange"));

// Record histogram
Instrumentation.Duration.Record(elapsedMs,
    new KeyValuePair<string, object?>("endpoint", "/api/v1/auth/exchange"),
    new KeyValuePair<string, object?>("status_code", 200));

// Failure tracking
if (result.IsFailure)
{
    Instrumentation.Failures.Add(1,
        new KeyValuePair<string, object?>("error_code", result.Error.Code));
}
```

### Custom Module Metrics

```csharp
// In ChatTelemetry.cs
public sealed class ChatTelemetry
{
    private static readonly Meter Meter = new("Axon.Chat");

    public static readonly Counter<long> MessagesProcessed =
        Meter.CreateCounter<long>("chat.messages.processed", description: "Messages processed");

    public static readonly Counter<long> AiResponsesGenerated =
        Meter.CreateCounter<long>("chat.ai_responses.generated", description: "AI responses generated");

    public static readonly Histogram<double> AiResponseTime =
        Meter.CreateHistogram<double>("chat.ai_response.duration_ms", unit: "ms");
}

// Usage
ChatTelemetry.MessagesProcessed.Add(1,
    new KeyValuePair<string, object?>("conversation_id", conversationId.ToString()));

ChatTelemetry.AiResponseTime.Record(elapsedMs);
```

---

## Structured Logging

### Configuration (Program.cs)

```csharp
// Configure structured logging with correlation IDs
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "simple";
});

builder.Services.Configure<ConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});
```

### Structured Logging Examples

```csharp
public sealed class ExchangeCredentialHandler
{
    private readonly ILogger<ExchangeCredentialHandler> _logger;

    public async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation(
            "Exchanging credential for provider {Provider}",
            cmd.ProviderType);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = Guid.NewGuid(),
            ["Provider"] = cmd.ProviderType
        }))
        {
            try
            {
                var result = await _orchestrator.ExchangeAsync(cmd.BearerToken, ct);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Credential exchanged successfully for user {UserId}, isNew={IsNew}",
                        result.Value.UserId,
                        result.Value.IsNewPrincipal);
                }
                else
                {
                    _logger.LogWarning(
                        "Credential exchange failed: {ErrorCode} - {ErrorMessage}",
                        result.Error.Code,
                        result.Error.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during credential exchange");
                throw;
            }
        }
    }
}
```

### Correlation ID Middleware

```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();

    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["RequestPath"] = context.Request.Path
    }))
    {
        await next();
    }
});
```

---

## Health Checks

### Configuration

```csharp
services.AddHealthChecks()
    .AddNpgSql(
        configuration.GetConnectionString("IdentityDb")!,
        name: "identity-db",
        tags: ["database", "identity"])
    .AddNpgSql(
        configuration.GetConnectionString("ChatDb")!,
        name: "chat-db",
        tags: ["database", "chat"])
    .AddCheck<DynamicHealthCheck>("dynamic-auth", tags: ["external", "auth"])
    .AddCheck<AiServiceHealthCheck>("ai-service", tags: ["external", "ai"]);

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Always healthy (liveness probe)
});
```

### Custom Health Check

```csharp
public sealed class DynamicHealthCheck : IHealthCheck
{
    private readonly IDynamicApiClient _client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            // Check JWKS endpoint
            var jwks = await _client.GetJwksAsync(ct);

            if (jwks == null || jwks.Keys.Count == 0)
                return HealthCheckResult.Degraded("JWKS keys not available");

            return HealthCheckResult.Healthy("Dynamic.xyz is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Dynamic.xyz health check failed",
                ex);
        }
    }
}
```

---

## Best Practices

### ✅ DO
- Use single `ActivitySource` and `Meter` per application
- Add correlation IDs to all logs
- Use structured logging with context
- Sample traces (10-20% in production)
- Tag activities with business context (user ID, operation type)
- Record exceptions in activities
- Use log levels appropriately (Information, Warning, Error)
- Export to centralized observability platform (Azure Monitor, Grafana)

### ❌ DON'T
- Create multiple ActivitySource instances
- Log sensitive data (tokens, passwords, PII)
- Over-instrument (avoid trace per DB query)
- Use string interpolation in log messages (use structured parameters)
- Leave production at 100% trace sampling
- Ignore error/warning logs

---

## Querying Telemetry

### Azure Monitor (Application Insights) Queries

```kusto
// Request duration by endpoint
requests
| where timestamp > ago(1h)
| summarize avg(duration), max(duration), count() by name
| order by avg_duration desc

// Failed requests
requests
| where success == false
| project timestamp, name, resultCode, customDimensions
| order by timestamp desc

// Custom metrics
customMetrics
| where name == "axon.requests"
| summarize sum(value) by bin(timestamp, 5m)
| render timechart

// Exceptions
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, innermostMessage
| order by timestamp desc
```

---

## Testing Observability

```csharp
[Test]
public async Task Handler_ShouldRecordMetrics()
{
    // Arrange
    var metricsListener = new TestMetricListener();
    metricsListener.Start();

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    var metrics = metricsListener.GetMetrics("axon.requests");
    metrics.Count.ShouldBeGreaterThan(0);
    metrics.First().Value.ShouldBe(1);
}

[Test]
public async Task Handler_ShouldCreateActivity()
{
    // Arrange
    using var activityListener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == "Axon.Application",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        ActivityStarted = activity =>
        {
            activity.DisplayName.ShouldStartWith("Request.");
        }
    };
    ActivitySource.AddActivityListener(activityListener);

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    // Activity was created and sampled
}
```

---

## Related Documentation

- [Testing Guide](../../testing/TESTING-GUIDE.md) - Testing telemetry
- [OpenTelemetry Library](../../../Libraries/OpenTelemetry/IMPLEMENTATION_GUIDE.md) - OTel setup

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team