# Infrastructure Migration Matrix - File-by-File Transformation Guide

## Overview
This document provides a detailed file-by-file migration matrix for transforming the current Infrastructure folder to the new functional architecture with Result<T> patterns.

---

## Current to Target File Mapping

### Application Layer Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Application/Behaviors/ObservabilityPipelineBehavior.cs` | `Core/Behaviors/ObservabilityPipelineBehavior.cs` | Add Result<T> wrapping, integrate with new telemetry |
| `Application/Events/CompositeEventMapper.cs` | `Messaging/Events/CompositeEventMapper.cs` | Add Result<T> returns, null safety with Option<T> |
| `Application/Events/EventDispatcher.cs` | `Messaging/Events/EventDispatcher.cs` | Replace exceptions with Result<T>, add circuit breaker |
| `Application/Events/IEventDispatcher.cs` | `Messaging/Events/IEventDispatcher.cs` | Change signature to return Result<T> |
| `Application/Events/IEventMapper.cs` | `Messaging/Events/IEventMapper.cs` | Add Result<T> to mapping operations |

### Caching Layer Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Caching/CachingBehavior.cs` | `Caching/Behaviors/CachingBehavior.cs` | Integrate multi-level cache, add Result<T> |
| `Caching/ICacheRequest.cs` | `Caching/Abstractions/ICacheRequest.cs` | No change needed |
| `Caching/IInvalidateCacheRequest.cs` | `Caching/Abstractions/IInvalidateCacheRequest.cs` | No change needed |
| `Caching/InvalidateCachingBehavior.cs` | `Caching/Behaviors/InvalidateCachingBehavior.cs` | Add Result<T> pattern, async invalidation |

### Logging Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Logging/LoggingBehavior.cs` | `Observability/Behaviors/LoggingBehavior.cs` | Add structured logging, integrate with OpenTelemetry |

### Messaging/MassTransit Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Messaging/MassTransit/ConsumeFilter.cs` | `Messaging/Filters/ConsumeFilter.cs` | Add Result<T> handling, circuit breaker |
| `Messaging/MassTransit/RabbitMqOptions.cs` | `Messaging/Configuration/RabbitMqOptions.cs` | Add validation, Option<T> for optional configs |
| `Messaging/MassTransit/TransportType.cs` | `Messaging/Configuration/TransportType.cs` | No change needed |
| `Messaging/Outbox/IntegrationEventWrapper.cs` | `Messaging/Outbox/IntegrationEventWrapper.cs` | Add Result<T> wrapping |

### Observability Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Observability/HealthChecks/HealthOptions.cs` | Keep location | Add more detailed options |
| `Observability/OpenTelemetry/CoreDiagnostics/*` | Keep structure | Add Result<T> instrumentation |
| `Observability/OpenTelemetry/ObservabilityConstant.cs` | Keep location | Expand constants |
| `Observability/OpenTelemetry/ObservabilityOptions.cs` | Keep location | Add validation |
| `Observability/OpenTelemetry/TelemetryTags.cs` | Keep location | Add more tags |

### Persistence Layer Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Persistence/Caching/CachingRepositoryExtensions.cs` | Merge into `Caching/Extensions/` | Rewrite with Result<T> |
| `Persistence/Common/CacheManagerBase.cs` | `Caching/MultiLevelCache.cs` | Complete rewrite |
| `Persistence/Common/ExplicitTransactionHandler.cs` | DELETE | Replace with UnitOfWork |
| `Persistence/Common/ITransactionBehaviorHandler.cs` | DELETE | Replace with UnitOfWork |
| `Persistence/Common/PerOperationTransactionHandler.cs` | DELETE | Replace with UnitOfWork |
| `Persistence/Common/PerRequestTransactionHandler.cs` | DELETE | Replace with UnitOfWork |
| `Persistence/Common/TransactionBehavior.cs` | `Persistence/Behaviors/TransactionBehavior.cs` | Rewrite with Result<T> |
| `Persistence/Common/Interfaces/IDbContext.cs` | Keep location | Add Result<T> methods |
| `Persistence/Common/Interfaces/IReadDbContext.cs` | Keep location | Add Result<T> methods |
| `Persistence/Common/Interfaces/IReadRepository.cs` | Keep location | Complete rewrite with Result<T> |
| `Persistence/Common/Interfaces/IWriteDbContext.cs` | Keep location | Add Result<T> methods |
| `Persistence/Common/Interfaces/IWriteRepository.cs` | Keep location | Complete rewrite with Result<T> |
| `Persistence/Common/Interfaces/IWriteUnitOfWork.cs` | Keep location | Complete rewrite with Result<T> |
| `Persistence/Extensions.cs` | Keep location | Add Result<T> extensions |
| `Persistence/Infrastructure/*` | Keep structure | Add Result<T> patterns |
| `Persistence/Pagination/PaginationExtensions.cs` | Keep location | Add Result<T> returns |
| `Persistence/PersistMessageProcessor/*` | `Messaging/Outbox/` | Complete rewrite as OutboxProcessor |
| `Persistence/Postgres/*` | Keep structure | Add Result<T> error handling |
| `Persistence/Read/CachedReadRepositoryForAggregates.cs` | DELETE | Replace with multi-level cache |
| `Persistence/Read/EfReadRepository.cs` | Keep location | Rewrite with Result<T> |
| `Persistence/Read/ReadDbContextBase.cs` | Keep location | Add Result<T> methods |
| `Persistence/Write/EfTxBehavior.cs` | DELETE | Replace with TransactionBehavior |
| `Persistence/Write/EfWriteRepository.cs` | Keep location | Rewrite with Result<T> |
| `Persistence/Write/EfWriteUnitOfWork.cs` | Keep location | Complete rewrite with Result<T> |
| `Persistence/Write/WriteDbContextBase.cs` | Keep location | Add Result<T> methods |
| `Persistence/Write/WriteRepositoryWithCacheInvalidation.cs` | DELETE | Integrate into WriteRepository |

### Resilience Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Resilience/PollyExtensions.cs` | `Resilience/Extensions/ResilienceExtensions.cs` | Add Result<T> policies |

### Security Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `Security/AuthHeaderHandler.cs` | Keep location | Add Result<T> validation |

### TestBase Files

| Current File | Target Location | Transformation Required |
|-------------|-----------------|------------------------|
| `TestBase/TestBase.cs` | `Testing/TestBase.cs` | Add Result<T> assertions |
| `TestBase/TestContainers.cs` | `Testing/TestContainers.cs` | No change needed |

---

## Detailed Transformation Templates

### 1. Repository Transformation Template

**Before (Current):**
```csharp
public class EfWriteRepository<TEntity> : IWriteRepository<TEntity>
{
    public async Task AddAsync(TEntity entity)
    {
        await _context.Set<TEntity>().AddAsync(entity);
    }
    
    public async Task<TEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Set<TEntity>().FindAsync(id);
    }
}
```

**After (Target):**
```csharp
public class EfWriteRepository<TEntity> : IWriteRepository<TEntity>
    where TEntity : class, IAggregate
{
    public Result<Unit> Add(TEntity entity)
    {
        try
        {
            _context.Set<TEntity>().Add(entity);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add entity {EntityType}", typeof(TEntity).Name);
            return Result<Unit>.Failure(Error.Persistence(
                "Repository.Add",
                $"Failed to add {typeof(TEntity).Name}"));
        }
    }
    
    public async Task<Result<Option<TEntity>>> GetByIdAsync(Guid id)
    {
        try
        {
            var entity = await _context.Set<TEntity>()
                .FirstOrDefaultAsync(e => e.Id == id);
            
            return Result<Option<TEntity>>.Success(
                entity != null ? Option<TEntity>.Some(entity) : Option<TEntity>.None());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entity {EntityType} by id {Id}", 
                typeof(TEntity).Name, id);
            return Result<Option<TEntity>>.Failure(Error.Persistence(
                "Repository.GetById",
                $"Failed to retrieve {typeof(TEntity).Name}"));
        }
    }
}
```

### 2. Behavior Transformation Template

**Before (Current):**
```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var cachedResponse = await _cache.GetAsync<TResponse>(cacheKey);
        if (cachedResponse != null)
            return cachedResponse;
            
        var response = await next();
        await _cache.SetAsync(cacheKey, response);
        return response;
    }
}
```

**After (Target):**
```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : ICacheRequest
{
    public async Task<Result<TResponse>> Handle(TRequest request, 
        RequestHandlerDelegate<Result<TResponse>> next, 
        CancellationToken cancellationToken)
    {
        var cacheKey = request.CacheKey;
        
        var cachedResult = await _cache.GetAsync<TResponse>(cacheKey);
        if (cachedResult.IsSuccess && cachedResult.Value.IsSome)
        {
            _metrics.RecordCacheHit(cacheKey);
            return Result<TResponse>.Success(cachedResult.Value.Value);
        }
        
        _metrics.RecordCacheMiss(cacheKey);
        
        var result = await next();
        
        if (result.IsSuccess)
        {
            await _cache.SetAsync(cacheKey, result.Value, request.CacheOptions);
        }
        
        return result;
    }
}
```

### 3. Event Dispatcher Transformation Template

**Before (Current):**
```csharp
public class EventDispatcher : IEventDispatcher
{
    public async Task DispatchAsync(IEvent @event)
    {
        await _mediator.Publish(@event);
    }
}
```

**After (Target):**
```csharp
public class EventDispatcher : IEventDispatcher
{
    public async Task<Result<Unit>> DispatchAsync(IEvent @event)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            try
            {
                using var activity = Activity.StartActivity("EventDispatcher.Dispatch");
                activity?.SetTag("event.type", @event.GetType().Name);
                
                await _mediator.Publish(@event);
                
                _metrics.RecordEventDispatched(@event.GetType().Name);
                
                return Result<Unit>.Success(Unit.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch event {EventType}", 
                    @event.GetType().Name);
                    
                _metrics.RecordEventDispatchFailure(@event.GetType().Name);
                
                return Result<Unit>.Failure(Error.External(
                    "EventDispatcher.Dispatch",
                    $"Failed to dispatch {event.GetType().Name}"));
            }
        }, "event-dispatch");
    }
}
```

### 4. UnitOfWork Transformation Template

**Before (Current):**
```csharp
public class EfWriteUnitOfWork : IWriteUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }
    
    public async Task CommitAsync()
    {
        await _transaction?.CommitAsync();
    }
}
```

**After (Target):**
```csharp
public class EfWriteUnitOfWork : IUnitOfWork
{
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Activity.StartActivity("UnitOfWork.SaveChanges");
            
            // Process domain events
            var eventsResult = await ProcessDomainEventsAsync(cancellationToken);
            if (eventsResult.IsFailure)
                return Result<int>.Failure(eventsResult.Error);
            
            // Save to outbox
            var outboxResult = await SaveToOutboxAsync(cancellationToken);
            if (outboxResult.IsFailure)
                return Result<int>.Failure(outboxResult.Error);
            
            // Save changes
            var count = await _context.SaveChangesAsync(cancellationToken);
            
            _metrics.RecordChangedEntities(count);
            
            return Result<int>.Success(count);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failed");
            return Result<int>.Failure(Error.Persistence(
                "UnitOfWork.SaveChanges",
                "Failed to save changes to database"));
        }
    }
    
    public async Task<Result<ITransaction>> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        try
        {
            var transaction = await _context.Database.BeginTransactionAsync(isolationLevel);
            return Result<ITransaction>.Success(new EfTransaction(transaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to begin transaction");
            return Result<ITransaction>.Failure(Error.Persistence(
                "UnitOfWork.BeginTransaction",
                "Failed to begin database transaction"));
        }
    }
}
```

---

## Migration Execution Order

### Phase 1: Core Infrastructure (Days 1-2)
1. Create new folder structure
2. Implement Result<T> and Option<T> if not present
3. Create new interfaces with Result<T> signatures
4. Implement base repository classes

### Phase 2: Persistence Layer (Days 3-4)
1. Transform DbContext classes
2. Rewrite repositories with Result<T>
3. Implement new UnitOfWork
4. Add transaction handling

### Phase 3: Messaging Layer (Days 5-6)
1. Transform EventDispatcher
2. Implement OutboxProcessor
3. Update event mappers
4. Add circuit breakers

### Phase 4: Caching Layer (Day 7)
1. Implement MultiLevelCache
2. Transform caching behaviors
3. Add cache invalidation
4. Update extensions

### Phase 5: Observability & Resilience (Days 8-9)
1. Update telemetry integration
2. Add comprehensive metrics
3. Implement circuit breakers
4. Add retry policies

### Phase 6: Testing & Cleanup (Day 10)
1. Update test base classes
2. Add Result<T> test helpers
3. Remove old files
4. Run integration tests

---

## Validation Checklist

### Per-File Validation
- [ ] File uses Result<T> for all operations that can fail
- [ ] No direct exception throwing (except in catch blocks)
- [ ] All nullable references handled with Option<T>
- [ ] Proper async/await usage
- [ ] Telemetry instrumentation added
- [ ] Unit tests updated
- [ ] Documentation comments added

### Per-Phase Validation
- [ ] All files in phase migrated
- [ ] Integration tests pass
- [ ] No compiler warnings
- [ ] Performance metrics met
- [ ] Circuit breakers tested
- [ ] Rollback plan verified

---

## Rollback Strategy

### Safe Rollback Points
1. **After Phase 1**: Can rollback completely, no domain impact
2. **After Phase 2**: Need to restore old persistence layer
3. **After Phase 3**: Need to restore messaging infrastructure
4. **After Phase 4**: Cache layer can be independently rolled back
5. **After Phase 5**: Observability can be reverted without data loss

### Rollback Procedure
```bash
# 1. Stop application
docker-compose down

# 2. Restore previous branch
git checkout main

# 3. Restore database if needed
psql -U postgres -d axon_backup < backup.sql

# 4. Restart application
docker-compose up -d
```

---

## Success Metrics

### Technical Metrics
- Zero null reference exceptions in production
- 100% of operations return Result<T>
- All async operations properly awaited
- Complete telemetry coverage

### Performance Metrics
- Database query p99 < 50ms
- Cache hit ratio > 80%
- Message processing latency < 100ms
- Circuit breaker response < 5ms

### Quality Metrics
- Test coverage > 90%
- Zero compiler warnings
- All TODO items resolved
- Documentation complete

---

## Conclusion

This migration matrix provides a complete file-by-file transformation guide for the Infrastructure folder refactoring. Follow the phases sequentially, validate at each checkpoint, and maintain rollback capability throughout the process.