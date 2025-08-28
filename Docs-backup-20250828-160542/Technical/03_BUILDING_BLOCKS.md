# 🧱 Axon Backend - Building Blocks Comprehensive Reference Guide

## Table of Contents
- [Overview](#overview)
- [Architecture Philosophy](#architecture-philosophy)
- [Package Structure](#package-structure)
- [Core Building Blocks](#core-building-blocks)
  - [Domain Model Components](#domain-model-components)
  - [Strong Type IDs](#strong-type-ids)
  - [Entities and Aggregates](#entities-and-aggregates)
- [CQRS Infrastructure](#cqrs-infrastructure)
  - [Commands and Queries](#commands-and-queries)
  - [Handlers](#handlers)
  - [Pipeline Behaviors](#pipeline-behaviors)
- [Result Pattern](#result-pattern)
- [Persistence Layer](#persistence-layer)
  - [Repository Pattern](#repository-pattern)
  - [Unit of Work](#unit-of-work)
  - [Transaction Management](#transaction-management)
  - [Caching Strategy](#caching-strategy)
- [Event System](#event-system)
  - [Domain Events](#domain-events)
  - [Integration Events](#integration-events)
  - [Event Dispatching](#event-dispatching)
  - [Message Processing](#message-processing)
- [Web Infrastructure](#web-infrastructure)
  - [API Configuration](#api-configuration)
  - [Minimal APIs](#minimal-apis)
  - [Correlation Tracking](#correlation-tracking)
- [Validation Framework](#validation-framework)
- [Exception Handling](#exception-handling)
- [Observability](#observability)
  - [OpenTelemetry Integration](#opentelemetry-integration)
  - [Metrics and Tracing](#metrics-and-tracing)
  - [Health Checks](#health-checks)
- [Security](#security)
  - [JWT Authentication](#jwt-authentication)
  - [Authorization](#authorization)
- [External Integrations](#external-integrations)
  - [MassTransit](#masstransit)
  - [PostgreSQL](#postgresql)
- [Testing Infrastructure](#testing-infrastructure)
- [Configuration Management](#configuration-management)
- [Migration Guide](#migration-guide)
- [Best Practices](#best-practices)

## Overview

The Building Blocks layer provides the foundational infrastructure components that enable the Axon Backend's modular monolith architecture. These components implement cross-cutting concerns, enforce architectural patterns, and provide reusable abstractions that ensure consistency across all modules.

### Key Responsibilities

1. **Architectural Enforcement**: Implements Clean Architecture, CQRS, and DDD patterns
2. **Infrastructure Abstraction**: Provides technology-agnostic interfaces for persistence, messaging, and caching
3. **Cross-Cutting Concerns**: Handles logging, validation, exception handling, and observability
4. **Performance Optimization**: Implements caching, connection pooling, and query optimization
5. **Developer Experience**: Provides consistent APIs and reduces boilerplate code

## Architecture Philosophy

### Design Principles

1. **Separation of Concerns**
   - Clear boundaries between business logic and infrastructure
   - Technology-specific implementations hidden behind abstractions
   - Module independence through shared contracts

2. **Explicit Over Implicit**
   - Result pattern for explicit error handling
   - Strong typing with custom ID types
   - Clear transaction boundaries

3. **Performance First**
   - Multi-level caching strategy
   - Optimistic concurrency control
   - Connection pooling and query optimization

4. **Developer Productivity**
   - Convention over configuration
   - Minimal boilerplate through base classes
   - Comprehensive IntelliSense documentation

5. **Production Readiness**
   - Built-in observability with OpenTelemetry
   - Health checks and diagnostics
   - Graceful error handling and recovery

## Package Structure

```
src/BuildingBlocks/
├── Core/                           # Core domain abstractions
│   ├── CQRS/                      # Command/Query separation
│   │   ├── ICommand.cs           # Command interfaces
│   │   ├── IQuery.cs             # Query interfaces
│   │   ├── ICommandHandler.cs    # Handler contracts
│   │   ├── IQueryHandler.cs      
│   │   ├── CommandBase.cs        # Base implementations
│   │   └── QueryBase.cs          
│   ├── Model/                     # Domain model building blocks
│   │   ├── Entity.cs             # Base entity implementations
│   │   ├── Aggregate.cs          # Aggregate root support
│   │   ├── IEntity.cs           # Entity contracts
│   │   ├── IAggregate.cs        
│   │   ├── IAuditable.cs        # Audit trail interfaces
│   │   ├── ISoftDeletable.cs    # Soft delete support
│   │   ├── IVersion.cs          # Optimistic concurrency
│   │   ├── IStrongId.cs         # Strong typing for IDs
│   │   └── StrongIdJsonConverter.cs
│   ├── Event/                     # Event-driven architecture
│   │   ├── IDomainEvent.cs      # Domain event contracts
│   │   ├── IIntegrationEvent.cs # Integration events
│   │   ├── EventDispatcher.cs   # Event orchestration
│   │   ├── IEventMapper.cs      # Event transformation
│   │   └── MessageEnvelope.cs   # Message metadata
│   ├── Results/                   # Result pattern implementation
│   │   ├── Result.cs            # Core result type
│   │   ├── Error.cs             # Error representation
│   │   └── ResultExtensions.cs  # Functional extensions
│   └── Pagination/                # Pagination support
│       ├── PagedResult.cs       # Paged response model
│       ├── IPageQuery.cs        # Pagination interfaces
│       └── Extensions.cs        # LINQ extensions
├── Persistence/                    # Data access layer
│   ├── Write/                    # Write model (CQRS)
│   │   ├── EfWriteRepository.cs # EF Core write repository
│   │   ├── EfWriteUnitOfWork.cs # Transaction management
│   │   └── WriteDbContextBase.cs# Base DbContext for writes
│   ├── Read/                     # Read model (CQRS)
│   │   ├── EfReadRepository.cs  # Optimized read repository
│   │   ├── CachedReadRepository.cs # Cached reads
│   │   └── ReadDbContextBase.cs # Base DbContext for reads
│   ├── Common/                   # Shared persistence
│   │   ├── Interfaces/          # Repository contracts
│   │   ├── TransactionBehavior.cs # Transaction strategies
│   │   └── CacheManagerBase.cs  # Cache coordination
│   ├── Infrastructure/           # Persistence utilities
│   │   ├── SeedManager.cs       # Data seeding
│   │   ├── PersistenceHealthCheck.cs # DB health checks
│   │   └── PerformanceTracker.cs # Query performance
│   └── Caching/                  # Cache infrastructure
│       └── CachingRepositoryExtensions.cs
├── Web/                           # Web API infrastructure
│   ├── MinimalApiExtensions.cs  # Minimal API helpers
│   ├── ApiVersioningExtensions.cs # API versioning
│   ├── CorrelationExtensions.cs # Request correlation
│   ├── CurrentUserProvider.cs   # User context
│   └── EndpointConfig.cs        # Endpoint configuration
├── Validation/                    # Input validation
│   ├── ValidationBehavior.cs    # MediatR validation
│   ├── ValidationError.cs       # Error models
│   └── Extensions.cs            # FluentValidation helpers
├── Exception/                     # Exception handling
│   ├── DomainException.cs       # Business rule violations
│   ├── NotFoundException.cs     # Resource not found
│   ├── ValidationException.cs   # Validation failures
│   └── AppException.cs          # Application errors
├── Logging/                       # Structured logging
│   └── LoggingBehavior.cs       # Request/response logging
├── Caching/                       # Cache abstractions
│   ├── ICacheRequest.cs         # Cache markers
│   ├── CachingBehavior.cs       # Auto-caching
│   └── InvalidateCachingBehavior.cs # Cache invalidation
├── OpenTelemetryCollector/        # Observability
│   ├── Extensions.cs             # OTel configuration
│   ├── ObservabilityOptions.cs  # Settings
│   ├── Behaviors/                # Tracing behaviors
│   └── CoreDiagnostics/          # Metrics collection
├── MassTransit/                   # Message bus
│   ├── Extensions.cs             # Bus configuration
│   ├── RabbitMqOptions.cs       # RabbitMQ settings
│   └── ConsumeFilter.cs         # Message filters
├── Postgres/                      # PostgreSQL specific
│   ├── PostgresExtensions.cs    # PG configuration
│   ├── PostgresOptions.cs       # Connection settings
│   └── POSTGRES_COMPATIBILITY_GUIDE.md
├── PersistMessageProcessor/       # Outbox pattern
│   ├── PersistMessage.cs        # Message entity
│   ├── PersistMessageProcessor.cs # Processing logic
│   └── PersistMessageBackgroundService.cs
├── HealthCheck/                   # Health monitoring
│   ├── Extensions.cs             # Health check setup
│   └── HealthOptions.cs         # Configuration
├── Jwt/                          # JWT authentication
│   ├── JwtExtensions.cs         # JWT configuration
│   └── AuthHeaderHandler.cs     # Token validation
├── TestBase/                     # Testing support
│   ├── TestBase.cs              # Base test class
│   └── TestContainers.cs        # Container testing
├── OpenApi/                      # API documentation
│   ├── Extensions.cs             # Swagger setup
│   └── SecuritySchemeDocumentTransformer.cs
├── ProblemDetails/               # RFC 7807 support
│   └── Extensions.cs            # Problem details setup
├── Polly/                        # Resilience policies
│   └── Extensions.cs            # Retry/circuit breaker
├── Mapster/                      # Object mapping
│   └── Extensions.cs            # Mapping configuration
├── Utils/                        # Utilities
│   ├── ServiceLocator.cs       # Service resolution
│   └── TypeProvider.cs         # Type discovery
└── Constants/                    # Shared constants
    └── IdentityConstant.cs      # Identity claims

```

## Core Building Blocks

### Domain Model Components

#### Base Entity Implementation

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for domain entities with strongly-typed identifiers.
/// Implements core entity concerns: identity, versioning, soft deletion.
/// Uses record type for value equality and immutability benefits.
/// </summary>
/// <typeparam name="T">The type of the entity identifier (must be struct)</typeparam>
public abstract record BaseEntity<T> : IEntity<T> where T : struct, IEquatable<T>
{
    // IIdentifiable<T> implementation
    public T? Id { get; init; }
    
    // ISoftDeletable implementation - enables soft delete pattern
    public bool IsDeleted { get; set; }
    
    // IVersion implementation - for optimistic concurrency control
    public long Version { get; set; }
}

/// <summary>
/// Base implementation for auditable domain entities.
/// Extends BaseEntity with comprehensive audit trail capabilities.
/// Automatically populated by AuditSaveChangesInterceptor.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public abstract record BaseAuditableEntity<T> : BaseEntity<T>, IAuditableEntity<T> 
    where T : struct, IEquatable<T>
{
    // IAuditable implementation - tracks creation and modification
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }      // User ID who created
    public DateTime? LastModified { get; set; }
    public long? LastModifiedBy { get; set; } // User ID who last modified
}
```

#### Base Aggregate Implementation

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base aggregate root implementation with domain event support.
/// Aggregates are transaction boundaries and consistency enforcers.
/// Events are automatically dispatched when aggregate is persisted.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
public abstract record BaseAggregate<TId> : BaseAuditableEntity<TId>, IAggregate<TId> 
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    /// <summary>
    /// Gets the list of domain events that have occurred on this aggregate.
    /// Events are dispatched by the infrastructure after successful persistence.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to this aggregate.
    /// Events will be dispatched when the aggregate is saved via Unit of Work.
    /// Use this to signal important state changes that other parts of the system need to know about.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears and returns all domain events from this aggregate.
    /// Called by infrastructure after events are successfully dispatched.
    /// </summary>
    public IEvent[] ClearDomainEvents()
    {
        IEvent[] dequeuedEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return dequeuedEvents;
    }
}
```

### Strong Type IDs

The system uses strongly-typed identifiers to prevent primitive obsession and ensure type safety:

```csharp
namespace BuildingBlocks.Core.Model;

/// <summary>
/// Interface for strongly-typed identifiers.
/// Prevents accidental ID mixing between different entity types.
/// </summary>
public interface IStrongId<TValue> where TValue : notnull
{
    TValue Value { get; }
}

/// <summary>
/// JSON converter factory for automatic strong ID serialization.
/// Registered globally to handle all strong ID types transparently.
/// </summary>
public class StrongIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        var genericType = typeToConvert.GetGenericTypeDefinition();
        return genericType.GetInterfaces()
            .Any(i => i.IsGenericType && 
                     i.GetGenericTypeDefinition() == typeof(IStrongId<>));
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetInterfaces()
            .First(i => i.IsGenericType && 
                       i.GetGenericTypeDefinition() == typeof(IStrongId<>))
            .GetGenericArguments()[0];

        var converterType = typeof(StrongIdJsonConverter<,>)
            .MakeGenericType(typeToConvert, valueType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

// Usage Example in a Module:
public sealed record ConversationId : IStrongId<Guid>
{
    public Guid Value { get; }
    
    private ConversationId(Guid value) 
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ConversationId cannot be empty", nameof(value));
        Value = value;
    }
    
    public static ConversationId New() => new(Guid.NewGuid());
    public static ConversationId From(Guid value) => new(value);
    
    // Implicit conversion for convenience
    public static implicit operator Guid(ConversationId id) => id.Value;
    
    public override string ToString() => Value.ToString();
}
```

## CQRS Infrastructure

### Commands and Queries

The system implements a strict CQRS pattern with clear separation between commands (write operations) and queries (read operations):

```csharp
namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Marker interface for all requests in the system.
/// Provides a common base for cross-cutting concerns.
/// </summary>
public interface IAxonRequest<out TResponse> : IRequest<TResponse>
{
    // Marker interface for Axon-specific request handling
}

/// <summary>
/// Base interface for commands that modify state.
/// All commands return a Result<T> for explicit error handling.
/// Commands should be immutable and represent user intentions.
/// </summary>
public interface ICommand<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    // Commands represent intentions to change state
}

/// <summary>
/// Convenience interface for commands without return value.
/// Uses Unit type to represent void in a functional way.
/// </summary>
public interface ICommand : ICommand<Unit>
{
    // For commands that don't return a value
}

/// <summary>
/// Base interface for queries that read state.
/// Queries should be side-effect free and cacheable.
/// </summary>
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    // Queries are idempotent read operations
}

/// <summary>
/// Base command implementation with common metadata.
/// Provides correlation ID for distributed tracing.
/// </summary>
public abstract record CommandBase : RequestBase<Result<Unit>>, ICommand
{
    // Additional command-specific properties can be added here
}

/// <summary>
/// Base query implementation with pagination support.
/// Provides standard pagination parameters for list queries.
/// </summary>
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, IQuery<TResponse>
    where TResponse : notnull
{
    // Query-specific base implementation
}

/// <summary>
/// Base request with correlation and metadata support.
/// Used for tracking requests across service boundaries.
/// </summary>
public abstract record RequestBase<TResponse> : IRequest<TResponse>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

### Handlers

Command and query handlers process requests and contain the business logic:

```csharp
namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for command handlers.
/// Handlers contain the business logic for processing commands.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    // Command handler contract
}

/// <summary>
/// Convenience interface for command handlers without return value.
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
    // For handlers of commands without return values
}

/// <summary>
/// Interface for query handlers.
/// Query handlers should be optimized for read performance.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
    // Query handler contract
}
```

### Pipeline Behaviors

Pipeline behaviors provide cross-cutting concerns for all requests:

```csharp
namespace BuildingBlocks.Validation;

/// <summary>
/// Validation pipeline behavior using FluentValidation.
/// Automatically validates requests before they reach handlers.
/// Throws ValidationException with detailed error information.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IValidator<TRequest> _validator;

    public ValidationBehavior(IServiceProvider serviceProvider, IValidator<TRequest> validator)
    {
        _serviceProvider = serviceProvider;
        _validator = validator;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResult = await _validator.ValidateAsync(context, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(x => new ValidationError(x.PropertyName, x.ErrorMessage))
                .ToList();
                
            throw new ValidationException(errors);
        }

        return await next();
    }
}
```

```csharp
namespace BuildingBlocks.Logging;

/// <summary>
/// Logging pipeline behavior for request/response tracking.
/// Provides automatic logging with performance metrics.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        const string prefix = nameof(LoggingBehavior<TRequest, TResponse>);
        
        _logger.LogInformation(
            "[{Prefix}] Handling {RequestName}",
            prefix, typeof(TRequest).Name);
            
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();
        
        var timeTaken = stopwatch.Elapsed;
        if (timeTaken.Seconds > 3)
        {
            _logger.LogWarning(
                "[{Prefix}] Long running request {RequestName} ({TimeTaken} seconds)",
                prefix, typeof(TRequest).Name, timeTaken.Seconds);
        }
        else
        {
            _logger.LogInformation(
                "[{Prefix}] Handled {RequestName} in {TimeTaken}ms",
                prefix, typeof(TRequest).Name, timeTaken.Milliseconds);
        }

        return response;
    }
}
```

## Result Pattern

The Result pattern provides explicit error handling without exceptions:

```csharp
namespace BuildingBlocks.Core.Results;

/// <summary>
/// Represents the result of an operation with explicit success/failure states.
/// Eliminates the need for exceptions in business logic.
/// Supports functional composition and railway-oriented programming.
/// </summary>
public class Result<T> where T : notnull
{
    protected Result(T? value, Error? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    // Factory methods
    public static Result<T> Success(T value) => new(value, null, true);
    public static Result<T> Failure(Error error) => new(default, error, false);

    // Functional programming support
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) where TNew : notnull
    {
        return IsSuccess 
            ? Result<TNew>.Success(mapper(Value!))
            : Result<TNew>.Failure(Error!);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper) where TNew : notnull
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

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) where TNew : notnull
    {
        return IsSuccess ? binder(Value!) : Result<TNew>.Failure(Error!);
    }
}

/// <summary>
/// Error representation with categorization and metadata.
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
    
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

## Persistence Layer

### Repository Pattern

The system implements separate read and write repositories following CQRS:

```csharp
namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Write repository for command operations.
/// Optimized for transactional consistency and domain logic.
/// </summary>
public interface IWriteRepository<TEntity, in TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read repository for query operations.
/// Optimized for performance with caching and projections.
/// </summary>
public interface IReadRepository<TEntity, in TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<TEntity>> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default);
    IQueryable<TEntity> Query();
}
```

### Unit of Work

The Unit of Work pattern manages transactions and coordinates persistence:

```csharp
namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Unit of Work pattern for transaction management.
/// Coordinates changes across multiple aggregates.
/// Dispatches domain events after successful persistence.
/// </summary>
public interface IWriteUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes and dispatches domain events.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Begins an explicit database transaction.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation within a transaction.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic Unit of Work with context access.
/// </summary>
public interface IWriteUnitOfWork<out TWriteContext> : IWriteUnitOfWork 
    where TWriteContext : IWriteDbContext
{
    TWriteContext Context { get; }
}
```

### Transaction Management

The system supports multiple transaction strategies:

```csharp
namespace BuildingBlocks.Persistence.Common;

/// <summary>
/// Transaction behavior strategies for different consistency requirements.
/// </summary>
public enum TransactionBehavior
{
    /// <summary>
    /// No automatic transaction management.
    /// </summary>
    None,
    
    /// <summary>
    /// One transaction per HTTP request (web scenarios).
    /// </summary>
    PerRequest,
    
    /// <summary>
    /// One transaction per operation (default).
    /// </summary>
    PerOperation,
    
    /// <summary>
    /// Explicit transaction management by developer.
    /// </summary>
    Explicit
}

/// <summary>
/// Interface for transaction behavior handlers.
/// </summary>
public interface ITransactionBehaviorHandler
{
    Task<T> HandleAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);
        
    Task HandleAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);
        
    TransactionBehavior BehaviorType { get; }
}
```

### Caching Strategy

Multi-level caching with automatic invalidation:

```csharp
namespace BuildingBlocks.Caching;

/// <summary>
/// Marker interface for cacheable queries.
/// </summary>
public interface ICacheRequest
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}

/// <summary>
/// Marker interface for cache invalidation.
/// </summary>
public interface IInvalidateCacheRequest
{
    string[] CacheKeys { get; }
}

/// <summary>
/// Caching behavior for automatic query result caching.
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheRequest
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<TResponse>(request.CacheKey, out var cachedResponse))
        {
            _logger.LogDebug("Cache hit for {CacheKey}", request.CacheKey);
            return cachedResponse!;
        }

        _logger.LogDebug("Cache miss for {CacheKey}", request.CacheKey);
        var response = await next();

        var duration = request.CacheDuration ?? TimeSpan.FromMinutes(5);
        _cache.Set(request.CacheKey, response, duration);

        return response;
    }
}
```

## Event System

### Domain Events

Domain events capture important business occurrences:

```csharp
namespace BuildingBlocks.Core.Event;

/// <summary>
/// Represents a domain event that occurred within a bounded context.
/// Domain events are dispatched after successful aggregate persistence.
/// </summary>
public interface IDomainEvent : IEvent, INotification
{
    /// <summary>
    /// Unique identifier for event correlation.
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base implementation for domain events.
/// </summary>
public abstract record DomainEventBase : EventBase, IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

### Integration Events

Integration events enable communication between modules:

```csharp
namespace BuildingBlocks.Core.Event;

/// <summary>
/// Represents an event that crosses bounded context boundaries.
/// Used for inter-module communication and external system integration.
/// </summary>
public interface IIntegrationEvent : IEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base implementation for integration events.
/// </summary>
public abstract record IntegrationEventBase : EventBase, IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

### Event Dispatching

The EventDispatcher orchestrates event handling and transformation:

```csharp
namespace BuildingBlocks.Core.Event;

/// <summary>
/// Central event dispatcher for domain and integration events.
/// Handles event transformation, persistence, and distribution.
/// </summary>
public sealed class EventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEventMapper _eventMapper;
    private readonly ILogger<EventDispatcher> _logger;
    private readonly IPersistMessageProcessor _persistMessageProcessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task SendAsync<T>(
        IReadOnlyList<T> events, 
        Type? type = null,
        CancellationToken cancellationToken = default) where T : IEvent
    {
        if (events.Count == 0) return;

        // Determine event type for routing
        var eventType = type != null && type.IsAssignableTo(typeof(IInternalCommand))
            ? EventType.InternalCommand
            : EventType.DomainEvent;

        // Process domain events
        if (events is IReadOnlyList<IDomainEvent> domainEvents)
        {
            // Transform to integration events
            var integrationEvents = await MapDomainEventToIntegrationEventAsync(domainEvents);
            
            // Publish via outbox pattern for reliability
            foreach (var integrationEvent in integrationEvents)
            {
                await _persistMessageProcessor.PublishMessageAsync(
                    new MessageEnvelope(integrationEvent, SetHeaders()),
                    cancellationToken);
            }
            
            // Handle internal commands if needed
            if (eventType == EventType.InternalCommand)
            {
                var internalCommands = await MapDomainEventToInternalCommandAsync(domainEvents);
                foreach (var command in internalCommands)
                {
                    await _persistMessageProcessor.AddInternalMessageAsync(command, cancellationToken);
                }
            }
        }
    }

    private Dictionary<string, object?> SetHeaders()
    {
        var headers = new Dictionary<string, object?>();
        
        // Add correlation ID for distributed tracing
        var correlationId = _httpContextAccessor?.HttpContext?.GetCorrelationId();
        if (correlationId is not null)
        {
            headers.Add("CorrelationId", correlationId);
        }
        
        // Add user context for audit trail
        var userId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            headers.Add("UserId", userId);
        }

        return headers;
    }
}
```

### Message Processing

The outbox pattern ensures reliable message delivery:

```csharp
namespace BuildingBlocks.PersistMessageProcessor;

/// <summary>
/// Implements the outbox pattern for reliable message delivery.
/// Ensures at-least-once delivery semantics.
/// </summary>
public class PersistMessageProcessor : IPersistMessageProcessor
{
    private readonly IPersistMessageDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PersistMessageProcessor> _logger;

    public async Task PublishMessageAsync<T>(
        MessageEnvelope<T> messageEnvelope,
        CancellationToken cancellationToken = default) where T : class
    {
        // Store message in outbox
        var persistMessage = new PersistMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageEnvelope.Message.GetType().Name,
            Data = JsonSerializer.Serialize(messageEnvelope.Message),
            Headers = JsonSerializer.Serialize(messageEnvelope.Headers),
            MessageStatus = MessageStatus.InProgress,
            Created = DateTime.UtcNow
        };

        await _context.PersistMessages.AddAsync(persistMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // Attempt immediate delivery
            await _publishEndpoint.Publish(messageEnvelope.Message, cancellationToken);
            
            persistMessage.MessageStatus = MessageStatus.Processed;
            persistMessage.Processed = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId}", persistMessage.Id);
            persistMessage.MessageStatus = MessageStatus.Failed;
            persistMessage.Error = ex.Message;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

## Web Infrastructure

### API Configuration

The web infrastructure provides a consistent API surface:

```csharp
namespace BuildingBlocks.Web;

/// <summary>
/// Extension methods for minimal API configuration.
/// </summary>
public static class MinimalApiExtensions
{
    /// <summary>
    /// Maps all minimal API endpoints in the assembly.
    /// </summary>
    public static IEndpointRouteBuilder MapMinimalEndpoints(this IEndpointRouteBuilder builder)
    {
        var endpointTypes = Assembly.GetCallingAssembly()
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IMinimalEndpoint)) && !t.IsAbstract);

        foreach (var endpointType in endpointTypes)
        {
            var endpoint = Activator.CreateInstance(endpointType) as IMinimalEndpoint;
            endpoint?.MapEndpoint(builder);
        }

        return builder;
    }
}

/// <summary>
/// Interface for minimal API endpoints.
/// </summary>
public interface IMinimalEndpoint
{
    IEndpointRouteBuilder MapEndpoint(IEndpointRouteBuilder builder);
}
```

### Correlation Tracking

Request correlation for distributed tracing:

```csharp
namespace BuildingBlocks.Web;

/// <summary>
/// Extensions for correlation ID management.
/// </summary>
public static class CorrelationExtensions
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            return correlationId as string ?? Guid.NewGuid().ToString();
        }

        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue))
        {
            correlationId = headerValue.ToString();
            context.Items[CorrelationIdHeader] = correlationId;
            return correlationId;
        }

        correlationId = Guid.NewGuid().ToString();
        context.Items[CorrelationIdHeader] = correlationId;
        return correlationId;
    }
}
```

## Validation Framework

FluentValidation integration with custom rules:

```csharp
namespace BuildingBlocks.Validation;

/// <summary>
/// Common validation rules and extensions.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates email format.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email is too long");
    }

    /// <summary>
    /// Validates strong ID is not empty.
    /// </summary>
    public static IRuleBuilderOptions<T, TId?> MustBeValidId<T, TId>(
        this IRuleBuilder<T, TId?> ruleBuilder)
        where TId : struct
    {
        return ruleBuilder
            .NotNull().WithMessage("ID is required")
            .Must(id => !id.Equals(default(TId)))
            .WithMessage("ID cannot be empty");
    }

    /// <summary>
    /// Validates entity exists in database.
    /// </summary>
    public static IRuleBuilderOptions<T, TId> MustExist<T, TEntity, TId>(
        this IRuleBuilder<T, TId> ruleBuilder,
        IServiceProvider serviceProvider)
        where TEntity : class, IEntity<TId>
        where TId : struct
    {
        return ruleBuilder
            .MustAsync(async (id, cancellation) =>
            {
                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IReadRepository<TEntity, TId>>();
                return await repository.ExistsAsync(id, cancellation);
            })
            .WithMessage("Entity does not exist");
    }
}
```

## Exception Handling

Structured exception hierarchy for different error scenarios:

```csharp
namespace BuildingBlocks.Exception;

/// <summary>
/// Base application exception with error code support.
/// </summary>
public abstract class AppException : System.Exception
{
    public string Code { get; }
    
    protected AppException(string message, string code = "ApplicationError") 
        : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Domain rule violation exception.
/// </summary>
public class DomainException : AppException
{
    public DomainException(string message) 
        : base(message, "DomainError")
    {
    }
}

/// <summary>
/// Resource not found exception.
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string resourceName, object key) 
        : base($"{resourceName} with id '{key}' was not found", "NotFound")
    {
    }
}

/// <summary>
/// Validation failure exception.
/// </summary>
public class ValidationException : AppException
{
    public IReadOnlyList<ValidationError> Errors { get; }
    
    public ValidationException(IReadOnlyList<ValidationError> errors) 
        : base("One or more validation failures occurred", "ValidationError")
    {
        Errors = errors;
    }
}
```

## Observability

### OpenTelemetry Integration

Comprehensive observability with distributed tracing and metrics:

```csharp
namespace BuildingBlocks.OpenTelemetryCollector;

/// <summary>
/// OpenTelemetry configuration and setup.
/// </summary>
public static class Extensions
{
    public static IServiceCollection AddCustomOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetOptions<ObservabilityOptions>("Observability");
        
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(options.ServiceName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("environment", options.Environment),
                    new KeyValuePair<string, object>("version", options.Version)
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(opts =>
                    {
                        opts.SetDbStatementForText = true;
                        opts.SetDbStatementForStoredProcedure = true;
                    })
                    .AddSource(ObservabilityConstant.DefaultActivitySource.Name)
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(options.OtlpEndpoint);
                        opts.Protocol = OtlpExportProtocol.Grpc;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(ObservabilityConstant.DefaultMeter.Name)
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(options.OtlpEndpoint);
                        opts.Protocol = OtlpExportProtocol.Grpc;
                    });
            });

        return services;
    }
}
```

### Metrics and Tracing

Custom metrics for business operations:

```csharp
namespace BuildingBlocks.OpenTelemetryCollector.CoreDiagnostics.Commands;

/// <summary>
/// Command handler metrics collection.
/// </summary>
public static class CommandHandlerMetrics
{
    private static readonly Counter<long> CommandCounter = ObservabilityConstant.DefaultMeter
        .CreateCounter<long>("commands_total", "Total number of commands processed");
        
    private static readonly Histogram<double> CommandDuration = ObservabilityConstant.DefaultMeter
        .CreateHistogram<double>("command_duration_ms", "Command execution duration in milliseconds");
        
    public static void RecordCommandExecution(string commandName, double durationMs, bool success)
    {
        var tags = new TagList
        {
            { "command", commandName },
            { "success", success }
        };
        
        CommandCounter.Add(1, tags);
        CommandDuration.Record(durationMs, tags);
    }
}
```

## Security

### JWT Authentication

JWT token configuration and validation:

```csharp
namespace BuildingBlocks.Jwt;

/// <summary>
/// JWT authentication configuration.
/// </summary>
public static class JwtExtensions
{
    public static IServiceCollection AddCustomJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetOptions<JwtOptions>("Jwt");
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = jwtOptions.Authority;
                options.Audience = jwtOptions.Audience;
                options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
                
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogError(context.Exception, "Authentication failed");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogInformation("Token validated for user {UserId}", 
                            context.Principal?.Identity?.Name);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
```

## External Integrations

### MassTransit

Message bus configuration for RabbitMQ:

```csharp
namespace BuildingBlocks.MassTransit;

/// <summary>
/// MassTransit configuration for message bus.
/// </summary>
public static class Extensions
{
    public static IServiceCollection AddCustomMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? assembly = null)
    {
        var options = configuration.GetOptions<RabbitMqOptions>("RabbitMq");
        
        services.AddMassTransit(configurator =>
        {
            configurator.SetKebabCaseEndpointNameFormatter();
            
            if (assembly != null)
            {
                configurator.AddConsumers(assembly);
            }
            
            configurator.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });
                
                cfg.UseMessageRetry(r => r.Intervals(100, 200, 500, 800, 1000));
                cfg.UseInMemoryOutbox();
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

### PostgreSQL

PostgreSQL-specific configuration:

```csharp
namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL database configuration.
/// </summary>
public static class PostgresExtensions
{
    public static IServiceCollection AddCustomPostgres<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            });
            
            options.UseSnakeCaseNamingConvention();
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
            
            // Add interceptors
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventInterceptor>());
        });

        return services;
    }
}
```

## Testing Infrastructure

Base classes for testing:

```csharp
namespace BuildingBlocks.TestBase;

/// <summary>
/// Base class for integration tests with test containers.
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected IConfiguration Configuration { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        var builder = new ConfigurationBuilder();
        ConfigureConfiguration(builder);
        Configuration = builder.Build();
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        
        await InitializeTestAsync();
    }
    
    protected abstract void ConfigureConfiguration(IConfigurationBuilder builder);
    protected abstract void ConfigureServices(IServiceCollection services);
    protected virtual Task InitializeTestAsync() => Task.CompletedTask;
    
    public virtual Task DisposeAsync() => Task.CompletedTask;
}
```

## Configuration Management

Centralized configuration with strongly-typed options:

```csharp
namespace BuildingBlocks.Web;

/// <summary>
/// Configuration helper for strongly-typed options.
/// </summary>
public static class ConfigurationHelper
{
    public static TOptions GetOptions<TOptions>(
        this IConfiguration configuration, 
        string sectionName)
        where TOptions : new()
    {
        var options = new TOptions();
        configuration.GetSection(sectionName).Bind(options);
        return options;
    }
    
    public static IServiceCollection ConfigureOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services.Configure<TOptions>(configuration.GetSection(sectionName));
        services.AddSingleton(provider => 
            provider.GetRequiredService<IOptions<TOptions>>().Value);
        
        return services;
    }
}
```

## Migration Guide

When upgrading or migrating components:

1. **Entity Migration**
   - Update entities to inherit from `BaseEntity<T>` or `BaseAuditableEntity<T>`
   - Replace primitive IDs with strong type IDs
   - Add version property for optimistic concurrency

2. **CQRS Migration**
   - Convert services to command/query handlers
   - Implement `ICommand` and `IQuery` interfaces
   - Add validation using FluentValidation

3. **Repository Migration**
   - Split repositories into read and write
   - Implement Unit of Work pattern
   - Add caching for read operations

4. **Event Migration**
   - Convert domain logic to emit domain events
   - Implement event handlers for side effects
   - Use outbox pattern for integration events

## Best Practices

### Domain Modeling
- Use strong type IDs to prevent primitive obsession
- Implement aggregates as transaction boundaries
- Emit domain events for important state changes
- Use value objects for complex domain concepts

### CQRS Implementation
- Keep commands and queries separate
- Use different models for reads and writes
- Implement caching for query results
- Validate commands before processing

### Error Handling
- Use Result pattern instead of exceptions
- Provide meaningful error messages
- Log errors with correlation IDs
- Return appropriate HTTP status codes

### Performance
- Implement multi-level caching
- Use async/await throughout
- Optimize database queries with indexes
- Monitor with OpenTelemetry metrics

### Security
- Validate all inputs
- Use JWT for authentication
- Implement authorization policies
- Audit sensitive operations

### Testing
- Write unit tests for handlers
- Use test containers for integration tests
- Mock external dependencies
- Test error scenarios

---

*Last Updated: January 2025*
*Version: 2.0.0*
*Architecture: Clean Architecture + DDD + CQRS*
*Technology Stack: .NET 8, EF Core, PostgreSQL, RabbitMQ, OpenTelemetry*