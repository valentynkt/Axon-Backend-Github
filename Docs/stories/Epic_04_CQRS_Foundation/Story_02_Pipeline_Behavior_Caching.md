# Story 02: Caching Pipeline Behavior for Declarative Query Caching

**Story ID:** AXON-CQRS-002  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P1 - High  
**Estimated Effort:** 6 hours  
**Dependencies:** Story_01 (W3C TraceContext and Caching Properties)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer using the CQRS query system,  
**I want** a pipeline behavior that automatically caches query results based on declarative properties,  
**So that** I can improve performance without adding caching logic to individual handlers.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - Query contracts with `UseCache` and `CacheDuration` properties (from Story 01)
  - MediatR pipeline infrastructure in place
  - No automatic caching implementation
  - Manual caching in some handlers

- **Integration Points:**
  - MediatR IPipelineBehavior interface
  - IDistributedCache or IMemoryCache
  - Query caching properties from Story 01
  - W3C TraceContext for cache isolation

- **Technology Stack:** 
  - .NET 10, MediatR
  - Microsoft.Extensions.Caching
  - System.Text.Json for serialization
  - Result<T> pattern

- **Architectural Layer:** BuildingBlocks/Application/Behaviors

### Patterns to Follow

```csharp
// Existing pipeline behavior pattern
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Pre-processing
        // ...
        
        // Continue pipeline
        var response = await next();
        
        // Post-processing
        return response;
    }
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Cache Key Generation**
   - [ ] Generate deterministic cache keys from query properties
   - [ ] Include query type name in key
   - [ ] Support cache key prefix customization
   - [ ] Ensure key uniqueness across different queries

2. **Cache Operations**
   - [ ] Check cache before handler execution when `UseCache = true`
   - [ ] Store results after successful handler execution
   - [ ] Skip caching for failed results (IsFailure)
   - [ ] Honor `CacheDuration` or use default

3. **Cache Invalidation**
   - [ ] Support cache removal by key pattern
   - [ ] Provide cache statistics/metrics
   - [ ] Handle cache misses gracefully
   - [ ] Support cache warmup scenarios

4. **Serialization**
   - [ ] Serialize/deserialize query results correctly
   - [ ] Handle complex types and collections
   - [ ] Preserve Result<T> structure
   - [ ] Support polymorphic types

### Non-Functional Requirements

1. **Performance**
   - [ ] Minimal overhead when caching disabled
   - [ ] Efficient key generation (< 1ms)
   - [ ] Async cache operations
   - [ ] Connection pooling for distributed cache

2. **Reliability**
   - [ ] Handle cache failures without breaking queries
   - [ ] Fallback to handler on cache errors
   - [ ] Log cache hits/misses for monitoring
   - [ ] Circuit breaker for cache failures

3. **Observability**
   - [ ] Track cache hit rate metrics
   - [ ] Log cache operations with trace context
   - [ ] Support distributed tracing
   - [ ] Performance counters for cache operations

---

## 🔧 Technical Implementation

### Files to Create/Modify

```yaml
New_Files:
  - src/BuildingBlocks/Application/Behaviors/CachingBehavior.cs
  - src/BuildingBlocks/Application/Caching/ICacheKeyGenerator.cs
  - src/BuildingBlocks/Application/Caching/DefaultCacheKeyGenerator.cs
  - src/BuildingBlocks/Application/Caching/CacheOptions.cs

Configuration:
  - src/BuildingBlocks/Application/Configuration/CachingConfiguration.cs

Tests:
  - tests/BuildingBlocks.Tests/Application/Behaviors/CachingBehaviorTests.cs
  - tests/BuildingBlocks.Tests/Application/Caching/CacheKeyGeneratorTests.cs
```

### Implementation Steps

#### Step 1: Create Cache Key Generator

```csharp
public interface ICacheKeyGenerator
{
    string GenerateKey<TQuery>(TQuery query) where TQuery : IQuery<object>;
    string GeneratePattern(string prefix);
}

public class DefaultCacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey<TQuery>(TQuery query) where TQuery : IQuery<object>
    {
        var queryType = query.GetType();
        var prefix = query.CacheKeyPrefix ?? queryType.Name;
        
        // Use deterministic hash of query properties
        var queryHash = ComputeHash(query);
        
        // Optional: Include trace context for isolation
        var traceId = query.TraceId ?? "global";
        
        return $"query:{prefix}:{queryHash}:{traceId}";
    }
    
    private string ComputeHash<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(bytes)[..16]; // Truncate for readability
    }
}
```

#### Step 2: Implement Caching Behavior

```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
    where TResponse : class
{
    private readonly IDistributedCache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly CacheOptions _options;
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Skip if caching disabled
        if (!request.UseCache)
        {
            _logger.LogDebug("Cache disabled for {QueryType}", typeof(TRequest).Name);
            return await next();
        }
        
        var cacheKey = _keyGenerator.GenerateKey(request);
        
        // Try get from cache
        try
        {
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedBytes != null)
            {
                var cached = JsonSerializer.Deserialize<TResponse>(cachedBytes);
                _logger.LogInformation("Cache hit for {QueryType} with key {CacheKey}", 
                    typeof(TRequest).Name, cacheKey);
                
                RecordCacheHit(request);
                return cached!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for {CacheKey}, continuing without cache", cacheKey);
        }
        
        // Execute handler
        _logger.LogDebug("Cache miss for {QueryType} with key {CacheKey}", 
            typeof(TRequest).Name, cacheKey);
        RecordCacheMiss(request);
        
        var response = await next();
        
        // Cache successful results only
        if (response is IResult result && !result.IsFailure)
        {
            try
            {
                var duration = request.CacheDuration ?? _options.DefaultDuration;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(response);
                
                await _cache.SetAsync(
                    cacheKey, 
                    bytes,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = duration
                    },
                    cancellationToken);
                    
                _logger.LogDebug("Cached {QueryType} result for {Duration}", 
                    typeof(TRequest).Name, duration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache set failed for {CacheKey}", cacheKey);
            }
        }
        
        return response;
    }
    
    private void RecordCacheHit(TRequest request)
    {
        // Metrics recording
        using var activity = Activity.Current;
        activity?.SetTag("cache.hit", true);
        activity?.SetTag("cache.key", _keyGenerator.GenerateKey(request));
    }
    
    private void RecordCacheMiss(TRequest request)
    {
        using var activity = Activity.Current;
        activity?.SetTag("cache.hit", false);
    }
}
```

#### Step 3: Configuration

```csharp
public class CacheOptions
{
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableCompression { get; set; } = false;
    public bool IncludeTraceInKey { get; set; } = false;
    public int MaxCacheSize { get; set; } = 1000;
}

public static class CachingConfiguration
{
    public static IServiceCollection AddCachingBehavior(
        this IServiceCollection services,
        Action<CacheOptions>? configure = null)
    {
        var options = new CacheOptions();
        configure?.Invoke(options);
        
        services.AddSingleton(options);
        services.AddSingleton<ICacheKeyGenerator, DefaultCacheKeyGenerator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        
        // Add cache implementation
        services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = "localhost:6379";
            opt.InstanceName = "AxonCache";
        });
        
        return services;
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Should_Return_Cached_Result_When_Available()
{
    // Arrange
    var cache = new Mock<IDistributedCache>();
    var cachedResult = new TestResult { Value = "cached" };
    var cachedBytes = JsonSerializer.SerializeToUtf8Bytes(cachedResult);
    
    cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(cachedBytes);
    
    var query = new TestQuery { UseCache = true };
    var behavior = new CachingBehavior<TestQuery, TestResult>(cache.Object, ...);
    
    // Act
    var result = await behavior.Handle(query, () => Task.FromResult(new TestResult { Value = "fresh" }), CancellationToken.None);
    
    // Assert
    result.Value.Should().Be("cached");
}

[Fact]
public async Task Should_Not_Cache_Failed_Results()
{
    // Arrange
    var cache = new Mock<IDistributedCache>();
    var query = new TestQuery { UseCache = true };
    var failedResult = Result<TestData>.Failure(Error.Validation("Test error"));
    
    // Act
    await behavior.Handle(query, () => Task.FromResult(failedResult), CancellationToken.None);
    
    // Assert
    cache.Verify(x => x.SetAsync(
        It.IsAny<string>(), 
        It.IsAny<byte[]>(), 
        It.IsAny<DistributedCacheEntryOptions>(), 
        It.IsAny<CancellationToken>()), 
        Times.Never);
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Cache_Query_Results_In_Redis()
{
    // Arrange
    using var redis = new TestcontainersBuilder<RedisContainer>()
        .WithImage("redis:7-alpine")
        .Build();
    await redis.StartAsync();
    
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    services.AddCachingBehavior(opt =>
    {
        opt.DefaultDuration = TimeSpan.FromSeconds(10);
    });
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    // Act
    var query = new GetProductsQuery { UseCache = true };
    var result1 = await mediator.Send(query);
    var result2 = await mediator.Send(query); // Should hit cache
    
    // Assert
    result1.Should().BeEquivalentTo(result2);
    // Verify handler was called only once
}
```

---

## 📐 Architecture Considerations

### Cache Strategy

1. **Cache-Aside Pattern**: Check cache, execute on miss, update cache
2. **Key Design**: Deterministic, collision-free, debuggable
3. **TTL Strategy**: Query-specific with sensible defaults
4. **Invalidation**: Event-driven or time-based

### Performance Optimization

- Use binary serialization for large objects
- Implement compression for network cache
- Connection pooling for Redis
- Circuit breaker for cache failures

### Security

- Don't cache sensitive data without encryption
- Validate cache keys to prevent injection
- Use separate cache instances per tenant
- Implement cache isolation by user context

---

## 📦 Definition of Done

- [ ] CachingBehavior implemented and tested
- [ ] Cache key generator with deterministic algorithm
- [ ] Configuration with sensible defaults
- [ ] Unit tests with 100% coverage
- [ ] Integration tests with Redis
- [ ] Performance benchmarks documented
- [ ] Metrics and logging implemented
- [ ] Documentation updated
- [ ] Code review approved

---

## 🔄 Migration Strategy

### Phase 1: Infrastructure Setup
- Deploy Redis/cache infrastructure
- Configure cache connections
- Add monitoring dashboards

### Phase 2: Selective Enablement
```csharp
// Start with read-heavy queries
public record GetProductListQuery : QueryBase<List<ProductDto>>
{
    public override bool UseCache => true;
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);
}
```

### Phase 3: Performance Tuning
- Analyze cache hit rates
- Adjust TTL values
- Optimize key generation

---

## 📊 Success Metrics

- Cache hit rate > 70% for enabled queries
- P95 query latency reduced by 50%
- Zero cache-related errors in production
- Successful failover during cache outages

---

## 🚀 Follow-up Stories

1. **Story 03**: Cache Invalidation Strategy
2. **Story 04**: Multi-tier Caching (Memory + Distributed)
3. **Story 05**: Cache Warming and Preloading
4. **Story 06**: Cache Analytics Dashboard