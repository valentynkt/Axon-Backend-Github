# 🧱 Axon Backend - Building Blocks Reference Guide

## Table of Contents
- [Overview](#overview)
- [Core Building Blocks](#core-building-blocks)
- [CQRS Infrastructure](#cqrs-infrastructure)
- [Domain Modeling](#domain-modeling)
- [Persistence Layer](#persistence-layer)
- [Web Infrastructure](#web-infrastructure)
- [Validation Framework](#validation-framework)
- [Error Handling](#error-handling)
- [Event System](#event-system)
- [Caching Infrastructure](#caching-infrastructure)
- [Health Monitoring](#health-monitoring)
- [Security Components](#security-components)
- [Observability](#observability)
- [Testing Infrastructure](#testing-infrastructure)
- [Usage Examples](#usage-examples)
- [Extension Points](#extension-points)

## Overview

Building Blocks provide the foundational components and patterns shared across all modules in the Axon Backend system. They implement cross-cutting concerns and enforce architectural consistency.

### Design Principles

1. **Reusability**: Components usable across all modules
2. **Consistency**: Standardized patterns and interfaces
3. **Isolation**: No business logic, only infrastructure
4. **Extensibility**: Easy to extend without modification
5. **Performance**: Optimized for high-throughput scenarios

### Package Structure

```
src/BuildingBlocks/
├── Core/                        # Core abstractions and patterns
│   ├── CQRS/                   # Command/Query infrastructure
│   ├── Model/                  # Base entities and aggregates
│   ├── Event/                  # Event system
│   ├── Results/                # Result pattern implementation
│   └── Pagination/             # Pagination support
├── Persistence/                # Data access patterns
│   ├── EfCore/                # Entity Framework extensions
│   ├── Repositories/          # Repository base classes
│   └── Specifications/        # Specification pattern
├── Web/                       # Web API infrastructure
│   ├── Endpoints/            # FastEndpoints base classes
│   ├── Middleware/           # Custom middleware
│   └── Filters/              # Action filters
├── Validation/               # Validation framework
├── Caching/                  # Caching abstractions
├── Logging/                  # Structured logging
├── Exception/                # Exception handling
├── HealthCheck/              # Health monitoring
├── OpenTelemetry/            # Observability
└── TestBase/                 # Test infrastructure
```

## Core Building Blocks

### Base Entity

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for all domain entities
/// Provides identity, soft deletion, and versioning
/// </summary>
public abstract record BaseEntity<TId> : IEntity<TId> 
    where TId : struct, IEquatable<TId>
{
    public TId? Id { get; init; }
    public bool IsDeleted { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Auditable entity with creation/modification tracking
/// </summary>
public abstract record BaseAuditableEntity<TId> : BaseEntity<TId>, IAuditableEntity<TId>
    where TId : struct, IEquatable<TId>
{
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? LastModified { get; set; }
    public long? LastModifiedBy { get; set; }
}

// Usage in modules
public sealed class Message : BaseEntity<MessageId>
{
    public MessageContent Content { get; init; }
    public MessageRole Role { get; init; }
    // Domain-specific properties
}
```

### Base Aggregate

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base aggregate root with domain event support
/// </summary>
public abstract record BaseAggregate<TId> : BaseAuditableEntity<TId>, IAggregate<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
    
    public IEvent[] ClearDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
}

// Usage example
public sealed class Conversation : BaseAggregate<ConversationId>
{
    public static Result<Conversation> Create(UserId userId, string title)
    {
        var conversation = new Conversation
        {
            Id = ConversationId.New(),
            UserId = userId,
            Title = title
        };
        
        conversation.AddDomainEvent(new ConversationStartedDomainEvent(conversation.Id));
        
        return Result.Success(conversation);
    }
}
```

### Strong Type IDs

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Interface for strongly-typed identifiers
/// </summary>
public interface IStrongId<TValue> where TValue : notnull
{
    TValue Value { get; }
}

/// <summary>
/// Base implementation for strong IDs
/// </summary>
public abstract record StrongId<TValue>(TValue Value) : IStrongId<TValue>
    where TValue : notnull
{
    public override string ToString() => Value.ToString() ?? string.Empty;
    
    public static implicit operator TValue(StrongId<TValue> id) => id.Value;
}

// JSON converter for strong IDs
public class StrongIdJsonConverter<TStrongId, TValue> : JsonConverter<TStrongId>
    where TStrongId : IStrongId<TValue>
    where TValue : notnull
{
    public override TStrongId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;
            
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        return value is null ? default : CreateInstance(value);
    }
    
    public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Value, options);
    }
    
    private static TStrongId CreateInstance(TValue value)
    {
        return (TStrongId)Activator.CreateInstance(typeof(TStrongId), value)!;
    }
}

// Usage
public sealed class ConversationId : StrongId<Guid>
{
    public ConversationId(Guid value) : base(value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ConversationId cannot be empty");
    }
    
    public static ConversationId New() => new(Guid.NewGuid());
    public static ConversationId From(Guid value) => new(value);
}
```

## CQRS Infrastructure

### Command Infrastructure

```csharp
namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base interface for commands
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
    where TResponse : notnull
{
}

/// <summary>
/// Base interface for command handlers
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}

/// <summary>
/// Base command with common properties
/// </summary>
public abstract record CommandBase : ICommand<Unit>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Command handler base with common functionality
public abstract class CommandHandlerBase<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    protected readonly ILogger<CommandHandlerBase<TCommand, TResponse>> Logger;
    protected readonly IUnitOfWork UnitOfWork;
    
    protected CommandHandlerBase(ILogger<CommandHandlerBase<TCommand, TResponse>> logger, IUnitOfWork unitOfWork)
    {
        Logger = logger;
        UnitOfWork = unitOfWork;
    }
    
    public async Task<Result<TResponse>> Handle(TCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Handling command {CommandName}", typeof(TCommand).Name);
            
            var result = await HandleCommand(request, cancellationToken);
            
            if (result.IsSuccess)
            {
                await UnitOfWork.SaveChangesAsync(cancellationToken);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling command {CommandName}", typeof(TCommand).Name);
            return Result.Failure<TResponse>(Error.FromException(ex));
        }
    }
    
    protected abstract Task<Result<TResponse>> HandleCommand(TCommand request, CancellationToken cancellationToken);
}
```

### Query Infrastructure

```csharp
namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base interface for queries
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    where TResponse : notnull
{
}

/// <summary>
/// Base interface for query handlers
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}

/// <summary>
/// Base query with pagination support
/// </summary>
public abstract record QueryBase<TResponse> : IQuery<TResponse>
    where TResponse : notnull
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
    public string? OrderBy { get; init; }
    public bool? OrderDescending { get; init; }
}

// Paged query response
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

// Query handler with caching
public abstract class CachedQueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachedQueryHandler<TQuery, TResponse>> _logger;
    
    protected abstract string GetCacheKey(TQuery query);
    protected abstract TimeSpan GetCacheDuration();
    
    public async Task<Result<TResponse>> Handle(TQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(request);
        
        // Try get from cache
        var cached = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return Result.Success(cached);
        }
        
        // Execute query
        var result = await ExecuteQuery(request, cancellationToken);
        
        // Cache successful results
        if (result.IsSuccess)
        {
            await _cache.SetAsync(cacheKey, result.Value, GetCacheDuration(), cancellationToken);
        }
        
        return result;
    }
    
    protected abstract Task<Result<TResponse>> ExecuteQuery(TQuery request, CancellationToken cancellationToken);
}
```

### Pipeline Behaviors

```csharp
namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Validation pipeline behavior
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();
            
        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();
            
        if (failures.Count != 0)
            throw new ValidationException(failures);
            
        return await next();
    }
}

/// <summary>
/// Logging pipeline behavior
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUser.UserId;
        
        _logger.LogInformation(
            "Handling {RequestName} for user {UserId} with data {@Request}",
            requestName, userId, request);
            
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName, stopwatch.ElapsedMilliseconds);
                
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName, stopwatch.ElapsedMilliseconds);
                
            throw;
        }
    }
}

/// <summary>
/// Transaction pipeline behavior
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip for queries
        if (request is IQuery<TResponse>)
            return await next();
            
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation("Beginning transaction for {RequestName}", requestName);
        
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var response = await next();
            
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Committed transaction for {RequestName}", requestName);
            
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            
            _logger.LogWarning("Rolled back transaction for {RequestName}", requestName);
            
            throw;
        }
    }
}
```

## Domain Modeling

### Value Objects

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base class for value objects
/// </summary>
public abstract record ValueObject
{
    protected static bool EqualOperator(ValueObject? left, ValueObject? right)
    {
        if (left is null ^ right is null)
            return false;
            
        return left?.Equals(right) ?? true;
    }
    
    protected static bool NotEqualOperator(ValueObject? left, ValueObject? right)
    {
        return !EqualOperator(left, right);
    }
}

// Common value objects
public sealed record Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
    public string Value { get; }
    
    private Email(string value) => Value = value;
    
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Email>(Error.Validation("Email is required"));
            
        if (!EmailRegex.IsMatch(value))
            return Result.Failure<Email>(Error.Validation("Invalid email format"));
            
        return Result.Success(new Email(value.ToLowerInvariant()));
    }
    
    public static implicit operator string(Email email) => email.Value;
}

public sealed record PhoneNumber : ValueObject
{
    private static readonly Regex PhoneRegex = new(
        @"^\+?[1-9]\d{1,14}$",
        RegexOptions.Compiled);
        
    public string Value { get; }
    
    private PhoneNumber(string value) => Value = value;
    
    public static Result<PhoneNumber> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<PhoneNumber>(Error.Validation("Phone number is required"));
            
        var normalized = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        
        if (!PhoneRegex.IsMatch(normalized))
            return Result.Failure<PhoneNumber>(Error.Validation("Invalid phone number"));
            
        return Result.Success(new PhoneNumber(normalized));
    }
}

public sealed record Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
    
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
            return Result.Failure<Money>(Error.Validation("Amount cannot be negative"));
            
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result.Failure<Money>(Error.Validation("Invalid currency code"));
            
        return Result.Success(new Money(amount, currency.ToUpperInvariant()));
    }
    
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
            
        return new Money(Amount + other.Amount, Currency);
    }
    
    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");
            
        return new Money(Amount - other.Amount, Currency);
    }
}
```

### Specifications

```csharp
namespace BuildingBlocks.Core.Specifications;

/// <summary>
/// Specification pattern for complex queries
/// </summary>
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    
    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }
    
    public Specification<T> And(Specification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }
    
    public Specification<T> Or(Specification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }
    
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}

// Composite specifications
public sealed class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;
    
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }
    
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();
        
        var parameter = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpression, parameter),
            Expression.Invoke(rightExpression, parameter));
            
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

// Usage example
public class ActiveConversationSpecification : Specification<Conversation>
{
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return c => c.State == ConversationState.Active && !c.IsDeleted;
    }
}

public class UserConversationSpecification : Specification<Conversation>
{
    private readonly UserId _userId;
    
    public UserConversationSpecification(UserId userId)
    {
        _userId = userId;
    }
    
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return c => c.UserId == _userId;
    }
}

// Combine specifications
var spec = new ActiveConversationSpecification()
    .And(new UserConversationSpecification(userId));
    
var conversations = await repository.FindAsync(spec);
```

## Persistence Layer

### Repository Base

```csharp
namespace BuildingBlocks.Persistence;

/// <summary>
/// Generic repository interface
/// </summary>
public interface IRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(Specification<TEntity> specification, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Specification<TEntity>? specification = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core repository implementation
/// </summary>
public abstract class EfRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    
    protected EfRepository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }
    
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(e => e.Id!.Value.Equals(id), cancellationToken);
    }
    
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => !e.IsDeleted)
            .ToListAsync(cancellationToken);
    }
    
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Specification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(specification.ToExpression())
            .ToListAsync(cancellationToken);
    }
    
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }
    
    public virtual void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }
    
    public virtual void Remove(TEntity entity)
    {
        entity.IsDeleted = true;
        Update(entity);
    }
    
    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(e => e.Id!.Value.Equals(id) && !e.IsDeleted, cancellationToken);
    }
    
    public virtual async Task<int> CountAsync(
        Specification<TEntity>? specification = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(e => !e.IsDeleted);
        
        if (specification != null)
            query = query.Where(specification.ToExpression());
            
        return await query.CountAsync(cancellationToken);
    }
}
```

### Unit of Work

```csharp
namespace BuildingBlocks.Persistence;

/// <summary>
/// Unit of Work pattern interface
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    bool HasChanges { get; }
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core Unit of Work implementation
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<EfUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;
    
    public EfUnitOfWork(
        DbContext context,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<EfUnitOfWork> logger)
    {
        _context = context;
        _domainEventDispatcher = domainEventDispatcher;
        _logger = logger;
    }
    
    public bool HasChanges => _context.ChangeTracker.HasChanges();
    
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events before saving
        await DispatchDomainEvents(cancellationToken);
        
        // Update audit fields
        UpdateAuditableEntities();
        
        // Save changes
        var result = await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Saved {Count} entities", result);
        
        return result;
    }
    
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transaction already in progress");
            
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        _logger.LogDebug("Transaction started");
        
        return _currentTransaction;
    }
    
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No transaction in progress");
            
        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
            
            _logger.LogDebug("Transaction committed");
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
    
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            return;
            
        await _currentTransaction.RollbackAsync(cancellationToken);
        
        _logger.LogDebug("Transaction rolled back");
        
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }
    
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        
        try
        {
            var result = await operation();
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
    
    private async Task DispatchDomainEvents(CancellationToken cancellationToken)
    {
        var aggregates = _context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
            
        var domainEvents = aggregates
            .SelectMany(a => a.ClearDomainEvents())
            .ToList();
            
        foreach (var domainEvent in domainEvents)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
    }
    
    private void UpdateAuditableEntities()
    {
        var entries = _context.ChangeTracker
            .Entries<IAuditable>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
            
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = GetCurrentUserId();
            }
            
            entry.Entity.LastModified = DateTime.UtcNow;
            entry.Entity.LastModifiedBy = GetCurrentUserId();
        }
    }
    
    private long? GetCurrentUserId()
    {
        // Get from current user service
        return null;
    }
    
    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
```

### Database Interceptors

```csharp
namespace BuildingBlocks.Persistence.Interceptors;

/// <summary>
/// Audit interceptor for automatic audit field population
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    
    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }
    
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }
    
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    
    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;
        
        var entries = context.ChangeTracker
            .Entries<IAuditable>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
            
        var userId = _currentUserService.UserId;
        var now = _dateTimeProvider.UtcNow;
        
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
            }
            
            entry.Entity.LastModified = now;
            entry.Entity.LastModifiedBy = userId;
        }
    }
}

/// <summary>
/// Domain event interceptor for dispatching events
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    
    public DomainEventInterceptor(IDomainEventDispatcher domainEventDispatcher)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }
    
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    
    private async Task DispatchDomainEvents(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null) return;
        
        var aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
            
        var domainEvents = aggregates
            .SelectMany(a => a.ClearDomainEvents())
            .ToList();
            
        foreach (var domainEvent in domainEvents)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
    }
}
```

## Web Infrastructure

### FastEndpoints Base

```csharp
namespace BuildingBlocks.Web.Endpoints;

/// <summary>
/// Base endpoint with common functionality
/// </summary>
public abstract class EndpointBase<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected IMediator Mediator => Resolve<IMediator>();
    protected ILogger Logger => Resolve<ILogger<EndpointBase<TRequest, TResponse>>>();
    protected ICurrentUserService CurrentUser => Resolve<ICurrentUserService>();
    
    protected async Task<IResult> HandleCommand<TCommand, TCommandResponse>(TCommand command)
        where TCommand : ICommand<TCommandResponse>
        where TCommandResponse : notnull
    {
        var result = await Mediator.Send(command);
        return result.ToHttpResult();
    }
    
    protected async Task<IResult> HandleQuery<TQuery, TQueryResponse>(TQuery query)
        where TQuery : IQuery<TQueryResponse>
        where TQueryResponse : notnull
    {
        var result = await Mediator.Send(query);
        return result.ToHttpResult();
    }
}

/// <summary>
/// Paged endpoint with built-in pagination
/// </summary>
public abstract class PagedEndpoint<TRequest, TResponse> : EndpointBase<TRequest, PagedResult<TResponse>>
    where TRequest : IPagedRequest
{
    protected override void OnBeforeValidate(TRequest req)
    {
        // Set default pagination values
        req.PageNumber ??= 1;
        req.PageSize ??= 20;
        
        // Enforce limits
        if (req.PageSize > 100)
            req.PageSize = 100;
    }
}

/// <summary>
/// Secured endpoint with authorization
/// </summary>
public abstract class SecuredEndpoint<TRequest, TResponse> : EndpointBase<TRequest, TResponse>
    where TRequest : notnull
{
    protected abstract string[] RequiredPolicies { get; }
    
    public override void Configure()
    {
        ConfigureRoute();
        Policies(RequiredPolicies);
        ConfigureOpenApi();
    }
    
    protected abstract void ConfigureRoute();
    protected abstract void ConfigureOpenApi();
}
```

### Middleware

```csharp
namespace BuildingBlocks.Web.Middleware;

/// <summary>
/// Correlation ID middleware
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-Id", correlationId);
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
    
    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            return correlationId!;
        }
        
        return Guid.NewGuid().ToString();
    }
}

/// <summary>
/// Exception handling middleware
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");
        
        var (statusCode, error) = exception switch
        {
            ValidationException validationEx => (400, CreateValidationError(validationEx)),
            DomainException domainEx => (400, Error.FromException(domainEx)),
            NotFoundException notFoundEx => (404, Error.FromException(notFoundEx)),
            UnauthorizedException unauthorizedEx => (401, Error.FromException(unauthorizedEx)),
            ForbiddenException forbiddenEx => (403, Error.FromException(forbiddenEx)),
            _ => (500, Error.Internal("An error occurred while processing your request"))
        };
        
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = error.Message,
            Status = statusCode,
            Detail = _environment.IsDevelopment() ? exception.ToString() : null,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
                ["correlationId"] = context.Items["CorrelationId"]
            }
        };
        
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
    
    private static Error CreateValidationError(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            
        return Error.Validation("Validation failed", errors);
    }
}
```

## Validation Framework

### FluentValidation Extensions

```csharp
namespace BuildingBlocks.Validation;

/// <summary>
/// Common validation rules
/// </summary>
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email too long");
    }
    
    public static IRuleBuilderOptions<T, string> ValidPhoneNumber<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format");
    }
    
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain digit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain special character");
    }
    
    public static IRuleBuilderOptions<T, Guid> ValidGuid<T>(this IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .NotEqual(Guid.Empty).WithMessage("Invalid identifier");
    }
    
    public static IRuleBuilderOptions<T, TProperty?> RequiredWhen<T, TProperty>(
        this IRuleBuilder<T, TProperty?> ruleBuilder,
        Func<T, bool> predicate,
        string message = "Field is required")
    {
        return ruleBuilder
            .Must((root, value, context) =>
            {
                if (predicate(root))
                    return value != null;
                return true;
            })
            .WithMessage(message);
    }
}

/// <summary>
/// Async validation with dependency injection
/// </summary>
public abstract class AsyncValidator<T> : AbstractValidator<T>
{
    protected IServiceProvider ServiceProvider { get; }
    
    protected AsyncValidator(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }
    
    protected async Task<bool> ExistsAsync<TEntity, TId>(TId id, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TId>
        where TId : struct
    {
        using var scope = ServiceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<TEntity, TId>>();
        return await repository.ExistsAsync(id, cancellationToken);
    }
    
    protected async Task<bool> UniqueAsync<TEntity>(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        return !await context.Set<TEntity>().AnyAsync(predicate, cancellationToken);
    }
}
```

## Error Handling

### Result Pattern

```csharp
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Result pattern for explicit error handling
/// </summary>
public class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    
    protected Result(T? value, Error? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }
    
    public static Result<T> Success(T value) => new(value, null, true);
    public static Result<T> Failure(Error error) => new(default, error, false);
    
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
    
    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess,
        Func<Error, Task<TResult>> onFailure)
    {
        return IsSuccess ? await onSuccess(Value!) : await onFailure(Error!);
    }
    
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess 
            ? Result<TNew>.Success(mapper(Value!))
            : Result<TNew>.Failure(Error!);
    }
    
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        return IsSuccess 
            ? Result<TNew>.Success(await mapper(Value!))
            : Result<TNew>.Failure(Error!);
    }
    
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
            action(Value!);
        return this;
    }
    
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        if (IsSuccess)
            await action(Value!);
        return this;
    }
}

/// <summary>
/// Result without value
/// </summary>
public class Result : Result<Unit>
{
    protected Result(Error? error, bool isSuccess) : base(Unit.Value, error, isSuccess)
    {
    }
    
    public static Result Success() => new(null, true);
    public static new Result Failure(Error error) => new(error, false);
}

/// <summary>
/// Error representation
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static Error None => new(string.Empty, string.Empty, ErrorType.None);
    public static Error Validation(string message) => new("Validation", message, ErrorType.Validation);
    public static Error NotFound(string message) => new("NotFound", message, ErrorType.NotFound);
    public static Error Unauthorized(string message) => new("Unauthorized", message, ErrorType.Unauthorized);
    public static Error Forbidden(string message) => new("Forbidden", message, ErrorType.Forbidden);
    public static Error Conflict(string message) => new("Conflict", message, ErrorType.Conflict);
    public static Error Internal(string message) => new("Internal", message, ErrorType.Internal);
    public static Error FromException(Exception exception) => new(exception.GetType().Name, exception.Message, ErrorType.Failure);
}

public enum ErrorType
{
    None,
    Failure,
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict,
    Internal
}
```

### HTTP Result Extensions

```csharp
namespace BuildingBlocks.Web.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.Match(
            onSuccess: value => Results.Ok(value),
            onFailure: error => error.Type switch
            {
                ErrorType.NotFound => Results.NotFound(error.ToProblemDetails()),
                ErrorType.Validation => Results.BadRequest(error.ToProblemDetails()),
                ErrorType.Unauthorized => Results.Unauthorized(),
                ErrorType.Forbidden => Results.Forbid(),
                ErrorType.Conflict => Results.Conflict(error.ToProblemDetails()),
                _ => Results.Problem(error.ToProblemDetails())
            });
    }
    
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            ErrorType.Conflict => 409,
            _ => 500
        };
        
        return new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = error.Code,
            Status = statusCode,
            Detail = error.Message
        };
    }
}
```

## Event System

### Domain Events

```csharp
namespace BuildingBlocks.Core.Event;

/// <summary>
/// Domain event interface
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base domain event
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event dispatcher
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>
/// MediatR-based domain event dispatcher
/// </summary>
public class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatRDomainEventDispatcher> _logger;
    
    public MediatRDomainEventDispatcher(IMediator mediator, ILogger<MediatRDomainEventDispatcher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dispatching domain event {EventType}", domainEvent.GetType().Name);
        
        await _mediator.Publish(domainEvent, cancellationToken);
    }
    
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, cancellationToken);
        }
    }
}
```

### Integration Events

```csharp
namespace BuildingBlocks.Core.Event;

/// <summary>
/// Integration event for cross-module communication
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base integration event
/// </summary>
public abstract record IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Event bus interface
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IIntegrationEvent;
    Task SubscribeAsync<T, TH>(CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
        where TH : IIntegrationEventHandler<T>;
}

/// <summary>
/// Integration event handler
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outbox pattern for reliable messaging
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}
```

## Caching Infrastructure

### Cache Service

```csharp
namespace BuildingBlocks.Caching;

/// <summary>
/// Cache service interface
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Multi-level cache implementation
/// </summary>
public class MultiLevelCacheService : ICacheService
{
    private readonly IMemoryCache _l1Cache;
    private readonly IDistributedCache _l2Cache;
    private readonly ILogger<MultiLevelCacheService> _logger;
    
    public MultiLevelCacheService(
        IMemoryCache l1Cache,
        IDistributedCache l2Cache,
        ILogger<MultiLevelCacheService> logger)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _logger = logger;
    }
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Try L1 cache
        if (_l1Cache.TryGetValue<T>(key, out var value))
        {
            _logger.LogDebug("L1 cache hit for key {Key}", key);
            return value;
        }
        
        // Try L2 cache
        var bytes = await _l2Cache.GetAsync(key, cancellationToken);
        if (bytes != null)
        {
            _logger.LogDebug("L2 cache hit for key {Key}", key);
            
            value = JsonSerializer.Deserialize<T>(bytes);
            if (value != null)
            {
                // Populate L1 cache
                _l1Cache.Set(key, value, TimeSpan.FromMinutes(5));
            }
            
            return value;
        }
        
        _logger.LogDebug("Cache miss for key {Key}", key);
        return default;
    }
    
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var exp = expiration ?? TimeSpan.FromHours(1);
        
        // Set in L1 cache
        _l1Cache.Set(key, value, TimeSpan.FromMinutes(5));
        
        // Set in L2 cache
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await _l2Cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = exp
        }, cancellationToken);
        
        _logger.LogDebug("Cached value for key {Key} with expiration {Expiration}", key, exp);
    }
    
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _l1Cache.Remove(key);
        await _l2Cache.RemoveAsync(key, cancellationToken);
        
        _logger.LogDebug("Removed cache entry for key {Key}", key);
    }
    
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var value = await GetAsync<T>(key, cancellationToken);
        if (value != null)
            return value;
            
        value = await factory();
        await SetAsync(key, value, expiration, cancellationToken);
        
        return value;
    }
}
```

## Usage Examples

### Creating a New Module

```csharp
// 1. Define domain model
namespace Axon.Modules.Orders.Domain;

public sealed class Order : BaseAggregate<OrderId>
{
    private readonly List<OrderLine> _lines = new();
    
    public CustomerId CustomerId { get; private init; }
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    
    public static Result<Order> Create(CustomerId customerId)
    {
        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            TotalAmount = Money.Zero("USD"),
            Status = OrderStatus.Draft
        };
        
        order.AddDomainEvent(new OrderCreatedDomainEvent(order.Id));
        
        return Result.Success(order);
    }
    
    public Result AddLine(ProductId productId, int quantity, Money price)
    {
        if (Status != OrderStatus.Draft)
            return Result.Failure(OrderErrors.CannotModifyOrder);
            
        var line = OrderLine.Create(productId, quantity, price);
        _lines.Add(line);
        
        RecalculateTotal();
        
        return Result.Success();
    }
    
    private void RecalculateTotal()
    {
        TotalAmount = _lines
            .Select(l => l.Price.Multiply(l.Quantity))
            .Aggregate(Money.Zero("USD"), (acc, m) => acc.Add(m));
    }
}

// 2. Create command
public sealed record PlaceOrderCommand(
    Guid OrderId
) : ICommand<PlaceOrderResponse>;

// 3. Create handler
public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, PlaceOrderResponse>
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result<PlaceOrderResponse>> Handle(
        PlaceOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure<PlaceOrderResponse>(OrderErrors.NotFound);
            
        var result = order.Place();
        if (result.IsFailure)
            return Result.Failure<PlaceOrderResponse>(result.Error);
            
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success(new PlaceOrderResponse(order.Id));
    }
}

// 4. Create endpoint
public sealed class PlaceOrderEndpoint : EndpointBase<PlaceOrderRequest, PlaceOrderResponse>
{
    public override void Configure()
    {
        Post("/api/orders/{id}/place");
        Policies("RequireAuthenticatedUser");
    }
    
    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var command = new PlaceOrderCommand(req.OrderId);
        var result = await HandleCommand<PlaceOrderCommand, PlaceOrderResponse>(command);
        await SendResultAsync(result);
    }
}
```

### Using Specifications

```csharp
// Define specifications
public class ActiveOrderSpecification : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.Status == OrderStatus.Active && !order.IsDeleted;
    }
}

public class CustomerOrderSpecification : Specification<Order>
{
    private readonly CustomerId _customerId;
    
    public CustomerOrderSpecification(CustomerId customerId)
    {
        _customerId = customerId;
    }
    
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.CustomerId == _customerId;
    }
}

public class DateRangeOrderSpecification : Specification<Order>
{
    private readonly DateTime _from;
    private readonly DateTime _to;
    
    public DateRangeOrderSpecification(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
    }
    
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.CreatedAt >= _from && order.CreatedAt <= _to;
    }
}

// Use specifications
var activeCustomerOrders = new ActiveOrderSpecification()
    .And(new CustomerOrderSpecification(customerId))
    .And(new DateRangeOrderSpecification(startDate, endDate));
    
var orders = await repository.FindAsync(activeCustomerOrders);
```

### Implementing Caching

```csharp
public class CachedOrderQueryHandler : CachedQueryHandler<GetOrderQuery, OrderDto>
{
    private readonly IOrderRepository _repository;
    
    protected override string GetCacheKey(GetOrderQuery query)
    {
        return $"order:{query.OrderId}";
    }
    
    protected override TimeSpan GetCacheDuration()
    {
        return TimeSpan.FromMinutes(15);
    }
    
    protected override async Task<Result<OrderDto>> ExecuteQuery(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);
        
        if (order is null)
            return Result.Failure<OrderDto>(OrderErrors.NotFound);
            
        return Result.Success(order.ToDto());
    }
}
```

## Extension Points

### Creating Custom Pipeline Behaviors

```csharp
public class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMetricsService _metrics;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        using var timer = _metrics.StartTimer($"handler_{requestName}");
        
        try
        {
            var response = await next();
            _metrics.IncrementCounter($"handler_{requestName}_success");
            return response;
        }
        catch
        {
            _metrics.IncrementCounter($"handler_{requestName}_failure");
            throw;
        }
    }
}
```

### Custom Validators

```csharp
public class UniqueEmailValidator : AsyncValidator<RegisterUserCommand>
{
    public UniqueEmailValidator(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        RuleFor(x => x.Email)
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email already exists");
    }
    
    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        return await UniqueAsync<User>(u => u.Email == email, cancellationToken);
    }
}
```

### Custom Specifications

```csharp
public abstract class CompositeSpecification<T> : Specification<T>
{
    protected readonly List<Specification<T>> Specifications = new();
    
    public void Add(Specification<T> specification)
    {
        Specifications.Add(specification);
    }
    
    public override abstract Expression<Func<T, bool>> ToExpression();
}

public class AndCompositeSpecification<T> : CompositeSpecification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        if (!Specifications.Any())
            return _ => true;
            
        var expression = Specifications[0].ToExpression();
        
        for (int i = 1; i < Specifications.Count; i++)
        {
            expression = expression.And(Specifications[i].ToExpression());
        }
        
        return expression;
    }
}
```

---

*Last Updated: August 2025*
*Version: 1.0.0*