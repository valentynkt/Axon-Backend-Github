# Application Migration Matrix - File-by-File Transformation Guide

## Overview
This document provides a detailed file-by-file migration matrix for transforming the current Application folder to the new functional CQRS architecture with Result<T> patterns and comprehensive pipeline behaviors.

---

## Current to Target File Mapping

### Behaviors Layer

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| `Behaviors/ObservabilityPipelineBehavior.cs` | `Behaviors/ObservabilityBehavior.cs` | Add Result<T> wrapping, enhance telemetry, add metrics | High |
| - | `Behaviors/ValidationBehavior.cs` | **NEW** - Create with FluentValidation integration | Critical |
| - | `Behaviors/TransactionBehavior.cs` | **NEW** - Create with UnitOfWork integration | Critical |
| - | `Behaviors/CachingBehavior.cs` | **NEW** - Create for query caching | High |
| - | `Behaviors/LoggingBehavior.cs` | **NEW** - Create structured logging behavior | Medium |
| - | `Behaviors/RetryBehavior.cs` | **NEW** - Create retry logic for transient failures | Medium |
| - | `Behaviors/AuthorizationBehavior.cs` | **NEW** - Create authorization checks | Low |

### Events Layer

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| `Events/EventDispatcher.cs` | `Events/DomainEventDispatcher.cs` | Complete rewrite with Result<T>, add domain event specific logic | Critical |
| `Events/IEventDispatcher.cs` | `Events/IDomainEventDispatcher.cs` | Change signature to return Result<T> | Critical |
| - | `Events/IntegrationEventPublisher.cs` | **NEW** - Create for outbox pattern | Critical |
| - | `Events/IIntegrationEventPublisher.cs` | **NEW** - Interface for integration events | Critical |
| `Events/CompositeEventMapper.cs` | `Events/EventMapper.cs` | Add Result<T> returns, null safety with Option<T> | Medium |
| `Events/IEventMapper.cs` | Keep location | Add Result<T> to mapping operations | Medium |
| - | `Events/EventNotificationHandler.cs` | **NEW** - MediatR notification handler | Low |

### CQRS Abstractions (NEW)

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| - | `Abstractions/CQRS/ICommand.cs` | **NEW** - Command marker interface | Critical |
| - | `Abstractions/CQRS/ICommandHandler.cs` | **NEW** - Command handler interface | Critical |
| - | `Abstractions/CQRS/IQuery.cs` | **NEW** - Query marker interface | Critical |
| - | `Abstractions/CQRS/IQueryHandler.cs` | **NEW** - Query handler interface | Critical |
| - | `Abstractions/CQRS/CommandBase.cs` | **NEW** - Base command implementation | Critical |
| - | `Abstractions/CQRS/QueryBase.cs` | **NEW** - Base query implementation | Critical |
| - | `Abstractions/CQRS/PagedQueryBase.cs` | **NEW** - Paged query base | High |

### Event Abstractions (NEW)

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| - | `Abstractions/Events/IDomainEventHandler.cs` | **NEW** - Domain event handler | High |
| - | `Abstractions/Events/IIntegrationEventHandler.cs` | **NEW** - Integration event handler | High |
| - | `Abstractions/Events/IEventNotification.cs` | **NEW** - Event notification wrapper | Medium |
| - | `Abstractions/Events/DomainEventNotification.cs` | **NEW** - Domain event notification | Medium |

### Validation Layer (NEW)

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| - | `Abstractions/Validation/IValidator.cs` | **NEW** - Validation interface | Critical |
| - | `Abstractions/Validation/ValidationResult.cs` | **NEW** - Validation result type | Critical |
| - | `Validation/FluentValidationAdapter.cs` | **NEW** - FluentValidation integration | Critical |
| - | `Validation/ValidationService.cs` | **NEW** - Validation service | High |
| - | `Validation/CompositeValidator.cs` | **NEW** - Composite validation | Medium |
| - | `Validation/ValidationError.cs` | **NEW** - Validation error type | High |

### Saga Layer (NEW)

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| - | `Abstractions/Sagas/ISaga.cs` | **NEW** - Saga interface | Low |
| - | `Abstractions/Sagas/ISagaState.cs` | **NEW** - Saga state interface | Low |
| - | `Abstractions/Sagas/SagaBase.cs` | **NEW** - Base saga implementation | Low |
| - | `Sagas/SagaManager.cs` | **NEW** - Saga orchestration | Low |
| - | `Sagas/SagaStateRepository.cs` | **NEW** - Saga persistence | Low |
| - | `Sagas/SagaCoordinator.cs` | **NEW** - Saga coordination | Low |

### Extensions (NEW)

| Current File | Target Location | Transformation Required | Priority |
|--------------|-----------------|------------------------|----------|
| - | `Extensions/MediatorExtensions.cs` | **NEW** - MediatR extensions | High |
| - | `Extensions/ValidationExtensions.cs` | **NEW** - Validation helpers | Medium |
| - | `Extensions/PipelineExtensions.cs` | **NEW** - Pipeline configuration | Medium |
| - | `Extensions/ResultExtensions.cs` | **NEW** - Result<T> helpers | High |

---

## Detailed Transformation Templates

### 1. Behavior Transformation Template

**Before (Current ObservabilityPipelineBehavior):**
```csharp
public class ObservabilityPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.StartActivity($"Request_{typeof(TRequest).Name}");
        
        try
        {
            return await next();
        }
        finally
        {
            activity?.Stop();
        }
    }
}
```

**After (Target ObservabilityBehavior):**
```csharp
public sealed class ObservabilityBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : IRequest<Result<TResponse>>
{
    private readonly ILogger<ObservabilityBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.StartActivity($"Request.{typeof(TRequest).Name}");
        
        // Add tags
        activity?.SetTag("request.type", typeof(TRequest).Name);
        activity?.SetTag("request.timestamp", DateTime.UtcNow);
        
        if (request is ICommand<TResponse> command)
        {
            activity?.SetTag("correlation.id", command.CorrelationId);
            activity?.SetTag("operation.type", "command");
        }
        else if (request is IQuery<TResponse> query)
        {
            activity?.SetTag("operation.type", "query");
            activity?.SetTag("use.cache", query.UseCache);
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await next();
            
            stopwatch.Stop();
            
            activity?.SetTag("result.success", result.IsSuccess);
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            
            _metrics.RecordRequestDuration(
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                result.IsSuccess);
            
            if (result.IsFailure)
            {
                activity?.SetTag("error.type", result.Error.Type.ToString());
                activity?.SetTag("error.code", result.Error.Code);
                
                _logger.LogWarning(
                    "Request {RequestType} failed: {Error}",
                    typeof(TRequest).Name,
                    result.Error);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            _metrics.RecordRequestFailure(typeof(TRequest).Name);
            
            _logger.LogError(ex,
                "Unhandled exception in request {RequestType}",
                typeof(TRequest).Name);
            
            return Result<TResponse>.Failure(
                Error.Unexpected(
                    $"Request.{typeof(TRequest).Name}",
                    ex.Message));
        }
    }
}
```

### 2. Event Dispatcher Transformation

**Before (Current EventDispatcher):**
```csharp
public sealed class EventDispatcher : IEventDispatcher
{
    public async Task DispatchAsync(IEvent @event, CancellationToken ct = default)
    {
        await _mediator.Publish(@event, ct);
    }
}
```

**After (Target DomainEventDispatcher):**
```csharp
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    public async Task<Result<Unit>> DispatchAsync(
        IEnumerable<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<Error>();
        
        foreach (var @event in events)
        {
            try
            {
                using var activity = Activity.StartActivity($"DomainEvent.{@event.GetType().Name}");
                activity?.SetTag("event.id", @event.EventId);
                activity?.SetTag("aggregate.id", @event.AggregateId);
                
                var notification = new DomainEventNotification<IDomainEvent>(@event);
                await _mediator.Publish(notification, cancellationToken);
                
                _metrics.RecordDomainEventDispatched(@event.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch event {EventType}", @event.GetType().Name);
                errors.Add(Error.Unexpected("EventDispatcher", $"Failed to dispatch {@event.GetType().Name}"));
            }
        }
        
        return errors.Any()
            ? Result<Unit>.Failure(Error.Aggregate(errors.ToArray()))
            : Result<Unit>.Success(Unit.Value);
    }
}
```

### 3. New Validation Behavior

**New Implementation:**
```csharp
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : ICommand<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        
        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();
        
        if (failures.Any())
        {
            var errors = failures
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToArray();
            
            return Result<TResponse>.Failure(Error.Aggregate(errors));
        }
        
        return await next();
    }
}
```

---

## Migration Execution Plan

### Phase 1: CQRS Foundation (Day 1)
```bash
# Create new abstractions
mkdir -p src/BuildingBlocks/Application/Abstractions/CQRS
mkdir -p src/BuildingBlocks/Application/Abstractions/Events
mkdir -p src/BuildingBlocks/Application/Abstractions/Validation

# Implement base interfaces and classes
- ICommand.cs
- ICommandHandler.cs
- IQuery.cs
- IQueryHandler.cs
- CommandBase.cs
- QueryBase.cs
```

### Phase 2: Behavior Pipeline (Days 2-3)
```bash
# Update existing behavior
- Transform ObservabilityPipelineBehavior.cs

# Create new behaviors
- ValidationBehavior.cs
- TransactionBehavior.cs
- CachingBehavior.cs
- LoggingBehavior.cs
- RetryBehavior.cs
```

### Phase 3: Event System (Days 4-5)
```bash
# Transform existing event handling
- EventDispatcher.cs → DomainEventDispatcher.cs
- IEventDispatcher.cs → IDomainEventDispatcher.cs

# Create integration event handling
- IntegrationEventPublisher.cs
- IIntegrationEventPublisher.cs
- EventNotificationHandler.cs
```

### Phase 4: Validation Framework (Day 6)
```bash
# Create validation infrastructure
mkdir -p src/BuildingBlocks/Application/Validation

# Implement validators
- FluentValidationAdapter.cs
- ValidationService.cs
- CompositeValidator.cs
```

### Phase 5: Saga Support (Days 7-8)
```bash
# Create saga infrastructure
mkdir -p src/BuildingBlocks/Application/Sagas
mkdir -p src/BuildingBlocks/Application/Abstractions/Sagas

# Implement saga components
- ISaga.cs
- ISagaState.cs
- SagaBase.cs
- SagaManager.cs
- SagaStateRepository.cs
```

---

## Validation Checklist Per Phase

### Phase 1 Validation
- [ ] All CQRS interfaces created
- [ ] Base classes implement Result<T> pattern
- [ ] Correlation ID properly tracked
- [ ] Metadata support added

### Phase 2 Validation
- [ ] All behaviors use Result<T>
- [ ] Telemetry properly instrumented
- [ ] Transaction scope correct
- [ ] Cache keys generated correctly

### Phase 3 Validation
- [ ] Domain events dispatched correctly
- [ ] Integration events go to outbox
- [ ] Event metrics recorded
- [ ] Error aggregation works

### Phase 4 Validation
- [ ] FluentValidation integrated
- [ ] Composite validation works
- [ ] Validation errors properly formatted
- [ ] Async validation supported

### Phase 5 Validation
- [ ] Saga state persisted
- [ ] Compensation logic works
- [ ] Message correlation correct
- [ ] Saga metrics recorded

---

## Dependency Updates

### NuGet Packages to Add
```xml
<PackageReference Include="FluentValidation" Version="11.9.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
<PackageReference Include="MediatR" Version="12.2.0" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.2.0" />
```

### Registration in DI Container
```csharp
// Program.cs or Startup.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Application).Assembly);
    cfg.AddBehavior<IPipelineBehavior<,>, ValidationBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, TransactionBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, CachingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, LoggingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, ObservabilityBehavior<,>>();
});

services.AddValidatorsFromAssembly(typeof(Application).Assembly);
```

---

## Testing Strategy

### Unit Test Coverage Requirements

| Component | Required Coverage | Test Types |
|-----------|------------------|------------|
| Command Handlers | 95% | Unit, Integration |
| Query Handlers | 95% | Unit, Integration |
| Behaviors | 100% | Unit |
| Validators | 100% | Unit |
| Event Dispatchers | 90% | Unit, Integration |
| Sagas | 85% | Unit, Integration |

### Test Template for Behaviors
```csharp
[Fact]
public async Task ValidationBehavior_InvalidRequest_ReturnsValidationError()
{
    // Arrange
    var validators = new[] { new TestCommandValidator() };
    var behavior = new ValidationBehavior<TestCommand, TestResponse>(validators, logger);
    var command = new TestCommand { Value = null }; // Invalid
    
    // Act
    var result = await behavior.Handle(
        command,
        () => Task.FromResult(Result<TestResponse>.Success(new TestResponse())),
        CancellationToken.None);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.Validation);
}
```

---

## Rollback Strategy

### Safe Rollback Points
1. **After Phase 1**: Can remove new abstractions without impact
2. **After Phase 2**: Need to restore original behavior
3. **After Phase 3**: Need to restore event handling
4. **After Phase 4**: Can remove validation independently
5. **After Phase 5**: Sagas can be removed if unused

### Rollback Commands
```bash
# Rollback to previous commit
git checkout HEAD~1

# Remove new directories
rm -rf src/BuildingBlocks/Application/Abstractions
rm -rf src/BuildingBlocks/Application/Validation
rm -rf src/BuildingBlocks/Application/Sagas

# Restore original files
git checkout main -- src/BuildingBlocks/Application/
```

---

## Performance Impact Analysis

### Expected Performance Changes

| Component | Current | Target | Impact |
|-----------|---------|--------|--------|
| Command Execution | 50ms | 45ms | -10% (caching) |
| Query Execution | 30ms | 25ms | -17% (caching) |
| Validation Overhead | 0ms | 3-5ms | +5ms |
| Transaction Overhead | 5ms | 8ms | +3ms (outbox) |
| Event Processing | 10ms | 15ms | +5ms (resilience) |

### Optimization Opportunities
1. **Parallel Validation**: Run validators concurrently
2. **Query Caching**: Cache frequently accessed data
3. **Batch Event Processing**: Process events in batches
4. **Connection Pooling**: Optimize database connections
5. **Async All The Way**: Ensure no blocking calls

---

## Success Metrics

### Technical Metrics
- Zero exceptions thrown in Application layer
- 100% Result<T> adoption
- All behaviors properly configured
- Complete telemetry coverage

### Business Metrics
- Command success rate > 99%
- Query response time < 50ms p99
- Validation catching 100% of bad requests
- Zero data inconsistencies

### Quality Metrics
- Test coverage > 90%
- Zero compiler warnings
- All TODOs resolved
- Documentation complete

---

## Common Pitfalls to Avoid

### 1. Behavior Order
```csharp
// WRONG - Transaction before validation
cfg.AddBehavior<TransactionBehavior>();
cfg.AddBehavior<ValidationBehavior>();

// CORRECT - Validation before transaction
cfg.AddBehavior<ValidationBehavior>();
cfg.AddBehavior<TransactionBehavior>();
```

### 2. Result Pattern Misuse
```csharp
// WRONG - Throwing exception
if (entity == null)
    throw new NotFoundException();

// CORRECT - Returning Result
if (entity == null)
    return Result<Entity>.Failure(Error.NotFound("Entity", "Not found"));
```

### 3. Async Deadlocks
```csharp
// WRONG - .Result can cause deadlock
var result = HandleAsync().Result;

// CORRECT - Async all the way
var result = await HandleAsync();
```

---

## Conclusion

This migration matrix provides a complete transformation guide for the Application layer. Follow the phases sequentially, validate at each checkpoint, and maintain backward compatibility during migration. The end result will be a robust, functional CQRS implementation with comprehensive cross-cutting concerns.