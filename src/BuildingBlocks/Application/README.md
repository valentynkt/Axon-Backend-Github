# BuildingBlocks Application Layer Documentation

## Overview

The **BuildingBlocks Application Layer** is a comprehensive set of cross-cutting concerns and infrastructure components that implement advanced CQRS patterns, resilience, observability, and reliability features for the Axon Backend system. This layer provides the foundational behaviors and services that ensure consistent, reliable, and observable application operations across all modules.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Core Components](#core-components)
  - [Pipeline Behaviors](#pipeline-behaviors)
  - [Caching System](#caching-system)
  - [Event Handling](#event-handling)
  - [Outbox Pattern](#outbox-pattern)
  - [Inbox Pattern](#inbox-pattern)
  - [Validation Framework](#validation-framework)
- [Component Details](#component-details)
- [Configuration](#configuration)
- [Usage Guide](#usage-guide)

## Architecture Overview

The Application Layer implements the **Mediator Pattern** using MediatR with a sophisticated pipeline of behaviors that provide cross-cutting concerns. The layer follows these architectural principles:

- **Pipeline Architecture**: Sequential processing through configurable behaviors
- **Result Pattern**: Functional error handling without exceptions
- **Declarative Configuration**: Attribute and interface-based feature activation
- **Observability First**: Built-in tracing, metrics, and structured logging
- **Reliability Patterns**: Outbox pattern, retries, and circuit breaking
- **Performance Optimization**: Multi-level caching and intelligent key generation

### Pipeline Execution Order

The pipeline behaviors execute in a specific order (outermost to innermost):

1. **ObservabilityBehavior** - W3C tracing, metrics collection, telemetry
2. **LoggingBehavior** - Structured logging with correlation
3. **QueryRetryBehavior** - Transient failure recovery with exponential backoff
4. **ResultValidationBehavior** - FluentValidation integration with Result pattern
5. **DomainValidationBehavior** - Domain rule validation
6. **QueryCachingBehavior** - L1/L2 caching for queries
7. **CommandCacheInvalidationBehavior** - Cache invalidation on commands
8. **TransactionBehavior** - Database transaction management with outbox
9. **ExceptionToResultBehavior** - Exception conversion to Result pattern

## Core Components

### Pipeline Behaviors

#### ObservabilityPipelineBehavior

**Purpose**: Provides comprehensive observability through W3C TraceContext, OpenTelemetry metrics, and structured logging.

**Key Features**:
- W3C TraceContext propagation (TraceId = CorrelationId)
- OpenTelemetry activity spans with semantic tags
- Low-cardinality metrics (requests, duration, errors, cancellations)
- Automatic error status propagation
- Request metadata enrichment

**Metrics Emitted**:
- `axon.observability.requests.total` - Total request count
- `axon.observability.request.duration` - Request duration histogram
- `axon.observability.requests.errors.total` - Failed request count
- `axon.observability.requests.cancelled.total` - Cancelled request count

#### LoggingBehavior

**Purpose**: Lightweight structured logging that activates only when ObservabilityBehavior is not present (avoiding duplicate logs).

**Key Features**:
- Automatic detection of existing Activity to prevent duplication
- Performance warnings for slow operations (>1s)
- Structured log context with request type and timing

#### QueryRetryBehavior

**Purpose**: Provides resilient query execution with intelligent retry logic for transient failures.

**Key Features**:
- Opt-in via `IRetryableQuery` interface or `[Retryable]` attribute
- Exponential backoff with jitter (Polly v8)
- Transient fault detection via `ITransientFaultDetector`
- Activity events for retry tracking
- Never retries non-idempotent operations (queries only)

**Configuration Options**:
```csharp
[Retryable(MaxAttempts = 3, InitialDelayMs = 100, MaxDelayMs = 30000)]
public class GetUserQuery : IQuery<Result<UserDto>> { }
```

#### ResultValidationBehavior

**Purpose**: Integrates FluentValidation with the Result pattern for request validation.

**Key Features**:
- Automatic validator discovery via DI
- Error grouping by property for cleaner payloads
- Compiled delegates for Result creation (no reflection on hot path)
- Metadata-aware validation context support
- Sample value capture for diagnostics

#### DomainValidationBehavior

**Purpose**: Executes domain-level validation rules before handler execution.

**Key Features**:
- Validates commands implementing `DomainCommandBase`
- Pure validation (no I/O or side effects)
- Converts `Validation<T>` failures to Result pattern
- Aggregate type awareness for logging

#### QueryCachingBehavior

**Purpose**: Implements declarative two-level caching (L1 Memory + L2 Distributed) for queries.

**Key Features**:
- Declarative caching via `IQuery` properties
- L1 (IMemoryCache) and L2 (IDistributedCache/Redis) caching
- Intelligent cache key generation with content hashing
- Tag-based cache invalidation support
- Automatic L2 to L1 promotion on hits
- JSON serialization for distributed cache

**Cache Key Format**: `axon:query:{prefix}:{contentHash}:{contextPart}`

#### CommandCacheInvalidationBehavior

**Purpose**: Automatically invalidates related cache entries after successful commands.

**Key Features**:
- Tag-based invalidation across distributed nodes
- Declarative via `ICacheInvalidatable` or `[InvalidatesCache]`
- Convention-based fallback (Create/Update/Delete patterns)
- Best-effort invalidation (doesn't fail commands)

#### TransactionBehavior

**Purpose**: Manages database transactions with domain event integration and outbox pattern support.

**Key Features**:
- Automatic transaction wrapping for commands
- Domain event collection and persistence
- Outbox pattern integration for reliable event publishing
- Metadata-aware isolation level selection
- Post-commit event processing
- Comprehensive error handling and rollback

**Isolation Level Selection**:
- Checks metadata for explicit `IsolationLevel`
- Upgrades to `Serializable` for high-consistency operations
- Upgrades to `RepeatableRead` for financial operations
- Defaults to `ReadCommitted`

#### ExceptionToResultBehavior

**Purpose**: Converts unhandled exceptions to Result pattern failures.

**Key Features**:
- Never converts `OperationCanceledException` when cancellation requested
- Compiled factories for Result creation
- Preserves exception details in Error metadata
- Type-safe for both `Result` and `Result<T>`

### Caching System

#### ICacheKeyGenerator & DefaultCacheKeyGenerator

**Purpose**: Generates deterministic, collision-resistant cache keys.

**Key Features**:
- SHA256 content hashing for uniqueness
- Query property serialization with camelCase JSON
- Optional W3C TraceContext isolation
- Fallback for non-IQuery types

#### CacheOptions

**Configuration**: Controls cache behavior, durations, and provider settings.

**Key Settings**:
- `DefaultDuration`: Default TTL when not specified (5 minutes)
- `IncludeTraceInKey`: W3C context isolation (default: false)
- `CompressionThreshold`: Compress entries larger than threshold
- `MemoryCacheSizeLimitMB`: L1 cache memory limit
- `RedisConnectionString`: L2 distributed cache connection

#### ICacheTagIndex & DistributedCacheTagIndex

**Purpose**: Maps cache tags to keys for reliable cross-node invalidation.

**Implementation**:
- JSON-based tag index in distributed cache
- Set operations for tag-to-key mapping
- Automatic cleanup on invalidation

### Event Handling

#### IEventDispatcher & EventDispatcher

**Purpose**: Manages domain event dispatching with integration event mapping.

**Key Features**:
- Domain to integration event mapping
- Internal command generation from domain events
- Header enrichment (CorrelationId, UserId, UserName)
- Support for `IHaveIntegrationEvent` wrapper pattern

#### IEventMapper & CompositeEventMapper

**Purpose**: Maps domain events to integration events and internal commands.

**Features**:
- Composite pattern for multiple mappers
- Null-safe mapping with fallback support
- Module-specific event transformation

### Outbox Pattern

#### IOutboxService & OutboxService

**Purpose**: Provides reliable event storage and processing within database transactions.

**Key Features**:
- Atomic event persistence with business data
- Transaction-scoped event storage
- Batch processing with controlled concurrency
- Failed event retry with exponential backoff
- Dead letter queue for permanent failures
- Comprehensive statistics and monitoring

**Core Operations**:
- `StoreEventsAsync`: Persist events within transaction
- `ProcessPendingEventsAsync`: Process events for specific transaction
- `ProcessAllPendingEventsAsync`: Batch process all pending events
- `RetryFailedEventsAsync`: Retry eligible failed events
- `ReprocessDeadLetterEventsAsync`: Manual dead letter reprocessing

#### IOutboxProcessor & OutboxProcessor

**Purpose**: Background service for continuous outbox event processing.

**Features**:
- Hosted service integration
- Configurable processing intervals
- Health monitoring with recent error tracking
- Automatic cleanup of completed entries
- Graceful shutdown support

**Health Metrics**:
- Processing status and last run time
- Pending and dead letter counts
- Recent error history

#### OutboxOptions

**Configuration**: Controls outbox processing behavior.

**Key Settings**:
- `BatchSize`: Events per processing batch (default: 100)
- `MaxRetries`: Attempts before dead letter (default: 3)
- `MaxConcurrency`: Parallel processing tasks
- `ProcessingInterval`: Background check interval
- `CompletedRetentionPeriod`: Cleanup after period

### Inbox Pattern

The Inbox pattern provides reliable inbound integration event processing with policy-driven retry and dead-letter handling. It ensures idempotent processing of external events while providing sophisticated error handling and observability.

#### IInboundIntegrationEventDispatcher & InboundIntegrationEventDispatcher

**Purpose**: Transport-neutral dispatcher for processing inbound integration events with retry and dead-letter capabilities.

**Key Features**:
- Type resolution and deserialization of integration events
- Idempotency checking via `IInboxStore`
- Policy-driven error classification and retry logic
- Dead-letter handling for poison messages
- Comprehensive metrics and observability
- Transport-neutral design (no Kafka/MassTransit dependencies)

**Processing Flow**:
1. Extract idempotency key and check for duplicates
2. Resolve event type and deserialize payload
3. Execute registered handlers sequentially
4. On failure: classify error → apply retry policy → dead-letter if needed

#### IInboundErrorClassifier & DefaultInboundErrorClassifier

**Purpose**: Classifies handler failures to determine appropriate error handling strategy.

**Error Classifications**:
- **Transient**: Network timeouts, database connection issues, HTTP 5xx errors
- **Permanent**: Validation errors, argument exceptions, business rule violations
- **Unclassified**: Unknown errors (configurable as permanent or transient)

**Classification Examples**:
```csharp
// Permanent errors (immediate dead-letter)
ArgumentException, InvalidOperationException, ValidationException

// Transient errors (retry with backoff)
TimeoutException, HttpRequestException, TaskCanceledException, DbException
```

#### IInboxRetryPolicy & DefaultInboxRetryPolicy

**Purpose**: Computes retry delays and determines when to move messages to dead-letter.

**Key Features**:
- Exponential backoff with jitter (mirrors outbox semantics)
- Configurable maximum attempts and delay caps
- Message age validation (prevent processing stale messages)
- Deterministic delay computation for consistent behavior

**Retry Formula**:
```
delay = min(baseDelay * 2^attempt, maxDelay) ± jitter
```

#### IInboxDeadLetterStore & InboxDeadLetterEntry

**Purpose**: Persistent storage for permanently failed messages with comprehensive forensic data.

**Dead Letter Entry Contents**:
- Original integration event envelope
- Error message and stack trace
- Processing attempt count and timing
- Idempotency key for correlation

**Storage Implementations**:
- `NoOpInboxDeadLetterStore`: Logs entries but doesn't persist (default)
- Infrastructure layer provides persistent implementations

#### InboxOptions

**Configuration**: Controls retry behavior, delays, and dead-letter handling.

**Key Settings**:
- `MaxAttempts`: Retry limit before dead-letter (default: 5)
- `BaseDelay`: Initial retry delay (default: 5 seconds)
- `MaxRetryDelay`: Maximum delay cap (default: 30 minutes)
- `UseJitter`: Enable jitter to prevent thundering herd (default: true)
- `JitterRatio`: Jitter percentage ±20% (default: 0.2)
- `UnclassifiedIsPermanent`: Treat unknown errors as permanent (default: false)
- `MaxMessageAge`: Stale message threshold (default: 7 days)

**Configuration Example**:
```csharp
services.AddInboundIntegrationEventPipeline(options =>
{
    options.MaxAttempts = 3;
    options.BaseDelay = TimeSpan.FromSeconds(10);
    options.MaxRetryDelay = TimeSpan.FromMinutes(15);
    options.UnclassifiedIsPermanent = true;
});
```

#### Metrics Integration

**Inbound Metrics** (extends `IOutboxMetrics`):
- `RecordInboundRetryScheduled(TimeSpan delay)`: Retry scheduled with delay
- `RecordInboundMovedToDeadLetter()`: Message moved to dead-letter
- `RecordInboundPermanentFailure()`: Permanent failure occurred

**Transport Integration**:
- Uses `X-Delivery-Attempt` header for attempt tracking
- Returns `HandlerFailed` result for retry/dead-letter decisions
- Infrastructure layer maps results to transport-specific actions (NACK/ACK)

### Validation Framework

#### ValidatorBase

**Purpose**: Enhanced base validator with metadata-aware validation capabilities.

**Features**:
- Context-aware validation rules
- Feature flag conditional validation
- Tenant-specific validation
- Metadata-driven rule application
- User-specific validation

**Helper Methods**:
- `WhenFeatureEnabled`: Apply rule when feature flag active
- `WhenTenant`: Apply rule for specific tenant
- `WhenMetadata`: Apply rule based on metadata values
- `WhenUser`: Apply rule for specific user
- `WhenContext`: Custom context predicate

#### IValidationContext & ValidationContext

**Purpose**: Provides metadata access during validation.

**Context Data**:
- Request ID and timestamp
- W3C trace context (TraceId, SpanId)
- Metadata dictionary
- Feature flags
- Tenant and user information

#### IMetadataValidator

**Purpose**: Marker interface for validators requiring context.

**Usage**: Validators implementing this interface receive `IValidationContext` before validation begins.

## Configuration

### Extension Methods

#### OutboxServiceExtensions

**Methods**:
- `AddOutboxPattern`: Complete outbox implementation
- `AddOutboxServices`: Outbox without background processor
- `AddOutboxProcessor`: Background processor only
- `ConfigureTransactionBehavior`: Transaction options
- `ConfigureOutboxProcessing`: Outbox options

**Presets**:
- `OutboxPresets.HighThroughput`: Many events, high concurrency
- `OutboxPresets.LowLatency`: Fast processing, small batches
- `OutboxPresets.Conservative`: Limited resources
- `OutboxPresets.Development`: Extensive logging, fast cleanup

#### CachingConfiguration

**Methods**:
- `AddDeclarativeQueryCaching`: Configure caching providers
- `AddCachingPipelineBehavior`: Register caching behavior
- `AddDevelopmentCaching`: Development settings
- `AddProductionCaching`: Production settings

#### PipelineBehaviorExtensions

**Method**: `AddPipelineBehaviors` - Registers all behaviors in correct order

## Usage Guide

### Basic Setup

```csharp
// In Program.cs or Startup.cs
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add all pipeline behaviors in correct order
services.AddPipelineBehaviors();

// Configure caching
services.AddDeclarativeQueryCaching(options =>
{
    options.DefaultDuration = TimeSpan.FromMinutes(15);
    options.RedisConnectionString = "localhost:6379";
});

// Configure outbox pattern
services.AddOutboxPattern(
    configureTransaction: options =>
    {
        options.EnableOutboxProcessing = true;
        options.DefaultIsolationLevel = IsolationLevel.ReadCommitted;
    },
    configureOutbox: OutboxPresets.HighThroughput
);

// Add validators
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
```

### Creating Cached Queries

```csharp
public record GetProductsQuery : IQuery<Result<PagedResult<ProductDto>>>
{
    public bool UseCache => true;
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
    public string? CacheKeyPrefix => "products";
    
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

// Or with tags for invalidation
public class GetProductQuery : IQuery<Result<ProductDto>>, ICacheTaggable
{
    public Guid ProductId { get; init; }
    public bool UseCache => true;
    public string[] CacheTags => ["product", $"product:{ProductId}"];
}
```

### Creating Commands with Cache Invalidation

```csharp
[InvalidatesCache("products", "product-list")]
public record UpdateProductCommand : ICommand<Result>
{
    public Guid ProductId { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
}

// Or programmatically
public class DeleteProductCommand : ICommand<Result>, ICacheInvalidatable
{
    public Guid ProductId { get; init; }
    
    public string[] GetInvalidationTags() => 
        ["products", $"product:{ProductId}", "product-list"];
}
```

### Creating Retryable Queries

```csharp
[Retryable(MaxAttempts = 3, InitialDelayMs = 100)]
public record GetExternalDataQuery : IQuery<Result<ExternalData>>
{
    public string ApiEndpoint { get; init; }
}

// Or programmatically
public class GetUserDataQuery : IQuery<Result<UserData>>, IRetryableQuery
{
    public RetryPolicy GetRetryPolicy() => new()
    {
        MaxAttempts = 5,
        InitialDelay = TimeSpan.FromMilliseconds(200),
        MaxDelay = TimeSpan.FromSeconds(30)
    };
}
```

### Creating Metadata-Aware Validators

```csharp
public class CreateOrderValidator : ValidatorBase<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        // Standard validation
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        
        // Feature flag conditional
        RuleFor(x => x.DeliveryInstructions)
            .NotEmpty()
            .WhenFeatureEnabled("RequireDeliveryInstructions");
        
        // Tenant-specific
        RuleFor(x => x.PurchaseOrderNumber)
            .NotEmpty()
            .WhenTenant("enterprise-tenant");
        
        // Metadata-driven
        RuleFor(x => x.TaxId)
            .NotEmpty()
            .WhenMetadata("CustomerType", "Business");
        
        // Complex context validation
        RuleFor(x => x.ExpressShipping)
            .Must(BeAvailableForUser)
            .WhenContext(ctx => 
                ctx.IsFeatureEnabled("ExpressShipping") && 
                ctx.GetMetadata<string>("Region") == "US");
    }
    
    private bool BeAvailableForUser(bool expressShipping)
    {
        if (!expressShipping) return true;
        
        var userTier = GetMetadataOrDefault<string>("UserTier", "Standard");
        return userTier is "Premium" or "Enterprise";
    }
}
```

### Implementing Transient Fault Detection

```csharp
public class HttpTransientFaultDetector : ITransientFaultDetector
{
    public bool IsTransient(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => IsTransientHttpError(httpEx),
            TaskCanceledException => false,
            TimeoutException => true,
            _ => false
        };
    }
    
    private bool IsTransientHttpError(HttpRequestException ex)
    {
        // Check for 5xx errors, 429 (too many requests), etc.
        return ex.StatusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.TooManyRequests;
    }
}
```

### Domain Command with Validation

```csharp
public abstract class DomainCommandBase
{
    public abstract Type GetAggregateType();
    public abstract Validation<Unit> ValidateDomainRules();
}

public class CreateUserCommand : DomainCommandBase, ICommand<Result<Guid>>
{
    public string Email { get; init; }
    public string Name { get; init; }
    
    public override Type GetAggregateType() => typeof(User);
    
    public override Validation<Unit> ValidateDomainRules()
    {
        var errors = new List<Error>();
        
        if (!Email.Contains('@'))
            errors.Add(Error.Validation("Invalid email format", "USER_EMAIL_INVALID"));
            
        if (Name.Length < 2)
            errors.Add(Error.Validation("Name too short", "USER_NAME_TOO_SHORT"));
            
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors) 
            : Validation<Unit>.Valid(Unit.Value);
    }
}
```

## Best Practices

### Performance Optimization

1. **Cache Key Design**: Use meaningful prefixes and include only necessary data in cache keys
2. **Cache Duration**: Balance between data freshness and performance
3. **Batch Processing**: Configure outbox batch sizes based on load patterns
4. **Concurrency**: Set MaxConcurrency based on available resources

### Reliability

1. **Retry Strategy**: Use exponential backoff with jitter for transient failures
2. **Dead Letter Queue**: Monitor and process dead letter entries regularly
3. **Transaction Isolation**: Choose appropriate isolation levels for operations
4. **Outbox Cleanup**: Configure retention periods to manage storage

### Observability

1. **Correlation**: Use W3C TraceContext for distributed tracing
2. **Metrics**: Monitor key metrics (request rate, duration, error rate)
3. **Logging**: Use structured logging with appropriate log levels
4. **Health Checks**: Monitor outbox processor health

### Security

1. **Validation**: Always validate at boundaries (API and domain)
2. **Metadata**: Sanitize metadata before validation
3. **Cache Keys**: Avoid sensitive data in cache keys
4. **Error Messages**: Don't expose internal details in validation errors

## Troubleshooting

### Common Issues

1. **Cache Misses**: Check key generation and TTL settings
2. **Slow Queries**: Enable retry behavior for external dependencies
3. **Transaction Deadlocks**: Review isolation levels and query patterns
4. **Outbox Backlog**: Increase batch size or processing frequency
5. **Memory Issues**: Configure L1 cache size limits

### Monitoring Checklist

- [ ] Outbox pending count trending
- [ ] Cache hit/miss ratios
- [ ] Average request duration
- [ ] Retry attempt frequency
- [ ] Dead letter queue size
- [ ] Transaction rollback rate

## Summary

The BuildingBlocks Application Layer provides a robust, production-ready foundation for building reliable, observable, and performant applications. By leveraging the pipeline pattern with carefully ordered behaviors, the system achieves:

- **Reliability** through retry policies and the outbox pattern
- **Performance** through multi-level caching and optimized serialization
- **Observability** through comprehensive tracing and metrics
- **Consistency** through transaction management and validation
- **Maintainability** through declarative configuration and clean separation of concerns

The layer's modular design allows teams to adopt features incrementally while maintaining consistency across the entire application.