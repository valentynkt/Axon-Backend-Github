# Story 01: W3C TraceContext Integration with Metadata and Declarative Caching

**Story ID:** AXON-CQRS-001  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P0 - Critical Foundation  
**Estimated Effort:** 4 hours  
**Dependencies:** Epic_01 (Functional Foundation)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer working with the Axon CQRS system,  
**I want** enhanced command and query contracts with W3C trace context integration, metadata support, and declarative caching,  
**So that** I can leverage standard distributed tracing, attach contextual data, and declaratively configure query caching with minimal code overhead.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - Basic `ICommand<T>` and `IQuery<T>` interfaces exist
  - `RequestBase` provides `RequestId` and `RequestedAt` properties
  - OpenTelemetry infrastructure with Activity support in place
  - No correlation tracking or metadata support
  - No declarative caching properties

- **Integration Points:**
  - Existing MediatR pipeline behaviors
  - OpenTelemetry instrumentation
  - System.Diagnostics.Activity for W3C compliance
  - Result<T> pattern from Epic_01

- **Technology Stack:** 
  - .NET 10, C# 12
  - MediatR for CQRS
  - System.Diagnostics.Activity for W3C TraceContext
  - Immutable records pattern

- **Architectural Layer:** BuildingBlocks/Core/Abstractions/CQRS

### Patterns to Follow

```csharp
// Existing RequestBase pattern
public abstract record RequestBase : IAxonRequest
{
    public Guid RequestId { get; } = NewId.NextGuid();
    public DateTime RequestedAt { get; } = DateTime.UtcNow;
}

// W3C TraceContext via Activity
var activity = Activity.Current;
var traceId = activity?.TraceId.ToString();
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **W3C TraceContext Integration**
   - [ ] Commands/queries expose TraceId via `Activity.Current`
   - [ ] SpanId and ParentSpanId accessible through helper properties
   - [ ] No manual correlation ID generation required
   - [ ] Automatic propagation through async context

2. **Metadata Support**
   - [ ] All requests expose `IReadOnlyDictionary<string, object> Metadata`
   - [ ] Metadata is immutable after creation
   - [ ] Helper method `WithMetadata<T>()` for fluent additions
   - [ ] Support for tenant context, feature flags, etc.

3. **Declarative Query Caching**
   - [ ] Queries expose `bool UseCache` property (default: false)
   - [ ] Queries expose `TimeSpan? CacheDuration` property
   - [ ] Queries provide `string CacheKeyPrefix` property
   - [ ] Helper method `AsCached<T>()` for dynamic cache enablement

### Non-Functional Requirements

1. **Performance**
   - [ ] Zero allocations for W3C property access
   - [ ] Lazy evaluation of trace strings
   - [ ] Metadata copy-on-write semantics

2. **Compatibility**
   - [ ] Existing handlers continue working without modification
   - [ ] All contracts remain immutable records
   - [ ] No breaking changes to public APIs

3. **Observability**
   - [ ] Full integration with OpenTelemetry
   - [ ] Activity tags support for metadata
   - [ ] Trace propagation across HTTP boundaries

---

## 🔧 Technical Implementation

### Files to Modify

```yaml
Core_Interfaces:
  - src/BuildingBlocks/Core/Abstractions/CQRS/IRequest.cs
  - src/BuildingBlocks/Core/Abstractions/CQRS/ICommand.cs
  - src/BuildingBlocks/Core/Abstractions/CQRS/IQuery.cs

Base_Classes:
  - src/BuildingBlocks/Core/Abstractions/CQRS/RequestBase.cs
  - src/BuildingBlocks/Core/Abstractions/CQRS/CommandBase.cs
  - src/BuildingBlocks/Core/Abstractions/CQRS/QueryBase.cs
  - src/BuildingBlocks/Core/Abstractions/Pagination/PageQueryBase.cs

Tests:
  - tests/BuildingBlocks.Tests/Core/CQRS/W3CTraceContextTests.cs
  - tests/BuildingBlocks.Tests/Core/CQRS/MetadataTests.cs
  - tests/BuildingBlocks.Tests/Core/CQRS/CachingPropertiesTests.cs
```

### Implementation Steps

#### Step 1: Enhance IAxonRequest with W3C Support

```csharp
public interface IAxonRequest
{
    Guid RequestId { get; }
    DateTime RequestedAt { get; }
    
    // NEW: W3C TraceContext helpers
    string? TraceId => Activity.Current?.TraceId.ToString();
    string? SpanId => Activity.Current?.SpanId.ToString();
    string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();
    
    // NEW: Metadata support
    IReadOnlyDictionary<string, object> Metadata { get; }
}
```

#### Step 2: Update RequestBase with Metadata

```csharp
public abstract record RequestBase : IAxonRequest
{
    public Guid RequestId { get; } = NewId.NextGuid();
    public DateTime RequestedAt { get; } = DateTime.UtcNow;
    
    // W3C properties via interface default implementation
    
    // NEW: Metadata with init-only setter
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    
    // NEW: Helper for metadata addition
    public T WithMetadata<T>(string key, object value) where T : RequestBase
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = new ReadOnlyDictionary<string, object>(newMetadata) };
    }
}
```

#### Step 3: Add Caching to IQuery

```csharp
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, 
    MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    // NEW: Declarative caching
    bool UseCache { get; }
    TimeSpan? CacheDuration { get; }
    string CacheKeyPrefix => GetType().Name;
}
```

#### Step 4: Implement QueryBase Caching

```csharp
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, 
    IQuery<TResponse>
    where TResponse : notnull
{
    // NEW: Caching properties with defaults
    public virtual bool UseCache { get; init; } = false;
    public virtual TimeSpan? CacheDuration { get; init; } = null;
    public virtual string CacheKeyPrefix => GetType().Name;
    
    // NEW: Helper for cache enablement
    public T AsCached<T>(TimeSpan? duration = null) where T : QueryBase<TResponse>
    {
        return this with { UseCache = true, CacheDuration = duration };
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public void Should_Access_W3C_TraceContext_From_Current_Activity()
{
    // Arrange
    using var activity = new Activity("Test").Start();
    
    // Act
    var command = new TestCommand();
    
    // Assert
    command.TraceId.Should().Be(activity.TraceId.ToString());
    command.SpanId.Should().Be(activity.SpanId.ToString());
}

[Fact]
public void Should_Support_Metadata_Immutability()
{
    // Arrange
    var query = new TestQuery();
    
    // Act
    var enhanced = query.WithMetadata<TestQuery>("TenantId", "123");
    
    // Assert
    query.Metadata.Should().BeEmpty();
    enhanced.Metadata["TenantId"].Should().Be("123");
}

[Fact]
public void Should_Have_Caching_Disabled_By_Default()
{
    // Arrange & Act
    var query = new TestQuery();
    
    // Assert
    query.UseCache.Should().BeFalse();
    query.CacheDuration.Should().BeNull();
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Propagate_TraceContext_Through_Pipeline()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    services.AddOpenTelemetry()
        .WithTracing(builder => builder.AddAspNetCoreInstrumentation());
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    // Act
    using var activity = new Activity("Test").Start();
    var command = new CreateUserCommand("test@example.com");
    var result = await mediator.Send(command);
    
    // Assert
    // Verify trace context was available in handler
    result.Value.TraceId.Should().Be(activity.TraceId.ToString());
}
```

---

## 📐 Architecture Considerations

### Why W3C Over Manual Correlation?

1. **Standards Compliance**: Compatible with all W3C-compliant systems
2. **Zero Manual Code**: Automatic propagation via AsyncLocal
3. **Tool Integration**: Works with Jaeger, Zipkin, Application Insights
4. **HTTP Headers**: Automatic `traceparent` and `tracestate` propagation

### Performance Impact

- **Activity.Current**: Thread-static with AsyncLocal - negligible overhead
- **Metadata**: Copy-on-write pattern minimizes allocations
- **Caching Properties**: No runtime overhead, declarative only

### Security Considerations

```csharp
// DO: Non-sensitive metadata
command.WithMetadata("Feature", "beta");

// DON'T: Sensitive data in metadata
command.WithMetadata("Password", secret); // WRONG!
```

---

## 📦 Definition of Done

- [ ] All interfaces updated with new properties
- [ ] Base classes implement W3C helpers and metadata
- [ ] Query caching properties implemented
- [ ] Unit tests achieve 100% coverage
- [ ] Integration tests verify Activity propagation
- [ ] No breaking changes to existing handlers
- [ ] Documentation updated in Epic04_Story001_Implementation.md
- [ ] Code review approved
- [ ] No compiler warnings or analyzer violations

---

## 🔄 Migration Strategy

### Phase 1: Add Properties (Non-Breaking)
- Update interfaces and base classes
- All existing code continues to work

### Phase 2: Adopt in Pipeline Behaviors
- Update logging to use TraceId
- Implement caching behavior
- Add observability tags

### Phase 3: Gradual Feature Adoption
```csharp
// Existing code works
var query = new GetUserQuery(id);

// Opt-in to new features
var cached = query.AsCached<GetUserQuery>(TimeSpan.FromMinutes(5));
var tracked = query.WithMetadata<GetUserQuery>("TenantId", tenantId);
```

---

## 📊 Success Metrics

- Zero breaking changes reported
- 100% of new handlers use W3C trace context
- Cache hit rate > 60% for enabled queries
- Trace propagation success rate > 99.9%
- No performance degradation in handler execution

---

## 🚀 Follow-up Stories

1. **Story 02**: Pipeline Behavior for Caching Implementation
2. **Story 03**: Observability Behavior with W3C Integration
3. **Story 04**: Metadata Validation and Security Rules
4. **Story 05**: Cache Key Generation Strategy