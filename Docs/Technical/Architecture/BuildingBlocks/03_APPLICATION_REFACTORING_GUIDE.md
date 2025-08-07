# Application Layer Refactoring Guide - BuildingBlocks

## Executive Summary

This guide provides a comprehensive roadmap for refactoring the BuildingBlocks Application layer following the brutal refactoring approach. The Application layer orchestrates use cases, implements CQRS patterns, and manages cross-cutting concerns through pipeline behaviors.

### Key Principles
- **Result<T> Pattern**: All operations return Result<T> - no exceptions
- **Railway-Oriented Programming**: Chain operations with Bind/Map
- **CQRS Separation**: Complete separation of commands and queries
- **Pipeline Behaviors**: Cross-cutting concerns via MediatR pipeline
- **Functional Composition**: Compose behaviors for complex workflows

---

## Current State Analysis

### Existing Structure
```
src/BuildingBlocks/Application/
├── Behaviors/
│   └── ObservabilityPipelineBehavior.cs
└── Events/
    ├── CompositeEventMapper.cs
    ├── EventDispatcher.cs
    ├── IEventDispatcher.cs
    └── IEventMapper.cs
```

### Problems Identified
1. **Limited Behaviors**: Only observability, missing validation, transaction, caching
2. **No CQRS Base Classes**: Missing command/query base abstractions
3. **Weak Event Handling**: No domain event processing pipeline
4. **No Validation Framework**: Missing FluentValidation integration
5. **Limited Error Handling**: Direct exceptions instead of Result<T>
6. **No Saga/Process Manager**: Missing long-running process support

---

## Target Architecture

### New Folder Structure
```
src/BuildingBlocks/Application/
├── Abstractions/
│   ├── CQRS/
│   │   ├── ICommand.cs
│   │   ├── ICommandHandler.cs
│   │   ├── IQuery.cs
│   │   ├── IQueryHandler.cs
│   │   ├── CommandBase.cs
│   │   └── QueryBase.cs
│   ├── Events/
│   │   ├── IDomainEventHandler.cs
│   │   ├── IIntegrationEventHandler.cs
│   │   └── IEventNotification.cs
│   ├── Validation/
│   │   ├── IValidator.cs
│   │   └── ValidationResult.cs
│   └── Sagas/
│       ├── ISaga.cs
│       ├── ISagaState.cs
│       └── SagaBase.cs
├── Behaviors/
│   ├── ValidationBehavior.cs
│   ├── TransactionBehavior.cs
│   ├── CachingBehavior.cs
│   ├── LoggingBehavior.cs
│   ├── ObservabilityBehavior.cs
│   ├── RetryBehavior.cs
│   └── AuthorizationBehavior.cs
├── Events/
│   ├── DomainEventDispatcher.cs
│   ├── IntegrationEventPublisher.cs
│   ├── EventMapper.cs
│   └── EventNotificationHandler.cs
├── Validation/
│   ├── FluentValidationAdapter.cs
│   ├── ValidationService.cs
│   └── CompositeValidator.cs
├── Sagas/
│   ├── SagaManager.cs
│   ├── SagaStateRepository.cs
│   └── SagaCoordinator.cs
└── Extensions/
    ├── MediatorExtensions.cs
    ├── ValidationExtensions.cs
    └── PipelineExtensions.cs
```

---

## Implementation Phases

## Phase 1: CQRS Abstractions (Day 1)

### 1.1 Command Abstractions

```csharp
// Application/Abstractions/CQRS/ICommand.cs
namespace BuildingBlocks.Application.Abstractions.CQRS;

/// <summary>
/// Marker interface for commands that return a result
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
    Guid CorrelationId { get; }
    DateTime Timestamp { get; }
    Dictionary<string, object> Metadata { get; }
}

/// <summary>
/// Marker interface for commands without return value
/// </summary>
public interface ICommand : ICommand<Unit> { }

/// <summary>
/// Base command implementation
/// </summary>
public abstract record CommandBase<TResponse> : ICommand<TResponse>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    protected CommandBase() { }
    
    protected CommandBase(Guid correlationId)
    {
        CorrelationId = correlationId;
    }
}

public abstract record CommandBase : CommandBase<Unit>
{
    protected CommandBase() : base() { }
    protected CommandBase(Guid correlationId) : base(correlationId) { }
}
```

### 1.2 Command Handler

```csharp
// Application/Abstractions/CQRS/ICommandHandler.cs
namespace BuildingBlocks.Application.Abstractions.CQRS;

/// <summary>
/// Command handler interface with Result<T> pattern
/// </summary>
public interface ICommandHandler<TCommand, TResponse> 
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}

public interface ICommandHandler<TCommand> 
    : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
}

/// <summary>
/// Base command handler with common functionality
/// </summary>
public abstract class CommandHandlerBase<TCommand, TResponse> 
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    protected readonly ILogger<CommandHandlerBase<TCommand, TResponse>> Logger;
    protected readonly IUnitOfWork UnitOfWork;
    
    protected CommandHandlerBase(
        ILogger<CommandHandlerBase<TCommand, TResponse>> logger,
        IUnitOfWork unitOfWork)
    {
        Logger = logger;
        UnitOfWork = unitOfWork;
    }
    
    public async Task<Result<TResponse>> Handle(
        TCommand command, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var activity = Activity.StartActivity($"CommandHandler.{typeof(TCommand).Name}");
            activity?.SetTag("command.type", typeof(TCommand).Name);
            activity?.SetTag("correlation.id", command.CorrelationId);
            
            Logger.LogInformation(
                "Handling command {CommandType} with CorrelationId {CorrelationId}",
                typeof(TCommand).Name,
                command.CorrelationId);
            
            var result = await HandleCommand(command, cancellationToken);
            
            if (result.IsSuccess)
            {
                Logger.LogInformation(
                    "Command {CommandType} handled successfully",
                    typeof(TCommand).Name);
            }
            else
            {
                Logger.LogWarning(
                    "Command {CommandType} failed: {Error}",
                    typeof(TCommand).Name,
                    result.Error);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Unexpected error handling command {CommandType}",
                typeof(TCommand).Name);
                
            return Result<TResponse>.Failure(
                Error.Unexpected(
                    $"CommandHandler.{typeof(TCommand).Name}",
                    ex.Message));
        }
    }
    
    protected abstract Task<Result<TResponse>> HandleCommand(
        TCommand command,
        CancellationToken cancellationToken);
}
```

### 1.3 Query Abstractions

```csharp
// Application/Abstractions/CQRS/IQuery.cs
namespace BuildingBlocks.Application.Abstractions.CQRS;

/// <summary>
/// Marker interface for queries
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
    bool UseCache { get; }
    TimeSpan? CacheDuration { get; }
}

/// <summary>
/// Paged query interface
/// </summary>
public interface IPagedQuery<TResponse> : IQuery<PagedResult<TResponse>>
{
    int Page { get; }
    int PageSize { get; }
    string? SortBy { get; }
    bool SortDescending { get; }
}

/// <summary>
/// Base query implementation
/// </summary>
public abstract record QueryBase<TResponse> : IQuery<TResponse>
{
    public virtual bool UseCache { get; init; } = false;
    public virtual TimeSpan? CacheDuration { get; init; }
    
    protected QueryBase() { }
}

public abstract record PagedQueryBase<TResponse> : IPagedQuery<TResponse>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = false;
    public virtual bool UseCache { get; init; } = false;
    public virtual TimeSpan? CacheDuration { get; init; }
    
    protected PagedQueryBase() { }
}
```

### 1.4 Query Handler

```csharp
// Application/Abstractions/CQRS/IQueryHandler.cs
namespace BuildingBlocks.Application.Abstractions.CQRS;

/// <summary>
/// Query handler interface with Result<T> pattern
/// </summary>
public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}

/// <summary>
/// Base query handler with common functionality
/// </summary>
public abstract class QueryHandlerBase<TQuery, TResponse>
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    protected readonly ILogger<QueryHandlerBase<TQuery, TResponse>> Logger;
    protected readonly IReadRepository Repository;
    
    protected QueryHandlerBase(
        ILogger<QueryHandlerBase<TQuery, TResponse>> logger,
        IReadRepository repository)
    {
        Logger = logger;
        Repository = repository;
    }
    
    public async Task<Result<TResponse>> Handle(
        TQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            using var activity = Activity.StartActivity($"QueryHandler.{typeof(TQuery).Name}");
            activity?.SetTag("query.type", typeof(TQuery).Name);
            activity?.SetTag("query.use_cache", query.UseCache);
            
            Logger.LogDebug(
                "Handling query {QueryType}",
                typeof(TQuery).Name);
            
            var result = await HandleQuery(query, cancellationToken);
            
            if (result.IsFailure)
            {
                Logger.LogWarning(
                    "Query {QueryType} failed: {Error}",
                    typeof(TQuery).Name,
                    result.Error);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Unexpected error handling query {QueryType}",
                typeof(TQuery).Name);
                
            return Result<TResponse>.Failure(
                Error.Unexpected(
                    $"QueryHandler.{typeof(TQuery).Name}",
                    ex.Message));
        }
    }
    
    protected abstract Task<Result<TResponse>> HandleQuery(
        TQuery query,
        CancellationToken cancellationToken);
}
```

---

## Phase 2: Pipeline Behaviors (Day 2-3)

### 2.1 Validation Behavior

```csharp
// Application/Behaviors/ValidationBehavior.cs
namespace BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : ICommand<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;
    
    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();
        
        using var activity = Activity.StartActivity("ValidationBehavior");
        activity?.SetTag("request.type", typeof(TRequest).Name);
        
        var context = new ValidationContext<TRequest>(request);
        
        var validationTasks = _validators
            .Select(v => v.ValidateAsync(context, cancellationToken));
        
        var validationResults = await Task.WhenAll(validationTasks);
        
        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();
        
        if (failures.Any())
        {
            _logger.LogWarning(
                "Validation failed for {RequestType} with {ErrorCount} errors",
                typeof(TRequest).Name,
                failures.Count);
            
            var errors = failures
                .GroupBy(e => e.PropertyName)
                .Select(g => Error.Validation(
                    g.Key,
                    string.Join("; ", g.Select(e => e.ErrorMessage))))
                .ToArray();
            
            return Result<TResponse>.Failure(Error.Aggregate(errors));
        }
        
        return await next();
    }
}
```

### 2.2 Transaction Behavior

```csharp
// Application/Behaviors/TransactionBehavior.cs
namespace BuildingBlocks.Application.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : ICommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly IOutboxProcessor _outboxProcessor;
    
    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IOutboxProcessor outboxProcessor)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _outboxProcessor = outboxProcessor;
    }
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        // Skip if already in transaction
        if (_unitOfWork.HasActiveTransaction)
            return await next();
        
        using var activity = Activity.StartActivity("TransactionBehavior");
        activity?.SetTag("request.type", typeof(TRequest).Name);
        
        var transactionResult = await _unitOfWork.BeginTransactionAsync();
        if (transactionResult.IsFailure)
            return Result<TResponse>.Failure(transactionResult.Error);
        
        using var transaction = transactionResult.Value;
        
        try
        {
            var result = await next();
            
            if (result.IsSuccess)
            {
                var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saveResult.IsFailure)
                {
                    await transaction.RollbackAsync();
                    return Result<TResponse>.Failure(saveResult.Error);
                }
                
                await transaction.CommitAsync();
                
                // Process outbox messages after commit
                _ = Task.Run(() => _outboxProcessor.ProcessPendingAsync(), cancellationToken);
                
                _logger.LogDebug(
                    "Transaction committed for {RequestType}",
                    typeof(TRequest).Name);
            }
            else
            {
                await transaction.RollbackAsync();
                _logger.LogDebug(
                    "Transaction rolled back for {RequestType}",
                    typeof(TRequest).Name);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            
            _logger.LogError(ex,
                "Transaction failed for {RequestType}",
                typeof(TRequest).Name);
            
            return Result<TResponse>.Failure(
                Error.Unexpected(
                    "TransactionBehavior",
                    "Transaction failed"));
        }
    }
}
```

### 2.3 Caching Behavior

```csharp
// Application/Behaviors/CachingBehavior.cs
namespace BuildingBlocks.Application.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : IQuery<TResponse>
{
    private readonly IMultiLevelCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    private readonly ICacheKeyGenerator _keyGenerator;
    
    public CachingBehavior(
        IMultiLevelCache cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger,
        ICacheKeyGenerator keyGenerator)
    {
        _cache = cache;
        _logger = logger;
        _keyGenerator = keyGenerator;
    }
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!request.UseCache)
            return await next();
        
        var cacheKey = _keyGenerator.GenerateKey(request);
        
        using var activity = Activity.StartActivity("CachingBehavior");
        activity?.SetTag("cache.key", cacheKey);
        activity?.SetTag("request.type", typeof(TRequest).Name);
        
        // Try to get from cache
        var cachedResult = await _cache.GetAsync<TResponse>(cacheKey);
        
        if (cachedResult.IsSuccess && cachedResult.Value.IsSome)
        {
            _logger.LogDebug(
                "Cache hit for {RequestType} with key {CacheKey}",
                typeof(TRequest).Name,
                cacheKey);
            
            activity?.SetTag("cache.hit", true);
            return Result<TResponse>.Success(cachedResult.Value.Value);
        }
        
        _logger.LogDebug(
            "Cache miss for {RequestType} with key {CacheKey}",
            typeof(TRequest).Name,
            cacheKey);
        
        activity?.SetTag("cache.hit", false);
        
        // Execute handler
        var result = await next();
        
        // Cache successful results
        if (result.IsSuccess && request.CacheDuration.HasValue)
        {
            await _cache.SetAsync(
                cacheKey,
                result.Value,
                new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = request.CacheDuration.Value,
                    Priority = CachePriority.Normal
                });
            
            _logger.LogDebug(
                "Cached result for {RequestType} with key {CacheKey}",
                typeof(TRequest).Name,
                cacheKey);
        }
        
        return result;
    }
}
```

### 2.4 Retry Behavior

```csharp
// Application/Behaviors/RetryBehavior.cs
namespace BuildingBlocks.Application.Behaviors;

public sealed class RetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, Result<TResponse>>
    where TRequest : ICommand<TResponse>
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    
    public RetryBehavior(
        IRetryPolicy retryPolicy,
        ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _retryPolicy = retryPolicy;
        _logger = logger;
    }
    
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TResponse>> next,
        CancellationToken cancellationToken)
    {
        // Check if request is retryable
        if (request is not IRetryableCommand retryable || !retryable.IsRetryable)
            return await next();
        
        using var activity = Activity.StartActivity("RetryBehavior");
        activity?.SetTag("request.type", typeof(TRequest).Name);
        
        var attemptCount = 0;
        Result<TResponse> lastResult = null!;
        
        await _retryPolicy.ExecuteAsync(
            async () =>
            {
                attemptCount++;
                activity?.SetTag("retry.attempt", attemptCount);
                
                _logger.LogDebug(
                    "Executing {RequestType}, attempt {Attempt}",
                    typeof(TRequest).Name,
                    attemptCount);
                
                lastResult = await next();
                
                if (lastResult.IsFailure && IsTransientError(lastResult.Error))
                {
                    _logger.LogWarning(
                        "Transient error on attempt {Attempt} for {RequestType}: {Error}",
                        attemptCount,
                        typeof(TRequest).Name,
                        lastResult.Error);
                    
                    throw new TransientException(lastResult.Error);
                }
                
                return lastResult;
            },
            retryable.MaxRetries,
            retryable.RetryDelay);
        
        return lastResult;
    }
    
    private static bool IsTransientError(Error error)
    {
        return error.Type == ErrorType.External ||
               error.Type == ErrorType.Conflict ||
               (error.Type == ErrorType.Persistence && 
                error.Code.Contains("Timeout", StringComparison.OrdinalIgnoreCase));
    }
}
```

---

## Phase 3: Event Handling (Day 4-5)

### 3.1 Domain Event Dispatcher

```csharp
// Application/Events/DomainEventDispatcher.cs
namespace BuildingBlocks.Application.Events;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<DomainEventDispatcher> _logger;
    private readonly IEventMetrics _metrics;
    
    public DomainEventDispatcher(
        IMediator mediator,
        ILogger<DomainEventDispatcher> logger,
        IEventMetrics metrics)
    {
        _mediator = mediator;
        _logger = logger;
        _metrics = metrics;
    }
    
    public async Task<Result<Unit>> DispatchAsync(
        IEnumerable<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
            return Result<Unit>.Success(Unit.Value);
        
        using var activity = Activity.StartActivity("DomainEventDispatcher.Dispatch");
        activity?.SetTag("event.count", eventsList.Count);
        
        var errors = new List<Error>();
        
        foreach (var @event in eventsList)
        {
            try
            {
                using var eventActivity = Activity.StartActivity($"DomainEvent.{@event.GetType().Name}");
                eventActivity?.SetTag("event.id", @event.EventId);
                eventActivity?.SetTag("aggregate.id", @event.AggregateId);
                
                _logger.LogDebug(
                    "Dispatching domain event {EventType} for aggregate {AggregateId}",
                    @event.GetType().Name,
                    @event.AggregateId);
                
                var notification = new DomainEventNotification<IDomainEvent>(@event);
                await _mediator.Publish(notification, cancellationToken);
                
                _metrics.RecordDomainEventDispatched(@event.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch domain event {EventType}",
                    @event.GetType().Name);
                
                errors.Add(Error.Unexpected(
                    "DomainEventDispatcher",
                    $"Failed to dispatch {@event.GetType().Name}"));
                
                _metrics.RecordDomainEventFailed(@event.GetType().Name);
            }
        }
        
        return errors.Any()
            ? Result<Unit>.Failure(Error.Aggregate(errors.ToArray()))
            : Result<Unit>.Success(Unit.Value);
    }
}
```

### 3.2 Integration Event Publisher

```csharp
// Application/Events/IntegrationEventPublisher.cs
namespace BuildingBlocks.Application.Events;

public sealed class IntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<IntegrationEventPublisher> _logger;
    private readonly IEventMetrics _metrics;
    
    public IntegrationEventPublisher(
        IOutboxRepository outboxRepository,
        IEventSerializer serializer,
        ILogger<IntegrationEventPublisher> logger,
        IEventMetrics metrics)
    {
        _outboxRepository = outboxRepository;
        _serializer = serializer;
        _logger = logger;
        _metrics = metrics;
    }
    
    public async Task<Result<Unit>> PublishAsync(
        IIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("IntegrationEventPublisher.Publish");
        activity?.SetTag("event.type", @event.GetType().Name);
        activity?.SetTag("event.id", @event.EventId);
        
        try
        {
            _logger.LogInformation(
                "Publishing integration event {EventType} with ID {EventId}",
                @event.GetType().Name,
                @event.EventId);
            
            var outboxMessage = OutboxMessage.Create(
                @event.EventId,
                @event.GetType().Name,
                _serializer.Serialize(@event),
                @event.Metadata);
            
            var result = await _outboxRepository.AddAsync(outboxMessage, cancellationToken);
            
            if (result.IsSuccess)
            {
                _metrics.RecordIntegrationEventPublished(@event.GetType().Name);
                _logger.LogDebug(
                    "Integration event {EventType} added to outbox",
                    @event.GetType().Name);
            }
            else
            {
                _metrics.RecordIntegrationEventFailed(@event.GetType().Name);
                _logger.LogError(
                    "Failed to add integration event {EventType} to outbox: {Error}",
                    @event.GetType().Name,
                    result.Error);
            }
            
            return result.Map(_ => Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error publishing integration event {EventType}",
                @event.GetType().Name);
            
            return Result<Unit>.Failure(
                Error.Unexpected(
                    "IntegrationEventPublisher",
                    $"Failed to publish {@event.GetType().Name}"));
        }
    }
    
    public async Task<Result<Unit>> PublishBatchAsync(
        IEnumerable<IIntegrationEvent> events,
        CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
            return Result<Unit>.Success(Unit.Value);
        
        using var activity = Activity.StartActivity("IntegrationEventPublisher.PublishBatch");
        activity?.SetTag("event.count", eventsList.Count);
        
        var outboxMessages = eventsList
            .Select(e => OutboxMessage.Create(
                e.EventId,
                e.GetType().Name,
                _serializer.Serialize(e),
                e.Metadata))
            .ToList();
        
        var result = await _outboxRepository.AddRangeAsync(outboxMessages, cancellationToken);
        
        if (result.IsSuccess)
        {
            foreach (var @event in eventsList)
            {
                _metrics.RecordIntegrationEventPublished(@event.GetType().Name);
            }
        }
        
        return result.Map(_ => Unit.Value);
    }
}
```

---

## Phase 4: Validation Framework (Day 6)

### 4.1 FluentValidation Integration

```csharp
// Application/Validation/FluentValidationAdapter.cs
namespace BuildingBlocks.Application.Validation;

public sealed class FluentValidationAdapter<T> : IValidator<T>
{
    private readonly AbstractValidator<T> _validator;
    private readonly ILogger<FluentValidationAdapter<T>> _logger;
    
    public FluentValidationAdapter(
        AbstractValidator<T> validator,
        ILogger<FluentValidationAdapter<T>> logger)
    {
        _validator = validator;
        _logger = logger;
    }
    
    public async Task<ValidationResult> ValidateAsync(
        T instance,
        CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("Validation");
        activity?.SetTag("type", typeof(T).Name);
        
        var result = await _validator.ValidateAsync(instance, cancellationToken);
        
        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Validation failed for {Type} with {ErrorCount} errors",
                typeof(T).Name,
                result.Errors.Count);
            
            var errors = result.Errors
                .Select(e => new ValidationError(
                    e.PropertyName,
                    e.ErrorMessage,
                    e.ErrorCode))
                .ToList();
            
            return ValidationResult.Failure(errors);
        }
        
        return ValidationResult.Success();
    }
    
    public ValidationResult ValidateSync(T instance)
    {
        var result = _validator.Validate(instance);
        
        if (!result.IsValid)
        {
            var errors = result.Errors
                .Select(e => new ValidationError(
                    e.PropertyName,
                    e.ErrorMessage,
                    e.ErrorCode))
                .ToList();
            
            return ValidationResult.Failure(errors);
        }
        
        return ValidationResult.Success();
    }
}
```

### 4.2 Composite Validator

```csharp
// Application/Validation/CompositeValidator.cs
namespace BuildingBlocks.Application.Validation;

public sealed class CompositeValidator<T> : IValidator<T>
{
    private readonly IEnumerable<IValidator<T>> _validators;
    private readonly ILogger<CompositeValidator<T>> _logger;
    
    public CompositeValidator(
        IEnumerable<IValidator<T>> validators,
        ILogger<CompositeValidator<T>> logger)
    {
        _validators = validators;
        _logger = logger;
    }
    
    public async Task<ValidationResult> ValidateAsync(
        T instance,
        CancellationToken cancellationToken = default)
    {
        if (!_validators.Any())
            return ValidationResult.Success();
        
        var validationTasks = _validators
            .Select(v => v.ValidateAsync(instance, cancellationToken));
        
        var results = await Task.WhenAll(validationTasks);
        
        var allErrors = results
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();
        
        if (allErrors.Any())
        {
            _logger.LogWarning(
                "Composite validation failed with {ErrorCount} errors",
                allErrors.Count);
            
            return ValidationResult.Failure(allErrors);
        }
        
        return ValidationResult.Success();
    }
}
```

---

## Phase 5: Saga Implementation (Day 7-8)

### 5.1 Saga Base

```csharp
// Application/Sagas/SagaBase.cs
namespace BuildingBlocks.Application.Sagas;

public abstract class SagaBase<TState> : ISaga<TState>
    where TState : ISagaState, new()
{
    private readonly List<ICommand> _commands = new();
    private readonly List<IIntegrationEvent> _events = new();
    
    public Guid SagaId { get; protected set; }
    public TState State { get; protected set; }
    public SagaStatus Status { get; protected set; }
    public DateTime StartedAt { get; protected set; }
    public DateTime? CompletedAt { get; protected set; }
    public string? FailureReason { get; protected set; }
    
    protected SagaBase()
    {
        SagaId = Guid.NewGuid();
        State = new TState();
        Status = SagaStatus.NotStarted;
        StartedAt = DateTime.UtcNow;
    }
    
    public async Task<Result<Unit>> StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status != SagaStatus.NotStarted)
            return Result<Unit>.Failure(Error.InvalidOperation(
                "Saga.Start",
                "Saga has already been started"));
        
        Status = SagaStatus.Running;
        State.CurrentStep = 0;
        
        return await ExecuteNextStepAsync(cancellationToken);
    }
    
    public async Task<Result<Unit>> HandleAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        if (Status != SagaStatus.Running)
            return Result<Unit>.Failure(Error.InvalidOperation(
                "Saga.Handle",
                $"Cannot handle message in {Status} status"));
        
        var result = await ProcessMessageAsync(message, cancellationToken);
        
        if (result.IsFailure)
        {
            return await CompensateAsync(cancellationToken);
        }
        
        State.CurrentStep++;
        
        if (State.CurrentStep >= GetTotalSteps())
        {
            return await CompleteAsync(cancellationToken);
        }
        
        return await ExecuteNextStepAsync(cancellationToken);
    }
    
    protected abstract Task<Result<Unit>> ExecuteNextStepAsync(
        CancellationToken cancellationToken);
    
    protected abstract Task<Result<Unit>> ProcessMessageAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : class;
    
    protected abstract Task<Result<Unit>> CompensateAsync(
        CancellationToken cancellationToken);
    
    protected abstract int GetTotalSteps();
    
    private async Task<Result<Unit>> CompleteAsync(CancellationToken cancellationToken)
    {
        Status = SagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        
        await PublishCompletionEventAsync(cancellationToken);
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    protected void AddCommand(ICommand command)
    {
        _commands.Add(command);
    }
    
    protected void AddEvent(IIntegrationEvent @event)
    {
        _events.Add(@event);
    }
    
    public IReadOnlyList<ICommand> GetPendingCommands() => _commands.AsReadOnly();
    public IReadOnlyList<IIntegrationEvent> GetPendingEvents() => _events.AsReadOnly();
    
    public void ClearPendingMessages()
    {
        _commands.Clear();
        _events.Clear();
    }
    
    protected abstract Task PublishCompletionEventAsync(CancellationToken cancellationToken);
}
```

### 5.2 Saga Manager

```csharp
// Application/Sagas/SagaManager.cs
namespace BuildingBlocks.Application.Sagas;

public sealed class SagaManager : ISagaManager
{
    private readonly ISagaStateRepository _repository;
    private readonly IMediator _mediator;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<SagaManager> _logger;
    private readonly ISagaMetrics _metrics;
    
    public SagaManager(
        ISagaStateRepository repository,
        IMediator mediator,
        IIntegrationEventPublisher eventPublisher,
        ILogger<SagaManager> logger,
        ISagaMetrics metrics)
    {
        _repository = repository;
        _mediator = mediator;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _metrics = metrics;
    }
    
    public async Task<Result<Guid>> StartSagaAsync<TSaga, TState>(
        TSaga saga,
        CancellationToken cancellationToken = default)
        where TSaga : ISaga<TState>
        where TState : ISagaState
    {
        using var activity = Activity.StartActivity("SagaManager.StartSaga");
        activity?.SetTag("saga.type", typeof(TSaga).Name);
        activity?.SetTag("saga.id", saga.SagaId);
        
        _logger.LogInformation(
            "Starting saga {SagaType} with ID {SagaId}",
            typeof(TSaga).Name,
            saga.SagaId);
        
        var startResult = await saga.StartAsync(cancellationToken);
        if (startResult.IsFailure)
        {
            _metrics.RecordSagaFailed(typeof(TSaga).Name);
            return Result<Guid>.Failure(startResult.Error);
        }
        
        var saveResult = await _repository.SaveAsync(saga, cancellationToken);
        if (saveResult.IsFailure)
        {
            _metrics.RecordSagaFailed(typeof(TSaga).Name);
            return Result<Guid>.Failure(saveResult.Error);
        }
        
        await ProcessSagaMessagesAsync(saga, cancellationToken);
        
        _metrics.RecordSagaStarted(typeof(TSaga).Name);
        
        return Result<Guid>.Success(saga.SagaId);
    }
    
    public async Task<Result<Unit>> HandleMessageAsync<TSaga, TState, TMessage>(
        Guid sagaId,
        TMessage message,
        CancellationToken cancellationToken = default)
        where TSaga : ISaga<TState>
        where TState : ISagaState
        where TMessage : class
    {
        using var activity = Activity.StartActivity("SagaManager.HandleMessage");
        activity?.SetTag("saga.type", typeof(TSaga).Name);
        activity?.SetTag("saga.id", sagaId);
        activity?.SetTag("message.type", typeof(TMessage).Name);
        
        var loadResult = await _repository.LoadAsync<TSaga, TState>(sagaId, cancellationToken);
        if (loadResult.IsFailure)
            return Result<Unit>.Failure(loadResult.Error);
        
        if (loadResult.Value.IsNone)
            return Result<Unit>.Failure(Error.NotFound(
                "SagaManager",
                $"Saga {sagaId} not found"));
        
        var saga = loadResult.Value.Value;
        
        var handleResult = await saga.HandleAsync(message, cancellationToken);
        if (handleResult.IsFailure)
        {
            _metrics.RecordSagaStepFailed(typeof(TSaga).Name);
            saga.Status = SagaStatus.Failed;
            saga.FailureReason = handleResult.Error.Message;
        }
        
        var saveResult = await _repository.SaveAsync(saga, cancellationToken);
        if (saveResult.IsFailure)
            return Result<Unit>.Failure(saveResult.Error);
        
        await ProcessSagaMessagesAsync(saga, cancellationToken);
        
        if (saga.Status == SagaStatus.Completed)
        {
            _metrics.RecordSagaCompleted(typeof(TSaga).Name);
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    private async Task ProcessSagaMessagesAsync<TState>(
        ISaga<TState> saga,
        CancellationToken cancellationToken)
        where TState : ISagaState
    {
        // Process commands
        foreach (var command in saga.GetPendingCommands())
        {
            await _mediator.Send(command, cancellationToken);
        }
        
        // Process events
        foreach (var @event in saga.GetPendingEvents())
        {
            await _eventPublisher.PublishAsync(@event, cancellationToken);
        }
        
        saga.ClearPendingMessages();
    }
}
```

---

## Migration Strategy

### Incremental Migration Path

1. **Phase 1 (Day 1)**: Create CQRS abstractions alongside existing code
2. **Phase 2 (Days 2-3)**: Add pipeline behaviors one by one
3. **Phase 3 (Days 4-5)**: Migrate event handling to new structure
4. **Phase 4 (Day 6)**: Integrate validation framework
5. **Phase 5 (Days 7-8)**: Add saga support for complex workflows

### Compatibility Bridge

```csharp
// Application/Compatibility/LegacyCommandAdapter.cs
public class LegacyCommandAdapter<TLegacyCommand, TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    
    public async Task<TResponse> HandleLegacyCommand(TLegacyCommand legacyCommand)
    {
        var command = _mapper.Map<TCommand>(legacyCommand);
        var result = await _mediator.Send(command);
        
        if (result.IsFailure)
            throw new ApplicationException(result.Error.Message);
        
        return result.Value;
    }
}
```

---

## Testing Strategy

### Unit Test Template

```csharp
// Tests/Application/CommandHandlerTests.cs
public class CommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ILogger<TestCommandHandler>> _logger;
    private readonly TestCommandHandler _handler;
    
    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command = new TestCommand { Value = "test" };
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(1));
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
    
    [Fact]
    public async Task Handle_SaveFails_ReturnsFailure()
    {
        // Arrange
        var command = new TestCommand { Value = "test" };
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure(Error.Persistence("Save", "Failed")));
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Persistence);
    }
}
```

---

## Success Metrics

### Technical Metrics
- ✅ All handlers use Result<T> pattern
- ✅ Complete CQRS separation
- ✅ All behaviors properly chained
- ✅ Event handling fully async
- ✅ Saga orchestration operational

### Performance Metrics
- ✅ Command execution < 100ms p99
- ✅ Query execution < 50ms p99
- ✅ Event processing < 20ms p99
- ✅ Validation overhead < 5ms

### Quality Metrics
- ✅ Test coverage > 90%
- ✅ Zero uncaught exceptions
- ✅ Complete telemetry coverage
- ✅ All operations logged

---

## Conclusion

This Application layer refactoring guide provides a complete transformation to a functional, CQRS-based architecture with comprehensive pipeline behaviors, event handling, and saga support. The phased approach ensures safe migration while maintaining compatibility.