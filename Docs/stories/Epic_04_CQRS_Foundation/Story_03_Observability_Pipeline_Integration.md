# Story 03: Observability Pipeline Integration with W3C TraceContext

**Story ID:** AXON-CQRS-003  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P1 - High  
**Estimated Effort:** 4 hours  
**Dependencies:** Story_01 (W3C TraceContext)  
**Status:** ✅ **MOSTLY COMPLETE** (85% implemented)  
**Target Sprint:** **MINOR ENHANCEMENTS NEEDED**  

---

## ✅ **IMPLEMENTATION STATUS**

**What's Actually Implemented:**
- ✅ Comprehensive `ObservabilityPipelineBehavior` in `src/BuildingBlocks/Application/Behaviors/ObservabilityPipelineBehavior.cs`
- ✅ W3C ActivityIdFormat configured correctly in OpenTelemetry setup
- ✅ Command/Query activity tracking with proper span creation
- ✅ Metrics collection for execution times and failures
- ✅ Activity context propagation through MediatR pipeline
- ✅ Error handling and exception tracking

**What's Missing:**
- ⚠️ **Metadata enrichment**: Activity tags from request metadata (depends on Story_01)
- ⚠️ **Enhanced correlation**: Rich context from request properties
- ⚠️ **Custom telemetry**: Request-specific observability customization

**Implementation Quality:**
- ✅ **High**: Solid foundation with proper W3C compliance
- ✅ **Performance**: Efficient activity management
- ✅ **Error handling**: Comprehensive exception scenarios

---

## 📋 User Story

**As a** DevOps engineer monitoring the Axon system,  
**I want** comprehensive observability for all CQRS operations using W3C TraceContext standards,  
**So that** I can track, debug, and optimize command and query execution across distributed systems.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - ✅ **EXCELLENT**: Comprehensive `ObservabilityPipelineBehavior` implemented
  - ✅ **CONFIGURED**: OpenTelemetry with proper W3C ActivityIdFormat
  - ❌ **MISSING**: W3C TraceContext properties (Story 01 not implemented)
  - ⚠️ **BASIC**: Activity tracking without metadata enrichment

- **Integration Points:**
  - System.Diagnostics.Activity API
  - OpenTelemetry instrumentation
  - MediatR pipeline behaviors
  - Application Insights / Jaeger / Zipkin

- **Technology Stack:** 
  - .NET 10 with Activity API
  - OpenTelemetry.NET
  - MediatR for CQRS
  - Structured logging with Serilog

- **Architectural Layer:** BuildingBlocks/Application/Behaviors

### Patterns to Follow

```csharp
// Current ObservabilityPipelineBehavior
public class ObservabilityPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    var isCommand = message is ICommand<TResponse>;
    var isQuery = message is IQuery<TResponse>;
    // Limited implementation
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Activity Management**
   - [ ] Create child activities for each request
   - [ ] Set activity tags from request metadata
   - [ ] Track request type (command/query)
   - [ ] Record request/response payload sizes

2. **Trace Enrichment**
   - [ ] Add request ID as trace tag
   - [ ] Include user context from metadata
   - [ ] Track tenant ID if present
   - [ ] Record feature flags used

3. **Performance Metrics**
   - [ ] Measure handler execution time
   - [ ] Track queue time before execution
   - [ ] Record result status (success/failure)
   - [ ] Monitor pipeline behavior chain time

4. **Error Tracking**
   - [ ] Capture exception details in traces
   - [ ] Link errors to trace context
   - [ ] Record error categories
   - [ ] Track failure rates by operation

### Non-Functional Requirements

1. **Performance**
   - [ ] < 1ms overhead per request
   - [ ] Minimal memory allocations
   - [ ] Async trace export
   - [ ] Sampling support for high volume

2. **Standards Compliance**
   - [ ] W3C TraceContext headers
   - [ ] OpenTelemetry semantic conventions
   - [ ] Structured logging format
   - [ ] CloudEvents compatibility

3. **Observability**
   - [ ] Real-time metrics export
   - [ ] Distributed trace correlation
   - [ ] Custom dashboards support
   - [ ] Alert rule compatibility

---

## 🔧 Technical Implementation

### Files to Create/Modify

```yaml
Modified_Files:
  - src/BuildingBlocks/Application/Behaviors/ObservabilityPipelineBehavior.cs
  
New_Files:
  - src/BuildingBlocks/Application/Observability/ActivityEnricher.cs
  - src/BuildingBlocks/Application/Observability/MetricsCollector.cs
  - src/BuildingBlocks/Application/Observability/TraceContextPropagator.cs
  
Configuration:
  - src/BuildingBlocks/Application/Configuration/ObservabilityConfiguration.cs

Tests:
  - tests/BuildingBlocks.Tests/Application/Behaviors/ObservabilityBehaviorTests.cs
```

### Implementation Steps

#### Step 1: Enhanced Observability Behavior

```csharp
public class ObservabilityPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : notnull
{
    private readonly ILogger<ObservabilityPipelineBehavior<TRequest, TResponse>> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly ActivitySource _activitySource;
    
    public ObservabilityPipelineBehavior(
        ILogger<ObservabilityPipelineBehavior<TRequest, TResponse>> logger,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
        _activitySource = new ActivitySource("Axon.CQRS");
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = GetRequestType(request);
        var requestName = typeof(TRequest).Name;
        
        // Start new activity for this request
        using var activity = _activitySource.StartActivity(
            $"CQRS.{requestType}.{requestName}",
            ActivityKind.Internal,
            Activity.Current?.Context ?? default);
        
        if (activity != null)
        {
            // Enrich activity with W3C context
            EnrichActivityWithRequest(activity, request);
            
            // Add metadata as tags
            foreach (var (key, value) in request.Metadata)
            {
                activity.SetTag($"metadata.{key}", value?.ToString());
            }
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Executing {RequestType} {RequestName} with TraceId {TraceId}",
                requestType, requestName, request.TraceId);
            
            var response = await next();
            
            stopwatch.Stop();
            
            if (activity != null)
            {
                activity.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
                activity.SetStatus(ActivityStatusCode.Ok);
                
                if (response is IResult result)
                {
                    activity.SetTag("result.success", !result.IsFailure);
                    if (result.IsFailure)
                    {
                        activity.SetTag("error.type", result.Error?.Type.ToString());
                        activity.SetTag("error.code", result.Error?.Code);
                    }
                }
            }
            
            RecordMetrics(request, requestType, stopwatch.Elapsed, true);
            
            _logger.LogInformation(
                "Executed {RequestType} {RequestName} in {ElapsedMs}ms",
                requestType, requestName, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.RecordException(ex);
                activity.SetTag("error.type", ex.GetType().Name);
            }
            
            RecordMetrics(request, requestType, stopwatch.Elapsed, false);
            
            _logger.LogError(ex,
                "Failed executing {RequestType} {RequestName} after {ElapsedMs}ms",
                requestType, requestName, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
    
    private void EnrichActivityWithRequest(Activity activity, TRequest request)
    {
        // Standard OpenTelemetry semantic conventions
        activity.SetTag("messaging.system", "mediatr");
        activity.SetTag("messaging.destination", typeof(TRequest).Name);
        activity.SetTag("messaging.operation", GetRequestType(request));
        
        // Axon-specific tags
        activity.SetTag("axon.request.id", request.RequestId);
        activity.SetTag("axon.request.timestamp", request.RequestedAt);
        activity.SetTag("axon.request.type", typeof(TRequest).FullName);
        
        // Cache information for queries
        if (request is IQuery<object> query)
        {
            activity.SetTag("axon.cache.enabled", query.UseCache);
            if (query.UseCache)
            {
                activity.SetTag("axon.cache.duration", query.CacheDuration?.TotalSeconds);
            }
        }
        
        // Add baggage for cross-service propagation
        activity.SetBaggage("request.id", request.RequestId.ToString());
    }
    
    private string GetRequestType(TRequest request)
    {
        return request switch
        {
            ICommand<TResponse> => "Command",
            IQuery<TResponse> => "Query",
            _ => "Request"
        };
    }
    
    private void RecordMetrics(TRequest request, string requestType, TimeSpan duration, bool success)
    {
        var tags = new TagList
        {
            { "request.type", requestType },
            { "request.name", typeof(TRequest).Name },
            { "request.success", success }
        };
        
        _metrics.RecordRequestDuration(duration, tags);
        _metrics.IncrementRequestCount(tags);
        
        if (!success)
        {
            _metrics.IncrementErrorCount(tags);
        }
    }
}
```

#### Step 2: Metrics Collector

```csharp
public interface IMetricsCollector
{
    void RecordRequestDuration(TimeSpan duration, TagList tags);
    void IncrementRequestCount(TagList tags);
    void IncrementErrorCount(TagList tags);
    void RecordCacheHit(bool hit, TagList tags);
}

public class OpenTelemetryMetricsCollector : IMetricsCollector
{
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _requestCount;
    private readonly Counter<long> _errorCount;
    private readonly Counter<long> _cacheHits;
    
    public OpenTelemetryMetricsCollector(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Axon.CQRS");
        
        _requestDuration = meter.CreateHistogram<double>(
            "cqrs.request.duration",
            unit: "ms",
            description: "Duration of CQRS request execution");
            
        _requestCount = meter.CreateCounter<long>(
            "cqrs.request.count",
            description: "Total number of CQRS requests");
            
        _errorCount = meter.CreateCounter<long>(
            "cqrs.request.errors",
            description: "Total number of CQRS request errors");
            
        _cacheHits = meter.CreateCounter<long>(
            "cqrs.cache.hits",
            description: "Cache hit/miss count");
    }
    
    public void RecordRequestDuration(TimeSpan duration, TagList tags)
    {
        _requestDuration.Record(duration.TotalMilliseconds, tags);
    }
    
    public void IncrementRequestCount(TagList tags)
    {
        _requestCount.Add(1, tags);
    }
    
    public void IncrementErrorCount(TagList tags)
    {
        _errorCount.Add(1, tags);
    }
    
    public void RecordCacheHit(bool hit, TagList tags)
    {
        tags.Add("cache.hit", hit);
        _cacheHits.Add(1, tags);
    }
}
```

#### Step 3: Configuration

```csharp
public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservabilityPipeline(
        this IServiceCollection services,
        Action<OpenTelemetryBuilder>? configure = null)
    {
        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("Axon.Backend"))
                    .AddSource("Axon.CQRS")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .SetSampler(new TraceIdRatioBasedSampler(0.1)); // 10% sampling
                    
                configure?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("Axon.Backend"))
                    .AddMeter("Axon.CQRS")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            });
        
        // Add metrics collector
        services.AddSingleton<IMetricsCollector, OpenTelemetryMetricsCollector>();
        
        // Add pipeline behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityPipelineBehavior<,>));
        
        return services;
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Should_Create_Activity_With_W3C_Context()
{
    // Arrange
    var activitySource = new ActivitySource("Test");
    using var listener = new ActivityListener
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    };
    ActivitySource.AddActivityListener(listener);
    
    var command = new TestCommand();
    var behavior = new ObservabilityPipelineBehavior<TestCommand, TestResult>(...);
    
    // Act
    using var rootActivity = activitySource.StartActivity("Root");
    await behavior.Handle(command, () => Task.FromResult(new TestResult()), CancellationToken.None);
    
    // Assert
    var activities = listener.GetActivities();
    var cqrsActivity = activities.FirstOrDefault(a => a.OperationName.StartsWith("CQRS"));
    
    cqrsActivity.Should().NotBeNull();
    cqrsActivity!.TraceId.Should().Be(rootActivity!.TraceId);
    cqrsActivity.ParentId.Should().Be(rootActivity.SpanId);
}

[Fact]
public async Task Should_Record_Metadata_As_Activity_Tags()
{
    // Arrange
    var command = new TestCommand
    {
        Metadata = new Dictionary<string, object>
        {
            ["TenantId"] = "tenant-123",
            ["Feature"] = "beta"
        }.AsReadOnly()
    };
    
    // Act
    await behavior.Handle(command, next, CancellationToken.None);
    
    // Assert
    var activity = Activity.Current;
    activity?.GetTagItem("metadata.TenantId").Should().Be("tenant-123");
    activity?.GetTagItem("metadata.Feature").Should().Be("beta");
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Export_Traces_To_Jaeger()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    services.AddObservabilityPipeline(tracing =>
    {
        tracing.AddJaegerExporter(options =>
        {
            options.AgentHost = "localhost";
            options.AgentPort = 6831;
        });
    });
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    // Act
    var command = new CreateOrderCommand();
    await mediator.Send(command);
    
    // Assert
    // Verify trace appears in Jaeger
    // This would require a Jaeger test container
}
```

---

## 📐 Architecture Considerations

### Sampling Strategy

1. **Development**: 100% sampling for debugging
2. **Staging**: 10% sampling for validation
3. **Production**: 1% baseline + dynamic for errors
4. **Head-based sampling**: Decision at trace start

### Performance Impact

- Activity creation: ~100ns overhead
- Tag setting: ~50ns per tag
- Metrics recording: ~200ns per metric
- Total overhead: < 1ms per request

### Data Privacy

- Don't log PII in traces
- Sanitize sensitive metadata
- Use data classification tags
- Implement retention policies

---

## 📦 Definition of Done

- [ ] Enhanced ObservabilityPipelineBehavior implemented
- [ ] Metrics collector with OpenTelemetry integration
- [ ] W3C TraceContext properly propagated
- [ ] Metadata exposed as activity tags
- [ ] Unit tests with 100% coverage
- [ ] Integration tests with trace validation
- [ ] Performance benchmarks documented
- [ ] Dashboards configured in Grafana
- [ ] Alerts configured for error rates

---

## 🔄 Migration Strategy

### Phase 1: Enable Tracing
- Deploy OpenTelemetry collectors
- Configure trace exporters
- Enable sampling at 1%

### Phase 2: Add Metrics
- Deploy Prometheus/Grafana
- Create CQRS dashboards
- Set up alerting rules

### Phase 3: Full Observability
- Increase sampling rates
- Add custom dashboards
- Implement SLOs/SLIs

---

## 📊 Success Metrics

- 100% of requests have trace context
- P99 latency visibility within 1 second
- Error detection within 30 seconds
- Trace correlation success rate > 99%
- Dashboard load time < 2 seconds

---

## 🚀 Follow-up Stories

1. **Story 04**: Distributed Tracing Across Services
2. **Story 05**: Custom Metrics and SLIs
3. **Story 06**: Trace-based Testing
4. **Story 07**: Performance Profiling Integration