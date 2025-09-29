# Caching Guide

**Multi-tier caching with IMemoryCache and optional IDistributedCache.**

---

## Architecture

```
L1: IMemoryCache (in-process, <1ms)
L2: IDistributedCache (Redis, optional, 5-10ms)
```

**Current Implementation**: L1 only (IMemoryCache) + in-process `MemoryDistributedCache`

---

## Configuration

```csharp
// Startup registration
services.AddMemoryCache();
services.AddDistributedMemoryCache(); // In-process fallback for IDistributedCache

// Optional: Redis for production
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "Axon:";
});
```

---

## Query Caching Behavior

```csharp
// In BuildingBlocks/Application/Behaviors/QueryCachingBehavior.cs
public sealed class QueryCachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery<TResponse>
{
    private readonly IMemoryCache _memory;
    private readonly IDistributedCache? _distributed;

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var cacheKey = request.CacheKey;

        // Check L1 (memory)
        if (_memory.TryGetValue(cacheKey, out TResponse? cached))
            return cached!;

        // Check L2 (distributed, if configured)
        if (_distributed != null)
        {
            var bytes = await _distributed.GetAsync(cacheKey, ct);
            if (bytes != null)
            {
                cached = Deserialize<TResponse>(bytes);
                _memory.Set(cacheKey, cached, request.CacheDuration);
                return cached;
            }
        }

        // Execute query
        var response = await next();

        // Cache only successful results (skip Result<T, Error> failures)
        if (IsSuccess(response))
        {
            _memory.Set(cacheKey, response, request.CacheDuration);

            if (_distributed != null)
            {
                var bytes = Serialize(response);
                await _distributed.SetAsync(cacheKey, bytes, 
                    new DistributedCacheEntryOptions 
                    { 
                        AbsoluteExpirationRelativeToNow = request.CacheDuration 
                    }, ct);
            }
        }

        return response;
    }
}
```

---

## Cacheable Queries

```csharp
// Mark query as cacheable
public sealed record GetMyPrincipalQuery(AxonUserId UserId)
    : ICacheableQuery<CurrentUserResult>
{
    public string CacheKey => $"principal:{UserId.Value}";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(5);
}
```

---

## CurrentUserService Cache Hierarchy

```csharp
// In BuildingBlocks/Infrastructure/Authentication/CurrentUserService.cs
/// <summary>
/// Progressive cache hierarchy:
/// 1. HttpContext.Items (0ms, request-scoped)
/// 2. IMemoryCache (1ms, app-scoped)
/// 3. Database (20-50ms, durable)
/// </summary>
public async Task<AxonPrincipal?> GetCurrentPrincipalAsync(CancellationToken ct)
{
    var principalId = GetPrincipalIdFromToken();

    // L1: HttpContext.Items (request-scoped)
    if (_httpContext.Items.TryGetValue($"principal:{principalId}", out var cached))
        return (AxonPrincipal)cached;

    // L2: IMemoryCache (app-scoped)
    if (_cache.TryGetValue($"principal:{principalId}", out AxonPrincipal? principal))
    {
        _httpContext.Items[$"principal:{principalId}"] = principal;
        return principal;
    }

    // L3: Database
    principal = await _repository.GetByIdAsync(principalId, ct);
    if (principal != null)
    {
        _cache.Set($"principal:{principalId}", principal, TimeSpan.FromMinutes(5));
        _httpContext.Items[$"principal:{principalId}"] = principal;
    }

    return principal;
}
```

---

## Cache Invalidation

**Event-Driven** (Domain Events):
```csharp
public sealed class PrincipalChangedEventHandler
    : INotificationHandler<DomainEventNotification<PrincipalChangedEvent>>
{
    private readonly IMemoryCache _cache;

    public async Task Handle(DomainEventNotification<PrincipalChangedEvent> n, CancellationToken ct)
    {
        var principalId = n.DomainEvent.PrincipalId;
        _cache.Remove($"principal:{principalId}");
    }
}
```

---

## Memory Cache Extensions

```csharp
// BuildingBlocks/Core/Diagnostics/Performance/MemoryCacheExtensions.cs
public static class MemoryCacheExtensions
{
    /// <summary>
    /// Compact cache by percentage (0.0 to 1.0)
    /// </summary>
    public static void Compact(this IMemoryCache cache, double percentage)
    {
        if (cache is MemoryCache memCache)
        {
            memCache.Compact(percentage);
        }
    }
}
```

---

## Best Practices

### ✅ DO
- Cache query results, not commands
- Use short TTLs (5-15 minutes)
- Invalidate on domain events
- Include version in cache key if needed
- Skip caching for Result<T, Error> failures

### ❌ DON'T
- Cache entire aggregates (only DTOs/projections)
- Use long TTLs without invalidation
- Cache write operations
- Over-engineer distributed cache for MVP

---

## Related Documentation

- [CQRS Pattern](../../guides/patterns/cqrs.md) - Query caching with MediatR
- [Testing Guide](../../testing/TESTING-GUIDE.md) - Testing cached queries

---

**Last Updated**: 2025-09-30
