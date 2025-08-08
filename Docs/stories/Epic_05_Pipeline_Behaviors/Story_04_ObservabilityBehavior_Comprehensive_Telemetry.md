# Story 04: ObservabilityBehavior - Comprehensive Telemetry

## Story Overview
**Story ID**: Epic_05_Story_04  
**Story Name**: ObservabilityBehavior - Comprehensive Telemetry  
**Estimated Duration**: 1-2 days  
**Dependencies**: 
- Epic_04 (CQRS Foundation)
- OpenTelemetry packages
- Result pattern from Epic_03

## User Story
**As a developer**, I want production-grade observability so that I can monitor, debug, and optimize application behavior with detailed insights through OpenTelemetry.

## Acceptance Criteria
- [ ] ObservabilityBehavior created as outermost pipeline wrapper
- [ ] Result<T> aware telemetry with success/failure categorization
- [ ] OpenTelemetry activities (traces) with rich context
- [ ] Comprehensive metrics using System.Diagnostics.Metrics
- [ ] Correlation ID generation and propagation
- [ ] W3C Trace Context support for distributed tracing
- [ ] Business metrics extraction from requests/responses
- [ ] Performance overhead < 1ms

## Technical Implementation

### Core Components

#### 1. ObservabilityBehavior Class
```csharp
namespace Axon.BuildingBlocks.Application.Behaviors;

public sealed class ObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly ILogger<ObservabilityBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    
    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new("Axon.Application");
    private static readonly Meter Meter = new("Axon.Application");
    
    // Metrics instruments
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "axon.requests.total",
        description: "Total number of requests");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "axon.requests.duration",
        unit: "ms",
        description: "Request duration in milliseconds");
    private static readonly Counter<long> RequestErrors = Meter.CreateCounter<long>(
        "axon.requests.errors",
        description: "Total number of request errors");
}
```

#### 2. Correlation ID Provider
```csharp
namespace Axon.BuildingBlocks.Application.Correlation;

public interface ICorrelationIdProvider
{
    string GetOrGenerate();
    void Set(string correlationId);
}

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AsyncLocal<string?> _correlationId = new();
    
    public string GetOrGenerate()
    {
        // Check HTTP header first, then AsyncLocal, then generate
        return _httpContextAccessor?.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? _correlationId.Value
            ?? GenerateCorrelationId();
    }
    
    private static string GenerateCorrelationId() => 
        $"axon-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
}
```

### Tasks

#### Task 1: Create ObservabilityBehavior Infrastructure
- [ ] Create `src/BuildingBlocks/Application/Behaviors/ObservabilityBehavior.cs`
- [ ] Configure as outermost pipeline behavior in registration
- [ ] Initialize ActivitySource for distributed tracing
- [ ] Create Meter and metric instruments

#### Task 2: Activity (Trace) Implementation
- [ ] Start new Activity for each request
- [ ] Set activity name: `{RequestType}.Handle`
- [ ] Add standard tags from request properties
- [ ] Extract and propagate W3C trace context
- [ ] Handle activity disposal in finally block

#### Task 3: Correlation ID Management
- [ ] Create ICorrelationIdProvider interface
- [ ] Implement CorrelationIdProvider with AsyncLocal storage
- [ ] Extract from HTTP headers if present
- [ ] Generate deterministic ID if not present
- [ ] Add to Activity tags and log scope

#### Task 4: Result-Aware Metrics
- [ ] Detect Result<T> success/failure status
- [ ] Categorize: success, business_failure, exception
- [ ] Extract error codes from Result.Error
- [ ] Track specific error types and frequencies
- [ ] Record sanitized error messages

#### Task 5: OpenTelemetry Metrics
- [ ] Request counter with dimensions
- [ ] Duration histogram with percentiles
- [ ] Error counter by error type
- [ ] Add tags: request_type, outcome, error_code
- [ ] Support custom business metrics

#### Task 6: Rich Activity Context
- [ ] Add request type and properties as tags
- [ ] Include user context (userId, tenantId)
- [ ] Add environment metadata
- [ ] Include deployment version
- [ ] Support custom domain tags via attributes

#### Task 7: W3C Trace Context Support
- [ ] Extract traceparent and tracestate headers
- [ ] Propagate context to downstream services
- [ ] Support baggage for cross-cutting concerns
- [ ] Integrate with HttpClient instrumentation
- [ ] Handle both incoming and outgoing contexts

#### Task 8: Performance Optimization
- [ ] Minimize allocations using object pooling
- [ ] Cache reflection results for tag extraction
- [ ] Use high-performance timing (Stopwatch)
- [ ] Lazy evaluation of expensive tags
- [ ] Benchmark overhead < 1ms

#### Task 9: Testing
- [ ] Unit tests for metric calculations
- [ ] Activity propagation verification
- [ ] Correlation ID flow tests
- [ ] W3C trace context tests
- [ ] Performance benchmarks
- [ ] Integration with OTLP exporter

## Code Examples

### Activity Tags
```csharp
using var activity = ActivitySource.StartActivity(
    $"{typeof(TRequest).Name}.Handle",
    ActivityKind.Internal);

if (activity != null)
{
    activity.SetTag("request.type", typeof(TRequest).Name);
    activity.SetTag("request.namespace", typeof(TRequest).Namespace);
    activity.SetTag("correlation.id", correlationId);
    activity.SetTag("user.id", _currentUser?.Id);
    activity.SetTag("tenant.id", _tenantContext?.Id);
    
    // After execution
    activity.SetTag("outcome", response.IsSuccess ? "success" : "failure");
    activity.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
    
    if (!response.IsSuccess)
    {
        activity.SetTag("error.type", response.Error?.Code);
        activity.SetTag("error.message", response.Error?.Message);
        activity.SetStatus(ActivityStatusCode.Error, response.Error?.Message);
    }
}
```

### Metrics Recording
```csharp
var tags = new TagList
{
    { "request.type", typeof(TRequest).Name },
    { "outcome", outcome },
    { "error.type", errorType }
};

RequestCounter.Add(1, tags);
RequestDuration.Record(stopwatch.ElapsedMilliseconds, tags);

if (!response.IsSuccess)
{
    RequestErrors.Add(1, tags);
}
```

### OpenTelemetry Configuration
```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Axon.Application")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddMeter("Axon.Application")
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

## Definition of Done
- [ ] ObservabilityBehavior implemented as outermost behavior
- [ ] OpenTelemetry activities properly created and tagged
- [ ] Metrics collected with appropriate dimensions
- [ ] Correlation IDs flow through entire pipeline
- [ ] W3C trace context properly propagated
- [ ] Result<T> outcomes correctly categorized
- [ ] Performance overhead verified < 1ms
- [ ] Integration with OTLP exporter tested
- [ ] Documentation includes dashboard examples

## Technical Notes

### Performance Targets
- Activity creation: < 0.5ms
- Metric recording: < 0.1ms per metric
- Tag extraction: < 0.2ms
- Total overhead: < 1ms

### Registration Order (Critical)
```csharp
services.AddMediatR(cfg =>
{
    cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));  // MUST be first
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Dashboard Queries (Prometheus)
```promql
# Request rate
rate(axon_requests_total[5m])

# Error rate
rate(axon_requests_errors[5m]) / rate(axon_requests_total[5m])

# P95 latency
histogram_quantile(0.95, rate(axon_requests_duration_bucket[5m]))

# Requests by type
sum by (request_type) (rate(axon_requests_total[5m]))
```

## Dependencies
- System.Diagnostics.DiagnosticSource 8.0.0
- OpenTelemetry 1.7.0
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Exporter.OpenTelemetryProtocol
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Instrumentation.EntityFrameworkCore

## References
- OpenTelemetry .NET: https://github.com/open-telemetry/opentelemetry-dotnet
- W3C Trace Context: https://www.w3.org/TR/trace-context/
- System.Diagnostics.Metrics: https://docs.microsoft.com/dotnet/core/diagnostics/metrics