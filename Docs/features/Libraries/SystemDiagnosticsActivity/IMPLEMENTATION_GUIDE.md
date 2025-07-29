w# System.Diagnostics.Activity Implementation Guide

## Overview

System.Diagnostics.Activity is .NET's built-in distributed tracing framework that provides W3C TraceContext support, automatic HTTP header propagation, and seamless integration with OpenTelemetry. This guide covers comprehensive implementation for the Axon Backend project's CQRS architecture.

**Key Benefits for Axon Backend:**
- ✅ **Built into .NET 10** - No additional packages required
- ✅ **W3C TraceContext compliant** - Industry standard distributed tracing
- ✅ **Automatic HTTP propagation** - Cross-service correlation out-of-the-box
- ✅ **Performance optimized** - Smart sampling and listener patterns
- ✅ **OpenTelemetry compatible** - Works with any observability platform

## Core Concepts

### Activity vs ActivitySource
- **Activity** = A single unit of work (equivalent to OpenTelemetry "Span")
- **ActivitySource** = Factory for creating Activities (equivalent to OpenTelemetry "Tracer")
- **ActivityListener** = Subscriber to Activity events for collection/export

### W3C TraceContext Support
.NET 5+ uses W3C TraceContext format by default:
- **TraceId**: Unique identifier for the entire distributed trace
- **SpanId**: Unique identifier for the current activity
- **TraceFlags**: Sampling and processing flags
- **TraceState**: Vendor-specific state information

## Implementation Architecture

### 1. ActivitySource Configuration

Create a centralized ActivitySource for the Axon Backend application:

```csharp
// src/Shared/Common/Observability/AxonActivitySource.cs
using System.Diagnostics;
using System.Reflection;

namespace Axon.Shared.Common.Observability;

public static class AxonActivitySource
{
    private static readonly AssemblyName AssemblyName = typeof(AxonActivitySource).Assembly.GetName();
    
    public static readonly ActivitySource Instance = new(
        AssemblyName.Name ?? "Axon.Backend", 
        AssemblyName.Version?.ToString() ?? "1.0.0"
    );
    
    // Module-specific sources for granular control
    public static readonly ActivitySource Chat = new("Axon.Backend.Chat", "1.0.0");
    public static readonly ActivitySource Api = new("Axon.Backend.Api", "1.0.0");
    
    // Dispose method for graceful shutdown
    public static void Dispose()
    {
        Instance.Dispose();
        Chat.Dispose();
        Api.Dispose();
    }
}
```

### 2. CQRS Integration Patterns

#### Command Handler Instrumentation

```csharp
// src/Modules/Chat/Application/Commands/SendMessage/SendMessageHandler.cs
using System.Diagnostics;
using MediatR;
using Axon.Shared.Common.Observability;

public sealed class SendMessageHandler : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    private readonly IChatRepository _repository;
    private readonly IAiClient _aiClient;

    public SendMessageHandler(IChatRepository repository, IAiClient aiClient)
    {
        _repository = repository;
        _aiClient = aiClient;
    }

    public async Task<Result<MessageDto>> Handle(SendMessageCommand request, CancellationToken ct)
    {
        using var activity = AxonActivitySource.Chat.StartActivity("Chat.SendMessage");
        
        // Add command metadata to activity
        activity?.SetTag("command.type", nameof(SendMessageCommand));
        activity?.SetTag("user.id", request.UserId.ToString());
        activity?.SetTag("conversation.id", request.ConversationId.ToString());
        
        // Add sensitive data only if requested by listeners
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("message.length", request.Content.Length);
            activity.SetTag("message.preview", request.Content[..Math.Min(50, request.Content.Length)]);
        }

        try
        {
            // Business logic with nested activities
            var conversation = await GetConversationAsync(request.ConversationId, ct);
            if (conversation.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, conversation.Error.Message);
                return conversation.Error;
            }

            var aiResponse = await ProcessWithAiAsync(request.Content, ct);
            var message = await SaveMessageAsync(conversation.Value, request, aiResponse, ct);

            activity?.SetTag("message.id", message.Id.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return message.ToDto();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }

    private async Task<Result<Conversation>> GetConversationAsync(ConversationId id, CancellationToken ct)
    {
        using var activity = AxonActivitySource.Chat.StartActivity("Chat.GetConversation");
        activity?.SetTag("conversation.id", id.ToString());
        
        var result = await _repository.GetByIdAsync(id, ct);
        activity?.SetTag("conversation.found", result != null);
        
        return result != null ? result : Error.NotFound("Conversation not found");
    }

    private async Task<string> ProcessWithAiAsync(string content, CancellationToken ct)
    {
        using var activity = AxonActivitySource.Chat.StartActivity("Chat.ProcessWithAI");
        activity?.SetTag("ai.provider", "openai");
        activity?.SetTag("content.length", content.Length);
        
        var response = await _aiClient.ProcessAsync(content, ct);
        
        activity?.SetTag("response.length", response.Length);
        return response;
    }

    private async Task<Message> SaveMessageAsync(Conversation conversation, SendMessageCommand request, string aiResponse, CancellationToken ct)
    {
        using var activity = AxonActivitySource.Chat.StartActivity("Chat.SaveMessage");
        
        var message = conversation.AddMessage(request.Content, request.UserId, aiResponse);
        await _repository.SaveAsync(conversation, ct);
        
        activity?.SetTag("message.saved", true);
        return message;
    }
}
```

#### Query Handler Instrumentation

```csharp
// src/Modules/Chat/Application/Queries/GetConversation/GetConversationHandler.cs
using System.Diagnostics;
using MediatR;
using Axon.Shared.Common.Observability;

public sealed class GetConversationHandler : IRequestHandler<GetConversationQuery, Result<ConversationDto>>
{
    private readonly IChatRepository _repository;

    public GetConversationHandler(IChatRepository repository) => _repository = repository;

    public async Task<Result<ConversationDto>> Handle(GetConversationQuery request, CancellationToken ct)
    {
        using var activity = AxonActivitySource.Chat.StartActivity("Chat.GetConversation");
        
        activity?.SetTag("query.type", nameof(GetConversationQuery));
        activity?.SetTag("conversation.id", request.Id.ToString());
        activity?.SetTag("user.id", request.UserId.ToString());

        try
        {
            var conversation = await _repository.GetByIdAsync(request.Id, ct);
            
            if (conversation == null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Conversation not found");
                return Error.NotFound("Conversation not found");
            }

            // Security check with tracing
            if (!conversation.HasAccess(request.UserId))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Access denied");
                activity?.SetTag("security.access_denied", true);
                return Error.Forbidden("Access denied");
            }

            activity?.SetTag("conversation.message_count", conversation.Messages.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return conversation.ToDto();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

### 3. MediatR Pipeline Behavior

```csharp
// src/Shared/Common/Behaviors/TracingBehavior.cs
using System.Diagnostics;
using MediatR;
using Axon.Shared.Common.Observability;

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var isCommand = requestName.EndsWith("Command");
        var operationName = isCommand ? $"Command.{requestName}" : $"Query.{requestName}";

        using var activity = AxonActivitySource.Instance.StartActivity(operationName);
        
        activity?.SetTag("mediatr.request_type", requestName);
        activity?.SetTag("mediatr.operation_type", isCommand ? "command" : "query");
        
        // Add request properties if data is requested
        if (activity?.IsAllDataRequested == true)
        {
            AddRequestTags(activity, request);
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            // Add response metadata
            AddResponseTags(activity, response);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }

    private static void AddRequestTags(Activity activity, TRequest request)
    {
        // Use reflection carefully for performance
        var properties = typeof(TRequest).GetProperties()
            .Where(p => p.CanRead && IsSimpleType(p.PropertyType))
            .Take(10); // Limit to prevent performance issues

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(request);
                if (value != null)
                {
                    activity.SetTag($"request.{prop.Name.ToLowerInvariant()}", value.ToString());
                }
            }
            catch
            {
                // Ignore reflection errors
            }
        }
    }

    private static void AddResponseTags(Activity? activity, TResponse response)
    {
        if (activity == null) return;

        // Handle Result<T> pattern
        if (response?.GetType().IsGenericType == true && 
            response.GetType().GetGenericTypeDefinition() == typeof(Result<>))
        {
            var isSuccess = (bool)(response.GetType().GetProperty("IsSuccess")?.GetValue(response) ?? false);
            activity.SetTag("result.success", isSuccess);
            
            if (!isSuccess)
            {
                var error = response.GetType().GetProperty("Error")?.GetValue(response);
                activity.SetTag("result.error", error?.ToString());
            }
        }
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive || 
        type == typeof(string) || 
        type == typeof(Guid) || 
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        Nullable.GetUnderlyingType(type) != null;
}
```

### 4. ASP.NET Core Integration

#### Endpoint Instrumentation

```csharp
// src/Api/Endpoints/Chat/SendMessageEndpoint.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Axon.Shared.Common.Observability;

public static class SendMessageEndpoint
{
    public static void MapSendMessage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat/conversations/{conversationId}/messages", SendMessageAsync)
           .WithName("SendMessage")
           .WithTags("Chat")
           .WithOpenApi();
    }

    private static async Task<IResult> SendMessageAsync(
        [FromRoute] Guid conversationId,
        [FromBody] SendMessageRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        using var activity = AxonActivitySource.Api.StartActivity("API.SendMessage");
        
        // HTTP context is automatically propagated
        activity?.SetTag("http.method", "POST");
        activity?.SetTag("http.endpoint", "/api/chat/conversations/{conversationId}/messages");
        activity?.SetTag("conversation.id", conversationId.ToString());
        
        // Add baggage for cross-service correlation
        Activity.Current?.AddBaggage("user.session", HttpContext.Current?.Session?.Id ?? "unknown");
        Activity.Current?.AddBaggage("api.version", "v1");

        var command = new SendMessageCommand(
            ConversationId.Create(conversationId).Value,
            request.Content,
            request.UserId
        );

        var result = await mediator.Send(command, ct);

        return result.IsSuccess 
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}
```

#### Custom Middleware for Activity Enhancement

```csharp
// src/Api/Middleware/ActivityEnrichmentMiddleware.cs
using System.Diagnostics;

public sealed class ActivityEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public ActivityEnrichmentMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        
        if (activity != null)
        {
            // Enrich with HTTP context information
            activity.SetTag("http.url", context.Request.GetDisplayUrl());
            activity.SetTag("http.user_agent", context.Request.Headers.UserAgent.ToString());
            activity.SetTag("http.remote_ip", context.Connection.RemoteIpAddress?.ToString());
            
            // Add user context if authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                activity.SetTag("user.id", context.User.FindFirst("sub")?.Value);
                activity.SetTag("user.authenticated", true);
                
                // Add to baggage for downstream services
                activity.AddBaggage("user.id", context.User.FindFirst("sub")?.Value ?? "unknown");
            }

            // Track response after processing
            context.Response.OnStarting(() =>
            {
                activity.SetTag("http.status_code", context.Response.StatusCode);
                activity.SetTag("http.status_text", ReasonPhrases.GetReasonPhrase(context.Response.StatusCode));
                
                if (context.Response.StatusCode >= 400)
                {
                    activity.SetStatus(ActivityStatusCode.Error, $"HTTP {context.Response.StatusCode}");
                }
                
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}
```

### 5. Dependency Injection Setup

```csharp
// src/Api/Extensions/ObservabilityExtensions.cs
using System.Diagnostics;
using OpenTelemetry.Trace;
using Axon.Shared.Common.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Activity defaults
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        // Register tracing behavior
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));

        // Configure OpenTelemetry (optional - for export to collectors)
        if (configuration.GetValue<bool>("Observability:OpenTelemetry:Enabled"))
        {
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .AddSource(AxonActivitySource.Instance.Name)
                        .AddSource(AxonActivitySource.Chat.Name)
                        .AddSource(AxonActivitySource.Api.Name)
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.EnrichWithHttpRequest = (activity, request) =>
                            {
                                activity.SetTag("http.request.body.size", request.ContentLength ?? 0);
                            };
                            options.EnrichWithHttpResponse = (activity, request) =>
                            {
                                activity.SetTag("http.response.body.size", request.ContentLength ?? 0);
                            };
                        })
                        .AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })
                        .AddEntityFrameworkCoreInstrumentation(options =>
                        {
                            options.SetDbStatementForText = true;
                            options.SetDbStatementForStoredProcedure = true;
                        });

                    // Configure exporters based on environment
                    var otlpEndpoint = configuration.GetValue<string>("Observability:OpenTelemetry:OtlpEndpoint");
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        builder.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                    else
                    {
                        builder.AddConsoleExporter(); // Development fallback
                    }
                });
        }
        else
        {
            // Simple console listener for development
            services.AddSingleton<ActivityListener>(provider =>
            {
                var listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name.StartsWith("Axon.Backend"),
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                    ActivityStarted = activity => Console.WriteLine($"Started: {activity.DisplayName}"),
                    ActivityStopped = activity => Console.WriteLine($"Stopped: {activity.DisplayName} ({activity.Duration.TotalMilliseconds}ms)")
                };
                
                ActivitySource.AddActivityListener(listener);
                return listener;
            });
        }

        return services;
    }

    public static IApplicationBuilder UseActivityEnrichment(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ActivityEnrichmentMiddleware>();
    }
}
```

### 6. Application Startup Configuration

```csharp
// src/Api/Program.cs
using Axon.Shared.Common.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add observability early in the pipeline
builder.Services.AddObservability(builder.Configuration);

// Other service registrations...
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

// Use activity enrichment early in middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseActivityEnrichment();
}

// Standard middleware pipeline...
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints...
app.MapChatEndpoints();

// Ensure proper cleanup
app.Lifetime.ApplicationStopping.Register(AxonActivitySource.Dispose);

app.Run();
```

## Advanced Patterns

### 1. Baggage for Cross-Service Context

```csharp
// Adding contextual information that flows across service boundaries
Activity.Current?.AddBaggage("tenant.id", tenantId);
Activity.Current?.AddBaggage("feature.flag", "chat_v2_enabled");
Activity.Current?.AddBaggage("user.role", userRole);

// Accessing baggage in downstream services
var tenantId = Activity.Current?.GetBaggageItem("tenant.id");
var featureFlag = Activity.Current?.GetBaggageItem("feature.flag");
```

### 2. Custom Activity Extensions

```csharp
// src/Shared/Common/Observability/ActivityExtensions.cs
using System.Diagnostics;

public static class ActivityExtensions
{
    public static Activity? RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null) return activity;
        
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stack_trace", exception.StackTrace);
        
        if (exception.InnerException != null)
        {
            activity.SetTag("exception.inner.type", exception.InnerException.GetType().FullName);
            activity.SetTag("exception.inner.message", exception.InnerException.Message);
        }
        
        return activity;
    }

    public static Activity? SetUserContext(this Activity? activity, string userId, string? role = null)
    {
        if (activity == null) return activity;
        
        activity.SetTag("user.id", userId);
        if (role != null)
        {
            activity.SetTag("user.role", role);
        }
        
        return activity;
    }

    public static Activity? SetBusinessContext(this Activity? activity, string module, string feature, string? operation = null)
    {
        if (activity == null) return activity;
        
        activity.SetTag("business.module", module);
        activity.SetTag("business.feature", feature);
        if (operation != null)
        {
            activity.SetTag("business.operation", operation);
        }
        
        return activity;
    }
}
```

### 3. Sampling Strategies

```csharp
// src/Api/Configuration/TracingSampler.cs
using System.Diagnostics;

public sealed class BusinessLogicSampler
{
    public static ActivitySamplingResult SampleActivity(ref ActivityCreationOptions<ActivityContext> options)
    {
        // Always sample errors and important business operations
        if (options.Name.Contains("Error") || 
            options.Name.Contains("Payment") || 
            options.Name.Contains("Auth"))
        {
            return ActivitySamplingResult.AllData;
        }

        // Sample health checks and metrics less frequently
        if (options.Name.Contains("Health") || 
            options.Name.Contains("Metrics"))
        {
            return Random.Shared.Next(100) < 1 ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
        }

        // Default sampling based on parent or 10%
        if (options.Parent?.TraceFlags.HasFlag(ActivityTraceFlags.Recorded) == true)
        {
            return ActivitySamplingResult.AllData;
        }

        return Random.Shared.Next(100) < 10 ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
    }
}
```

## Performance Considerations

### 1. Conditional Activity Creation

```csharp
// Only create activities when listeners are present
using var activity = AxonActivitySource.Instance.StartActivity("ExpensiveOperation");
if (activity != null)
{
    // Only add expensive tags if activity was created
    activity.SetTag("expensive.computation", ComputeExpensiveValue());
}
```

### 2. Smart Tag Population

```csharp
// Use IsAllDataRequested to avoid expensive tag computation
if (activity?.IsAllDataRequested == true)
{
    activity.SetTag("detailed.analysis", await PerformDetailedAnalysis());
    activity.SetTag("full.context", SerializeFullContext());
}
else
{
    activity?.SetTag("operation.id", operationId);
}
```

### 3. Avoid High-Cardinality Tags

```csharp
// ❌ BAD - High cardinality
activity?.SetTag("user.id", userId); // Could be millions of values
activity?.SetTag("timestamp", DateTime.Now.ToString()); // Always unique

// ✅ GOOD - Low cardinality  
activity?.SetTag("user.role", userRole); // Limited set of values
activity?.SetTag("operation.type", "read"); // Known set of operations
```

## Configuration

### appsettings.json

```json
{
  "Observability": {
    "OpenTelemetry": {
      "Enabled": true,
      "OtlpEndpoint": "http://localhost:4317",
      "ServiceName": "axon-backend",
      "ServiceVersion": "1.0.0"
    },
    "ActivitySources": [
      "Axon.Backend",
      "Axon.Backend.Chat",
      "Axon.Backend.Api"
    ],
    "Sampling": {
      "DefaultRate": 0.1,
      "AlwaysSample": [
        "*Error*",
        "*Auth*",
        "*Payment*"
      ],
      "NeverSample": [
        "*Health*"
      ]
    }
  }
}
```

## Integration with External Systems

### 1. HTTP Client Instrumentation

```csharp
// HTTP clients automatically propagate trace context
services.AddHttpClient<IAiClient, OpenAiClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://api.openai.com/v1/");
    });
```

### 2. Entity Framework Core

```csharp
// EF Core automatically creates activities for database operations
// No additional code needed - just ensure instrumentation is added in ObservabilityExtensions
```

### 3. Message Queue Integration (Example with Azure Service Bus)

```csharp
public async Task SendMessageAsync<T>(T message) where T : class
{
    using var activity = AxonActivitySource.Instance.StartActivity("ServiceBus.Send");
    
    activity?.SetTag("messaging.system", "servicebus");
    activity?.SetTag("messaging.destination", topicName);
    activity?.SetTag("messaging.message_type", typeof(T).Name);

    // Inject trace context into message properties
    var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message));
    
    if (Activity.Current != null)
    {
        serviceBusMessage.ApplicationProperties["traceparent"] = Activity.Current.Id;
        serviceBusMessage.ApplicationProperties["tracestate"] = Activity.Current.TraceStateString;
        
        // Add baggage
        foreach (var baggageItem in Activity.Current.Baggage)
        {
            serviceBusMessage.ApplicationProperties[$"baggage-{baggageItem.Key}"] = baggageItem.Value;
        }
    }

    await sender.SendMessageAsync(serviceBusMessage);
}
```

## Testing Strategies

### 1. Unit Testing with Activities

```csharp
// tests/Unit/Chat/SendMessageHandlerTests.cs
using System.Diagnostics;
using Xunit;

public class SendMessageHandlerTests
{
    [Fact]
    public async Task Handle_Should_CreateActivity_When_ProcessingCommand()
    {
        // Arrange
        var activities = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Axon.Backend"),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => activities.Add(activity)
        };
        
        ActivitySource.AddActivityListener(listener);

        var handler = new SendMessageHandler(mockRepository, mockAiClient);
        var command = new SendMessageCommand(conversationId, "test message", userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Single(activities);
        Assert.Equal("Chat.SendMessage", activities[0].DisplayName);
        Assert.Contains(activities[0].Tags, tag => tag.Key == "command.type" && tag.Value == "SendMessageCommand");
        
        listener.Dispose();
    }
}
```

### 2. Integration Testing

```csharp
// tests/Integration/ActivityPropagationTests.cs
[Fact]
public async Task SendMessage_Should_PropagateTraceContext_AcrossLayers()
{
    // Arrange
    using var rootActivity = AxonActivitySource.Instance.StartActivity("Test.Root");
    var traceId = rootActivity?.TraceId;

    // Act
    var response = await client.PostAsync("/api/chat/conversations/123/messages", 
        new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

    // Assert
    response.Headers.Should().ContainKey("traceparent");
    var responseTraceId = ExtractTraceIdFromHeaders(response.Headers);
    responseTraceId.Should().Be(traceId);
}
```

## Troubleshooting

### Common Issues

1. **Activities not being created**
   - Ensure ActivitySource is registered with listeners
   - Check that source names match between creation and listener registration

2. **Performance degradation**
   - Use `IsAllDataRequested` for expensive tag computation
   - Implement proper sampling strategies
   - Avoid high-cardinality tags

3. **Missing trace context in HTTP calls**
   - Verify HttpClient instrumentation is enabled
   - Check for custom headers overriding trace context

4. **Baggage not propagating**
   - Ensure baggage is added before HTTP calls
   - Verify receiving service is reading baggage correctly

### Debug Configuration

```csharp
// Add to Program.cs for debugging
if (builder.Environment.IsDevelopment())
{
    var debugListener = new ActivityListener
    {
        ShouldListenTo = source => source.Name.StartsWith("Axon.Backend"),
        Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        ActivityStarted = activity =>
        {
            Console.WriteLine($"🟢 Started: {activity.DisplayName} | TraceId: {activity.TraceId} | SpanId: {activity.SpanId}");
            foreach (var tag in activity.Tags)
            {
                Console.WriteLine($"   📝 {tag.Key}: {tag.Value}");
            }
            foreach (var baggage in activity.Baggage)
            {
                Console.WriteLine($"   🧳 {baggage.Key}: {baggage.Value}");
            }
        },
        ActivityStopped = activity =>
        {
            Console.WriteLine($"🔴 Stopped: {activity.DisplayName} | Duration: {activity.Duration.TotalMilliseconds}ms | Status: {activity.Status}");
        }
    };
    
    ActivitySource.AddActivityListener(debugListener);
}
```

## Best Practices Summary

1. **Use static ActivitySource instances** - Create once, use throughout application lifecycle
2. **Name sources hierarchically** - Use assembly name + component for uniqueness
3. **Implement conditional tagging** - Use `IsAllDataRequested` for performance
4. **Avoid high-cardinality attributes** - Keep tag values to a reasonable set
5. **Use semantic conventions** - Follow OpenTelemetry standards for tag names
6. **Implement proper sampling** - Balance observability needs with performance
7. **Test trace propagation** - Verify context flows across service boundaries
8. **Monitor instrumentation overhead** - Measure impact on application performance
9. **Use baggage sparingly** - Only for essential cross-service context
10. **Handle exceptions in activities** - Always record failures and errors

## Apply vs Not-Apply for Axon Backend

### ✅ **APPLY IMMEDIATELY**
- **Built-in .NET 10 support** - No package dependencies
- **W3C TraceContext compliance** - Industry standard
- **Automatic HTTP propagation** - Works with ASP.NET Core and HttpClient
- **Performance optimized** - Smart listener patterns and conditional creation
- **CQRS integration** - Perfect for command/query tracing
- **OpenTelemetry compatible** - Export to any observability platform

### ⚠️ **APPLY WITH CONSIDERATION**
- **OpenTelemetry packages** - Only if external collectors needed
- **Custom sampling logic** - Implement based on actual usage patterns
- **Baggage usage** - Use sparingly to avoid performance impact

### 🔄 **MONITOR AND ADJUST**
- **Tag cardinality** - Monitor for performance impact
- **Sampling rates** - Adjust based on volume and storage costs
- **Activity creation overhead** - Measure impact in high-throughput scenarios

## Confidence Assessment: **High**

**Primary Source Evidence:**
- Built into .NET 10 - no compatibility concerns
- Extensive Microsoft Learn documentation
- W3C TraceContext compliance by default
- Proven integration with ASP.NET Core and OpenTelemetry

**Implementation Readiness:** **Immediate** - Can be implemented without external dependencies or compatibility concerns.