# Epic 04 - Story 001: Enhanced CQRS Contracts Implementation Guide

## Overview

This document provides detailed implementation guidance for enhancing the CQRS contracts with W3C TraceContext correlation, metadata support, and declarative query caching.

## Architecture Context

### W3C TraceContext Standard

The W3C TraceContext specification defines standard HTTP headers and formats for distributed tracing:

- **traceparent**: Contains trace-id, parent-id, and trace-flags
- **tracestate**: Vendor-specific trace data

In .NET, this is implemented via `System.Diagnostics.Activity`:

```csharp
// Activity automatically propagates through async context
var activity = Activity.Current;
var traceId = activity?.TraceId.ToString();  // 128-bit trace identifier
var spanId = activity?.SpanId.ToString();     // 64-bit span identifier
```

### Why W3C Over Manual Correlation IDs?

1. **Standards Compliance**: Works with any W3C-compliant system
2. **Automatic Propagation**: Flows through async/await automatically
3. **Zero Code**: No manual ID generation or passing
4. **Tool Support**: Works with Jaeger, Zipkin, Application Insights, etc.
5. **HTTP Integration**: Automatic header propagation in HTTP calls

## Detailed Implementation

### 1. IAxonRequest Enhancement

**File**: `src/BuildingBlocks/Core/Abstractions/CQRS/IRequest.cs`

```csharp
using System.Diagnostics;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base interface for all Axon requests (commands and queries).
/// Provides W3C TraceContext integration and metadata support.
/// </summary>
public interface IAxonRequest
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// </summary>
    Guid RequestId { get; }
    
    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    DateTime RequestedAt { get; }
    
    /// <summary>
    /// W3C Trace ID from current Activity context.
    /// Returns null if no activity is active.
    /// </summary>
    string? TraceId => Activity.Current?.TraceId.ToString();
    
    /// <summary>
    /// W3C Span ID from current Activity context.
    /// Each request typically creates its own span.
    /// </summary>
    string? SpanId => Activity.Current?.SpanId.ToString();
    
    /// <summary>
    /// Parent Span ID for distributed trace correlation.
    /// </summary>
    string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();
    
    /// <summary>
    /// Extensible metadata for request context.
    /// Use for non-functional concerns like tenant ID, user context, feature flags.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Generic request interface with response type.
/// </summary>
public interface IAxonRequest<TResponse> : IAxonRequest where TResponse : notnull
{
}
```

### 2. RequestBase Implementation

**File**: `src/BuildingBlocks/Core/Abstractions/CQRS/RequestBase.cs`

```csharp
using System.Collections.ObjectModel;
using System.Diagnostics;
using MassTransit;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base record for all CQRS requests.
/// Provides W3C TraceContext helpers and metadata support.
/// </summary>
public abstract record RequestBase : IAxonRequest
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// </summary>
    public Guid RequestId { get; } = NewId.NextGuid();
    
    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAt { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// W3C Trace ID from current Activity context.
    /// </summary>
    public string? TraceId => Activity.Current?.TraceId.ToString();
    
    /// <summary>
    /// W3C Span ID from current Activity context.
    /// </summary>
    public string? SpanId => Activity.Current?.SpanId.ToString();
    
    /// <summary>
    /// Parent Span ID for distributed trace correlation.
    /// </summary>
    public string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();
    
    /// <summary>
    /// Extensible metadata for request context.
    /// Initialized as empty read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    
    /// <summary>
    /// Helper method to create a new instance with additional metadata.
    /// </summary>
    public T WithMetadata<T>(string key, object value) where T : RequestBase
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = new ReadOnlyDictionary<string, object>(newMetadata) };
    }
}

/// <summary>
/// Generic base record for requests with response type.
/// </summary>
public abstract record RequestBase<TResponse> : RequestBase, IAxonRequest<TResponse> 
    where TResponse : notnull
{
}
```

### 3. Enhanced Query Interface

**File**: `src/BuildingBlocks/Core/Abstractions/CQRS/IQuery.cs`

```csharp
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for queries with declarative caching support.
/// Queries represent read operations that don't modify state.
/// </summary>
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    /// <summary>
    /// Indicates whether this query result should be cached.
    /// Default is false for data consistency.
    /// </summary>
    bool UseCache { get; }
    
    /// <summary>
    /// Duration for which the query result should be cached.
    /// Null means use default cache duration from configuration.
    /// </summary>
    TimeSpan? CacheDuration { get; }
    
    /// <summary>
    /// Cache key prefix for this query type.
    /// Defaults to query type name.
    /// </summary>
    string CacheKeyPrefix => GetType().Name;
}
```

### 4. QueryBase with Caching

**File**: `src/BuildingBlocks/Core/Abstractions/CQRS/QueryBase.cs`

```csharp
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base record for queries with caching configuration.
/// </summary>
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, IQuery<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Indicates whether this query result should be cached.
    /// Override in derived classes to enable caching.
    /// </summary>
    public virtual bool UseCache { get; init; } = false;
    
    /// <summary>
    /// Duration for which the query result should be cached.
    /// Set to specific duration or leave null for default.
    /// </summary>
    public virtual TimeSpan? CacheDuration { get; init; } = null;
    
    /// <summary>
    /// Cache key prefix for this query type.
    /// Override to customize cache key generation.
    /// </summary>
    public virtual string CacheKeyPrefix => GetType().Name;
    
    /// <summary>
    /// Helper to create a cached version of this query.
    /// </summary>
    public T AsCached<T>(TimeSpan? duration = null) where T : QueryBase<TResponse>
    {
        return this with { UseCache = true, CacheDuration = duration };
    }
}
```

### 5. Command Implementation (No Changes Needed)

**File**: `src/BuildingBlocks/Core/Abstractions/CQRS/CommandBase.cs`

```csharp
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base record for commands without response.
/// Commands should not be cached as they modify state.
/// </summary>
public abstract record CommandBase : RequestBase<Result<Unit>>, ICommand
{
    // No caching properties - commands modify state
}

/// <summary>
/// Base record for commands with response.
/// </summary>
public abstract record CommandBase<TResponse> : RequestBase<Result<TResponse>>, ICommand<TResponse>
    where TResponse : notnull
{
    // No caching properties - commands modify state
}
```

## Usage Examples

### 1. Basic Query with W3C Tracing

```csharp
public record GetUserByIdQuery(UserId Id) : QueryBase<UserDto>
{
    // Trace context is automatically available
}

// Handler
public class GetUserByIdHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        // Access trace context if needed
        _logger.LogInformation("Processing query {QueryId} in trace {TraceId}", 
            query.RequestId, query.TraceId);
        
        // Business logic here
        return Result<UserDto>.Success(userDto);
    }
}
```

### 2. Cached Query

```csharp
public record GetProductListQuery : QueryBase<List<ProductDto>>
{
    // Enable caching with 5-minute duration
    public override bool UseCache => true;
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

// Or dynamically
var query = new GetProductListQuery().AsCached<GetProductListQuery>(TimeSpan.FromMinutes(10));
```

### 3. Command with Metadata

```csharp
public record CreateOrderCommand(OrderDetails Details) : CommandBase<OrderId>;

// Usage with metadata
var command = new CreateOrderCommand(details)
    .WithMetadata<CreateOrderCommand>("TenantId", tenantId)
    .WithMetadata<CreateOrderCommand>("FeatureFlag", "new-pricing");

// In handler
public async Task<Result<OrderId>> Handle(CreateOrderCommand command, CancellationToken ct)
{
    if (command.Metadata.TryGetValue("TenantId", out var tenantId))
    {
        // Use tenant context
    }
    
    // Trace is automatic
    _telemetry.TrackEvent("OrderCreated", new Dictionary<string, string>
    {
        ["TraceId"] = command.TraceId ?? "unknown",
        ["SpanId"] = command.SpanId ?? "unknown"
    });
}
```

## Pipeline Behavior Integration

### Caching Behavior Example

```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : notnull
{
    private readonly IDistributedCache _cache;
    
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!request.UseCache)
            return await next();
        
        // Generate cache key using trace context for isolation
        var cacheKey = $"{request.CacheKeyPrefix}:{request.GetHashCode()}:{request.TraceId}";
        
        // Try get from cache
        var cached = await _cache.GetAsync<TResponse>(cacheKey, ct);
        if (cached != null)
            return cached;
        
        // Execute and cache
        var response = await next();
        
        var duration = request.CacheDuration ?? TimeSpan.FromMinutes(5);
        await _cache.SetAsync(cacheKey, response, duration, ct);
        
        return response;
    }
}
```

### Observability Behavior

```csharp
public class ObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Activity is already started by ASP.NET Core or previous behavior
        var activity = Activity.Current;
        
        // Add request metadata as tags
        foreach (var (key, value) in request.Metadata)
        {
            activity?.SetTag($"request.metadata.{key}", value);
        }
        
        // Add request type
        activity?.SetTag("request.type", request.GetType().Name);
        activity?.SetTag("request.id", request.RequestId);
        
        return await next();
    }
}
```

## Testing Strategy

### 1. Unit Test for W3C Integration

```csharp
[Fact]
public void Request_Should_Expose_Current_Activity_Context()
{
    // Arrange
    var activitySource = new ActivitySource("TestSource");
    using var activity = activitySource.StartActivity("TestOperation");
    
    // Act
    var query = new TestQuery();
    
    // Assert
    query.TraceId.Should().Be(activity.TraceId.ToString());
    query.SpanId.Should().Be(activity.SpanId.ToString());
}

[Fact]
public void Request_Should_Handle_No_Activity_Context()
{
    // Arrange - ensure no activity
    Activity.Current = null;
    
    // Act
    var query = new TestQuery();
    
    // Assert
    query.TraceId.Should().BeNull();
    query.SpanId.Should().BeNull();
}
```

### 2. Metadata Immutability Test

```csharp
[Fact]
public void Metadata_Should_Be_Immutable()
{
    // Arrange
    var metadata = new Dictionary<string, object> { ["key1"] = "value1" };
    var query = new TestQuery { Metadata = metadata.AsReadOnly() };
    
    // Act & Assert
    query.Metadata.Should().BeOfType<ReadOnlyDictionary<string, object>>();
    query.Invoking(q => ((IDictionary<string, object>)q.Metadata).Add("key2", "value2"))
        .Should().Throw<NotSupportedException>();
}
```

### 3. Caching Configuration Test

```csharp
[Fact]
public void Query_Should_Support_Caching_Configuration()
{
    // Arrange & Act
    var query = new CacheableQuery 
    { 
        UseCache = true, 
        CacheDuration = TimeSpan.FromMinutes(10) 
    };
    
    // Assert
    query.UseCache.Should().BeTrue();
    query.CacheDuration.Should().Be(TimeSpan.FromMinutes(10));
}

[Fact]
public void Query_Should_Have_Caching_Disabled_By_Default()
{
    // Arrange & Act
    var query = new NonCacheableQuery();
    
    // Assert
    query.UseCache.Should().BeFalse();
    query.CacheDuration.Should().BeNull();
}
```

## Migration Guide

### Step 1: Update Base Classes (Non-Breaking)

1. Update `IAxonRequest` with W3C helper properties
2. Add `Metadata` property to `RequestBase`
3. Add caching properties to `IQuery` and `QueryBase`

### Step 2: Update Pipeline Behaviors

1. Modify logging behavior to use `TraceId` instead of custom correlation
2. Implement caching behavior using declarative properties
3. Update telemetry to include W3C context

### Step 3: Gradual Feature Adoption

```csharp
// Phase 1: Existing queries continue to work
public record GetUserQuery(UserId Id) : QueryBase<UserDto>;

// Phase 2: Opt-in to caching
public record GetUserQuery(UserId Id) : QueryBase<UserDto>
{
    public override bool UseCache => true;
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

// Phase 3: Use metadata for context
var query = new GetUserQuery(userId)
    .WithMetadata<GetUserQuery>("TenantId", tenantId);
```

## Performance Considerations

### W3C TraceContext Performance

- **Activity.Current**: Thread-static with AsyncLocal flow - minimal overhead
- **No allocations**: Properties return existing Activity data
- **Lazy evaluation**: Trace strings only created when accessed

### Metadata Performance

- **Immutable**: ReadOnlyDictionary prevents modifications
- **Copy-on-write**: WithMetadata creates new instance only when needed
- **Small footprint**: Most requests won't use metadata

### Caching Performance

- **Declarative**: No runtime decisions in handlers
- **Pipeline-based**: Caching logic separated from business logic
- **Key generation**: Uses efficient hash codes

## Security Considerations

### Metadata Guidelines

```csharp
// DO: Use metadata for non-sensitive context
command.WithMetadata("TenantId", tenantId)
       .WithMetadata("Feature", "beta");

// DON'T: Put sensitive data in metadata
command.WithMetadata("Password", password)  // WRONG!
       .WithMetadata("CreditCard", ccNumber); // WRONG!
```

### Trace Context Security

- W3C headers are visible in HTTP traffic
- Don't include sensitive data in Activity tags
- Use sampling to reduce trace volume in production

## Monitoring & Observability

### Distributed Tracing

```csharp
// Traces automatically flow through the system
HTTP Request → API Controller → MediatR → Handler → Database
     └─ TraceId: 4bf92f3577b34da2a3ce929d0e0e4736
         ├─ SpanId: 00f067aa0bae4738 (Controller)
         ├─ SpanId: 1f3a5c8b2d4e6a7c (Command)
         └─ SpanId: 3e5a7c9d4f6b8e1a (Database)
```

### Metrics

```csharp
// Track cache hit rates
_metrics.RecordCacheHit(query.CacheKeyPrefix, query.TraceId);

// Track metadata usage
_metrics.RecordMetadataKeys(query.Metadata.Keys);
```

## Troubleshooting

### Issue: TraceId is null

**Cause**: No Activity in current context
**Solution**: Ensure OpenTelemetry is configured and Activity is started

```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
```

### Issue: Metadata changes don't persist

**Cause**: Records are immutable
**Solution**: Use `with` expression or `WithMetadata` helper

```csharp
// Wrong
query.Metadata["key"] = "value"; // Won't compile

// Right
var newQuery = query.WithMetadata<MyQuery>("key", "value");
```

### Issue: Cache not working

**Cause**: UseCache not set or cache not configured
**Solution**: Check query configuration and cache setup

```csharp
// Verify in query
public override bool UseCache => true;

// Verify in DI
services.AddStackExchangeRedisCache(options => ...);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

## References

- [W3C TraceContext Specification](https://www.w3.org/TR/trace-context/)
- [.NET Activity Class Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors)