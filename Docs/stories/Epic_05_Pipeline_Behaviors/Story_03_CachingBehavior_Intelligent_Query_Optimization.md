# Story 03: CachingBehavior - ❌ NEEDS FULL IMPLEMENTATION

## Story Overview
**Story ID**: Epic_05_Story_03  
**Story Name**: CachingBehavior - Intelligent Query Optimization  
**Estimated Duration**: **1-2 days**  
**Status**: **❌ STUB EXISTS - FULL IMPLEMENTATION REQUIRED**
**Dependencies**: 
- ✅ Epic_04 (CQRS Foundation with IQuery interface)
- ❓ Redis infrastructure setup (needs verification)
- ✅ Result pattern from Epic_03

## Current Implementation Status
**Location**: `src/BuildingBlocks/Infrastructure/Caching/CachingBehavior.cs` (stub only)

**❌ CURRENT STATE:**
- Basic stub class exists ❌
- No actual caching logic implemented ❌
- Missing all required functionality ❌

**🎯 IMPLEMENTATION REQUIRED:**

## User Story
**As a developer**, I want sophisticated caching for queries so that frequently accessed data delivers optimal performance with intelligent invalidation.

## Acceptance Criteria - FULL IMPLEMENTATION NEEDED
- [ ] CachingBehavior class created for IQuery<TResponse> requests only
- [ ] Deterministic cache key generation using request properties
- [ ] Multi-level caching (L1 memory + L2 Redis) with configurable TTLs
- [ ] Per-query-type cache configuration via attributes or options
- [ ] Comprehensive cache metrics via OpenTelemetry
- [ ] Robust JSON serialization with version support
- [ ] Tag-based cache invalidation patterns
- [ ] Unit and integration tests including concurrent scenarios

## Technical Implementation

### Core Components

#### 1. CachingBehavior Class
```csharp
namespace Axon.BuildingBlocks.Infrastructure.Caching;

public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>  // Only for queries
    where TResponse : IResult
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly IOptions<CacheOptions> _options;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;
    
    private static readonly Counter<long> CacheHitCounter = Metrics.CreateCounter<long>(
        "axon.cache.hits",
        description: "Cache hit count");
    private static readonly Counter<long> CacheMissCounter = Metrics.CreateCounter<long>(
        "axon.cache.misses",
        description: "Cache miss count");
    private static readonly Histogram<double> CacheLatency = Metrics.CreateHistogram<double>(
        "axon.cache.latency",
        unit: "ms",
        description: "Cache operation latency");
}
```

#### 2. Cache Configuration
```csharp
namespace Axon.BuildingBlocks.Infrastructure.Caching;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheableAttribute : Attribute
{
    public int DurationSeconds { get; init; } = 300;  // 5 minutes default
    public bool UseSlidingExpiration { get; init; } = false;
    public int L1DurationSeconds { get; init; } = 30;  // 30 seconds L1
    public string[]? Tags { get; init; }
}

public interface ICacheable
{
    CacheConfiguration GetCacheConfiguration();
}
```

### Tasks

#### Task 1: Create CachingBehavior Infrastructure
- [ ] Create `src/BuildingBlocks/Infrastructure/Caching/CachingBehavior.cs`
- [ ] Implement IPipelineBehavior for IQuery<TResponse> only
- [ ] Inject IDistributedCache (Redis) and IMemoryCache (L1)
- [ ] Create ICacheKeyGenerator interface and implementation

#### Task 2: Deterministic Key Generation
- [ ] Create DeterministicCacheKeyGenerator class
- [ ] Use System.Text.Json to serialize request properties
- [ ] Sort properties alphabetically for consistency
- [ ] Include request type name and version in key
- [ ] Generate format: `axon:cache:{type}:{version}:{hash}`

#### Task 3: Multi-Level Caching Implementation
- [ ] Check L1 (IMemoryCache) first for hot data
- [ ] Fall back to L2 (Redis) on L1 miss
- [ ] Populate L1 from L2 on L2 hit
- [ ] Write-through to both levels on cache set
- [ ] Configure separate TTLs for each level

#### Task 4: Cache Configuration System
- [ ] Create CacheableAttribute for declarative configuration
- [ ] Support ICacheable interface for dynamic configuration
- [ ] Load configuration from IOptions<CacheOptions>
- [ ] Support per-environment overrides
- [ ] Default to no caching if not configured

#### Task 5: Serialization with Versioning
- [ ] Use System.Text.Json for serialization
- [ ] Include metadata: version, cached_at, expires_at
- [ ] Handle version mismatches (invalidate old versions)
- [ ] Support compression for payloads > 1KB
- [ ] Handle serialization failures gracefully

#### Task 6: Cache Metrics and Monitoring
- [ ] Track hit/miss ratio per query type
- [ ] Measure cache operation latency (get/set)
- [ ] Record cache size metrics for L1
- [ ] Add OpenTelemetry activity tags
- [ ] Create health check for Redis connectivity

#### Task 7: Tag-Based Invalidation
- [ ] Implement tag storage in Redis sets
- [ ] Support invalidation by single or multiple tags
- [ ] Create InvalidateCachingBehavior for commands
- [ ] Track tag relationships efficiently
- [ ] Support wildcard tag patterns

#### Task 8: Comprehensive Testing
- [ ] Unit tests for key generation determinism
- [ ] Integration tests with Redis container
- [ ] Concurrent access tests (cache stampede)
- [ ] L1/L2 coordination tests
- [ ] Serialization edge cases
- [ ] Performance benchmarks

## Code Examples

### Query with Caching
```csharp
[Cacheable(DurationSeconds = 300, Tags = new[] { "users", "user-{UserId}" })]
public record GetUserByIdQuery(UserId UserId) : IQuery<Result<UserResponse>>;
```

### Cache Invalidation in Command
```csharp
public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        // Update user logic...
        
        // Invalidate related caches
        await _cacheInvalidator.InvalidateByTagsAsync(new[]
        {
            "users",
            $"user-{command.UserId}"
        });
        
        return Result.Success(Unit.Value);
    }
}
```

### Cache Key Example
```json
{
  "key": "axon:cache:GetUserByIdQuery:v1:abc123def",
  "metadata": {
    "version": "1.0",
    "cached_at": "2024-01-15T10:30:00Z",
    "expires_at": "2024-01-15T10:35:00Z",
    "tags": ["users", "user-123"]
  },
  "data": {
    // Serialized response
  }
}
```

## Definition of Done
- [ ] CachingBehavior implemented with L1/L2 support
- [ ] Deterministic key generation working correctly
- [ ] Cache configuration via attributes and options
- [ ] All tests passing including concurrent scenarios
- [ ] OpenTelemetry metrics properly exposed
- [ ] Tag-based invalidation operational
- [ ] Performance targets met (L1 < 2ms, L2 < 10ms)
- [ ] Documentation with configuration examples

## Technical Notes

### Performance Targets
- L1 (Memory) hit: < 2ms total latency
- L2 (Redis) hit: < 10ms total latency
- Key generation: < 1ms
- JSON serialization: < 5ms for typical payloads

### Cache Stampede Prevention
- Use SemaphoreSlim to prevent multiple simultaneous fetches
- Consider implementing cache aside pattern with locking
- Add jitter to expiration times to spread load

### Registration Order
```csharp
services.AddMediatR(cfg =>
{
    cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));        // This story
    cfg.AddOpenBehavior(typeof(InvalidateCachingBehavior<,>)); // For commands
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Redis Configuration
```json
{
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "axon",
    "DefaultDatabase": 0
  },
  "Caching": {
    "DefaultDuration": 300,
    "L1DefaultDuration": 30,
    "MaxL1Size": "100MB",
    "CompressionThreshold": 1024
  }
}
```

## Dependencies
- Microsoft.Extensions.Caching.StackExchangeRedis 8.0.0
- Microsoft.Extensions.Caching.Memory
- System.Text.Json (included in .NET)
- System.IO.Compression (for large payload compression)

## References
- Redis documentation: https://redis.io/docs/
- Distributed caching in ASP.NET Core: https://docs.microsoft.com/aspnet/core/performance/caching/distributed