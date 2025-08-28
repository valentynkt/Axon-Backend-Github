# OpenTelemetry 1.12.0 Implementation Guide for .NET 10

## Overview

This guide provides comprehensive implementation guidance for OpenTelemetry version 1.12.0 in the Axon Backend project, focusing on distributed tracing, metrics collection, and observability for our Clean Architecture + DDD + CQRS patterns.

## Compatibility and Version Information

**OpenTelemetry .NET 1.12.0** is fully compatible with **.NET 10 preview** and provides:
- Native support for System.Diagnostics.Activity API
- W3C TraceContext compliance
- OTLP (OpenTelemetry Protocol) exporters
- ASP.NET Core automatic instrumentation
- Custom instrumentation for CQRS patterns

## Package Dependencies

### Core Packages
```xml
<PackageReference Include="OpenTelemetry" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.12.0" />
```

### Optional Exporters
```xml
<!-- For development/testing -->
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.12.0" />

<!-- For Jaeger integration (traces only) -->
<PackageReference Include="OpenTelemetry.Exporter.Jaeger" Version="1.12.0" />

<!-- For Prometheus metrics -->
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.12.0" />
```

## Basic Configuration

### Program.cs Setup
```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
const string serviceName = "axon-backend";
const string serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName,
            ["service.namespace"] = "axon"
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            // Filter out health checks and metrics endpoints
            options.Filter = httpContext => 
                !httpContext.Request.Path.StartsWithSegments("/health") &&
                !httpContext.Request.Path.StartsWithSegments("/metrics");
                
            // Enrich spans with additional data
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.user_agent", request.Headers.UserAgent.ToString());
                activity.SetTag("http.request.content_length", request.ContentLength);
            };
            
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.content_length", response.ContentLength);
            };
        })
        .AddHttpClientInstrumentation()
        // Add custom source for CQRS operations
        .AddSource("Axon.CQRS")
        .AddSource("Axon.Domain")
        .AddConsoleExporter()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        // Add custom meters
        .AddMeter("Axon.CQRS")
        .AddMeter("Axon.Domain")
        .AddConsoleExporter()
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddConsoleExporter()
        .AddOtlpExporter());

var app = builder.Build();
```

## CQRS and MediatR Integration

### Custom Pipeline Behavior for Tracing
```csharp
using System.Diagnostics;
using MediatR;
using OpenTelemetry.Trace;

namespace Axon.Shared.Infrastructure.Telemetry;

public sealed class TracingPipelineBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ActivitySource ActivitySource = new("Axon.CQRS");
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);
        var operationType = GetOperationType(requestType);
        var operationName = $"{operationType}.{requestType.Name}";
        
        using var activity = ActivitySource.StartActivity(operationName);
        
        // Set standard tags
        activity?.SetTag("cqrs.operation.type", operationType);
        activity?.SetTag("cqrs.operation.name", requestType.Name);
        activity?.SetTag("cqrs.request.type", requestType.FullName);
        
        // Add request-specific tags if it's a query with parameters
        if (request is IQuery query)
        {
            AddQueryTags(activity, query);
        }
        else if (request is ICommand command)
        {
            AddCommandTags(activity, command);
        }
        
        try
        {
            var response = await next();
            
            // Mark as successful
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("cqrs.result.success", true);
            
            return response;
        }
        catch (Exception ex)
        {
            // Record the exception
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("cqrs.result.success", false);
            activity?.SetTag("cqrs.error.type", ex.GetType().Name);
            activity?.SetTag("cqrs.error.message", ex.Message);
            
            throw;
        }
    }
    
    private static string GetOperationType(Type requestType)
    {
        if (typeof(ICommand).IsAssignableFrom(requestType))
            return "Command";
        if (typeof(IQuery).IsAssignableFrom(requestType))
            return "Query";
        return "Request";
    }
    
    private static void AddQueryTags(Activity? activity, IQuery query)
    {
        // Add query-specific properties
        var properties = query.GetType().GetProperties();
        foreach (var property in properties.Take(5)) // Limit to avoid too many tags
        {
            var value = property.GetValue(query);
            if (value != null)
            {
                activity?.SetTag($"cqrs.query.{property.Name.ToLowerInvariant()}", value.ToString());
            }
        }
    }
    
    private static void AddCommandTags(Activity? activity, ICommand command)
    {
        // Add command ID if available
        var idProperty = command.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            var id = idProperty.GetValue(command);
            activity?.SetTag("cqrs.command.id", id?.ToString());
        }
        
        // Add aggregate type if available
        var aggregateProperty = command.GetType().GetProperty("AggregateType");
        if (aggregateProperty != null)
        {
            var aggregateType = aggregateProperty.GetValue(command);
            activity?.SetTag("cqrs.command.aggregate_type", aggregateType?.ToString());
        }
    }
}

// Register in DI container
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingPipelineBehavior<,>));
```

### Custom Metrics for CQRS Operations
```csharp
using System.Diagnostics.Metrics;

namespace Axon.Shared.Infrastructure.Telemetry;

public sealed class CqrsMetrics
{
    private static readonly Meter Meter = new("Axon.CQRS");
    
    private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
        "cqrs_operations_total",
        "operations",
        "Total number of CQRS operations");
        
    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "cqrs_operation_duration_seconds",
        "seconds",
        "Duration of CQRS operations");
        
    private static readonly Counter<long> OperationErrors = Meter.CreateCounter<long>(
        "cqrs_operation_errors_total",
        "errors",
        "Total number of CQRS operation errors");
    
    public static void RecordOperation(string operationType, string operationName, double duration, bool success)
    {
        var tags = new TagList
        {
            ["operation_type"] = operationType,
            ["operation_name"] = operationName,
            ["success"] = success.ToString().ToLowerInvariant()
        };
        
        OperationCounter.Add(1, tags);
        OperationDuration.Record(duration, tags);
        
        if (!success)
        {
            OperationErrors.Add(1, tags);
        }
    }
}
```

## Domain Event Instrumentation

### Domain Event Tracing
```csharp
using System.Diagnostics;

namespace Axon.Shared.Domain;

public abstract class AggregateRoot<TId> where TId : notnull
{
    private static readonly ActivitySource ActivitySource = new("Axon.Domain");
    
    private readonly List<IDomainEvent> _domainEvents = [];
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        using var activity = ActivitySource.StartActivity($"DomainEvent.{domainEvent.GetType().Name}");
        
        activity?.SetTag("domain.event.type", domainEvent.GetType().Name);
        activity?.SetTag("domain.event.aggregate_id", Id?.ToString());
        activity?.SetTag("domain.event.aggregate_type", GetType().Name);
        activity?.SetTag("domain.event.timestamp", domainEvent.OccurredOn.ToString("O"));
        
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

## Custom Instrumentation Examples

### Repository Pattern Instrumentation
```csharp
using System.Diagnostics;

namespace Axon.Shared.Infrastructure.Persistence;

public abstract class Repository<TEntity, TId> where TEntity : AggregateRoot<TId> where TId : notnull
{
    private static readonly ActivitySource ActivitySource = new("Axon.Repository");
    protected readonly DbContext Context;
    
    protected Repository(DbContext context)
    {
        Context = context;
    }
    
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"Repository.GetById");
        
        activity?.SetTag("repository.entity_type", typeof(TEntity).Name);
        activity?.SetTag("repository.operation", "GetById");
        activity?.SetTag("repository.entity_id", id.ToString());
        
        try
        {
            var entity = await Context.Set<TEntity>().FindAsync([id], cancellationToken);
            
            activity?.SetTag("repository.result.found", entity != null);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return entity;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
    
    public async Task SaveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"Repository.Save");
        
        activity?.SetTag("repository.entity_type", typeof(TEntity).Name);
        activity?.SetTag("repository.operation", "Save");
        activity?.SetTag("repository.entity_id", entity.Id?.ToString());
        
        try
        {
            Context.Set<TEntity>().Update(entity);
            var changes = await Context.SaveChangesAsync(cancellationToken);
            
            activity?.SetTag("repository.changes_saved", changes);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## OTLP Exporters Configuration

### Environment Variables
```bash
# OTLP Endpoint Configuration
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc

# Service Information
OTEL_SERVICE_NAME=axon-backend
OTEL_SERVICE_VERSION=1.0.0
OTEL_RESOURCE_ATTRIBUTES=service.namespace=axon,deployment.environment=development

# Sampling Configuration
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1

# Batch Export Configuration
OTEL_BSP_MAX_EXPORT_BATCH_SIZE=512
OTEL_BSP_EXPORT_TIMEOUT=30000
OTEL_BSP_SCHEDULE_DELAY=5000
```

### Production OTLP Configuration
```csharp
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAxonOpenTelemetry(
        this IServiceCollection services, 
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "axon-backend";
        
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(GetResourceAttributes(configuration, environment)))
            .WithTracing(tracing => ConfigureTracing(tracing, otlpEndpoint, environment))
            .WithMetrics(metrics => ConfigureMetrics(metrics, otlpEndpoint, environment))
            .WithLogging(logging => ConfigureLogging(logging, otlpEndpoint, environment));
            
        return services;
    }
    
    private static TracerProviderBuilder ConfigureTracing(
        TracerProviderBuilder tracing, 
        string otlpEndpoint, 
        IWebHostEnvironment environment)
    {
        tracing
            .AddAspNetCoreInstrumentation(ConfigureAspNetCoreInstrumentation)
            .AddHttpClientInstrumentation()
            .AddSource("Axon.CQRS")
            .AddSource("Axon.Domain")
            .AddSource("Axon.Repository");
            
        if (environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
        
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            options.TimeoutMilliseconds = 30000;
        });
        
        return tracing;
    }
    
    private static void ConfigureAspNetCoreInstrumentation(AspNetCoreTraceInstrumentationOptions options)
    {
        // Filter out health checks and internal endpoints
        options.Filter = httpContext =>
        {
            var path = httpContext.Request.Path.Value?.ToLowerInvariant();
            return !IsInternalEndpoint(path);
        };
        
        // Enrich with custom data
        options.EnrichWithHttpRequest = EnrichWithHttpRequest;
        options.EnrichWithHttpResponse = EnrichWithHttpResponse;
    }
    
    private static bool IsInternalEndpoint(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        
        var internalPaths = new[] { "/health", "/metrics", "/swagger", "/_framework" };
        return internalPaths.Any(internalPath => path.StartsWith(internalPath));
    }
}
```

## Jaeger Integration

### Jaeger-Specific Configuration
```csharp
// Note: Jaeger only supports traces, not metrics or logs via OTLP
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("Axon.CQRS")
        // For Jaeger, use either OTLP or native Jaeger exporter
        .AddJaegerExporter(options =>
        {
            options.AgentHost = "localhost";
            options.AgentPort = 6831;
        })
        // OR use OTLP to Jaeger (recommended)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317"); // Jaeger OTLP endpoint
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        }));
```

### Docker Compose for Jaeger
```yaml
version: '3.8'
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"    # Jaeger UI
      - "4317:4317"      # OTLP gRPC receiver
      - "4318:4318"      # OTLP HTTP receiver
      - "14268:14268"    # Jaeger collector HTTP
    environment:
      - COLLECTOR_OTLP_ENABLED=true
```

## Performance Considerations

### Sampling Configuration
```csharp
// Configure sampling to reduce overhead
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetSampler(new TraceIdRatioBasedSampler(0.1)) // Sample 10% of traces
        .AddAspNetCoreInstrumentation(options =>
        {
            // Use conditional enrichment to reduce overhead
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                if (activity.IsAllDataRequested)
                {
                    // Only add expensive tags if someone is listening
                    activity.SetTag("http.request.body.size", request.ContentLength);
                }
            };
        }));
```

### Batch Processing Configuration
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddProcessor(new BatchActivityExportProcessor(
            new OtlpTraceExporter(new OtlpExporterOptions()),
            maxQueueSize: 2048,
            scheduledDelayMilliseconds: 5000,
            exporterTimeoutMilliseconds: 30000,
            maxExportBatchSize: 512)));
```

### High-Performance Scenarios
```csharp
public sealed class HighPerformanceActivity
{
    private static readonly ActivitySource ActivitySource = new("Axon.HighPerf");
    
    public void ProcessHighVolumeOperation()
    {
        // Check if anyone is listening before creating expensive spans
        if (!ActivitySource.HasListeners())
        {
            // Fast path - no telemetry overhead
            return;
        }
        
        using var activity = ActivitySource.StartActivity("HighVolumeOperation");
        
        // Only add tags if data is requested
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("operation.volume", "high");
        }
        
        // Your high-performance code here
    }
}
```

## Metrics Collection Patterns

### Custom Business Metrics
```csharp
using System.Diagnostics.Metrics;

namespace Axon.Modules.Chat.Application.Metrics;

public sealed class ChatMetrics
{
    private static readonly Meter Meter = new("Axon.Chat");
    
    // Counters
    private static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>(
        "chat_messages_processed_total",
        "messages",
        "Total number of chat messages processed");
        
    private static readonly Counter<long> ConversationsCreated = Meter.CreateCounter<long>(
        "chat_conversations_created_total",
        "conversations",
        "Total number of conversations created");
    
    // Histograms
    private static readonly Histogram<double> MessageProcessingTime = Meter.CreateHistogram<double>(
        "chat_message_processing_duration_seconds",
        "seconds",
        "Time taken to process chat messages");
        
    private static readonly Histogram<long> MessageLength = Meter.CreateHistogram<long>(
        "chat_message_length_characters",
        "characters",
        "Length of chat messages in characters");
    
    // Gauges (using UpDownCounter as .NET doesn't have native Gauge)
    private static readonly UpDownCounter<long> ActiveConversations = Meter.CreateUpDownCounter<long>(
        "chat_active_conversations",
        "conversations",
        "Number of currently active conversations");
    
    public static void RecordMessageProcessed(string messageType, double processingTimeSeconds, long messageLength)
    {
        var tags = new TagList { ["message_type"] = messageType };
        
        MessagesProcessed.Add(1, tags);
        MessageProcessingTime.Record(processingTimeSeconds, tags);
        MessageLength.Record(messageLength, tags);
    }
    
    public static void RecordConversationCreated(string conversationType)
    {
        var tags = new TagList { ["conversation_type"] = conversationType };
        ConversationsCreated.Add(1, tags);
        ActiveConversations.Add(1, tags);
    }
    
    public static void RecordConversationEnded(string conversationType)
    {
        var tags = new TagList { ["conversation_type"] = conversationType };
        ActiveConversations.Add(-1, tags);
    }
}
```

## Health Checks Integration

### OpenTelemetry Health Check
```csharp
public sealed class OpenTelemetryHealthCheck : IHealthCheck
{
    private static readonly ActivitySource ActivitySource = new("Axon.HealthCheck");
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("HealthCheck.OpenTelemetry");
        
        try
        {
            // Check if telemetry is working by creating a test span
            using var testActivity = ActivitySource.StartActivity("HealthCheck.Test");
            testActivity?.SetTag("health_check", "test");
            
            // Simulate some work
            await Task.Delay(10, cancellationToken);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            return HealthCheckResult.Healthy("OpenTelemetry is working correctly");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return HealthCheckResult.Unhealthy("OpenTelemetry is not working", ex);
        }
    }
}

// Register the health check
services.AddHealthChecks()
    .AddCheck<OpenTelemetryHealthCheck>("opentelemetry");
```

## Integration Testing

### Testing OpenTelemetry Instrumentation
```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit;

namespace Axon.Tests.Integration.Telemetry;

public sealed class OpenTelemetryInstrumentationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<Activity> _exportedActivities = [];
    
    public OpenTelemetryInstrumentationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace OTLP exporter with in-memory exporter for testing
                services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing
                        .AddInMemoryExporter(_exportedActivities));
            });
        });
    }
    
    [Fact]
    public async Task ProcessMessage_ShouldCreateTraceSpan()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { Message = "Test message", UserId = Guid.NewGuid() };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/chat/messages", request);
        
        // Assert
        response.Should().BeSuccessful();
        
        var cqrsActivities = _exportedActivities
            .Where(a => a.Source.Name == "Axon.CQRS")
            .ToList();
            
        cqrsActivities.Should().NotBeEmpty();
        
        var commandActivity = cqrsActivities
            .FirstOrDefault(a => a.Tags.Any(t => t.Key == "cqrs.operation.type" && t.Value == "Command"));
            
        commandActivity.Should().NotBeNull();
        commandActivity!.Tags.Should().Contain(t => t.Key == "cqrs.result.success" && t.Value == "true");
    }
}
```

## Troubleshooting

### Common Issues and Solutions

1. **No traces appearing in Jaeger**
   - Verify OTLP endpoint configuration
   - Check that Jaeger is configured to receive OTLP data
   - Ensure sampling rate is not too low

2. **High memory usage**
   - Reduce batch sizes and export frequency
   - Implement appropriate sampling
   - Use conditional enrichment with `IsAllDataRequested`

3. **Performance impact**
   - Monitor CPU usage and adjust sampling
   - Use `HasListeners()` check for high-throughput scenarios
   - Consider using separate ActivitySources for different components

4. **Missing custom spans**
   - Verify ActivitySource is registered with OpenTelemetry
   - Check that the source name matches the AddSource() configuration
   - Ensure proper disposal of Activity objects

### Logging Configuration
```csharp
// Enable OpenTelemetry internal logging for debugging
services.AddLogging(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.ParseStateValues = true;
    });
});
```

## Best Practices Summary

1. **Use meaningful span names** that indicate the operation being performed
2. **Add context-relevant tags** but avoid high-cardinality values
3. **Implement proper error handling** with appropriate status codes
4. **Use sampling in production** to manage performance and costs
5. **Separate ActivitySources** for different application layers
6. **Check `IsAllDataRequested`** before adding expensive tags
7. **Use structured logging** alongside tracing for complete observability
8. **Monitor your monitoring** - track the performance impact of telemetry
9. **Test your instrumentation** with integration tests
10. **Keep spans focused** on single units of work

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/dotnet/)
- [Microsoft .NET Observability Guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [OTLP Exporter Configuration](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
- [MediatR OpenTelemetry Discussion](https://github.com/jbogard/MediatR/discussions/762)
- [System.Diagnostics.Activity Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)
