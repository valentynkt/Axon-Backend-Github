# BuildingBlocks Application Layer - Usage Guide

## Table of Contents

1. [Quick Start](#quick-start)
2. [Pipeline Behaviors Usage](#pipeline-behaviors-usage)
3. [Caching Implementation](#caching-implementation)
4. [Event Handling](#event-handling)
5. [Outbox Pattern Implementation](#outbox-pattern-implementation)
6. [Validation Scenarios](#validation-scenarios)
7. [Advanced Scenarios](#advanced-scenarios)
8. [Testing Strategies](#testing-strategies)
9. [Performance Tuning](#performance-tuning)
10. [Migration Guide](#migration-guide)

## Quick Start

### Initial Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Add MediatR
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

// 2. Add all pipeline behaviors (order matters!)
builder.Services.AddPipelineBehaviors();

// 3. Add FluentValidation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// 4. Configure caching
builder.Services.AddDeclarativeQueryCaching(options =>
{
    options.DefaultDuration = TimeSpan.FromMinutes(5);
    options.RedisConnectionString = builder.Configuration.GetConnectionString("Redis");
    options.MemoryCacheSizeLimitMB = 100;
});

// 5. Configure outbox pattern
builder.Services.AddOutboxPattern(
    configureTransaction: options =>
    {
        options.EnableOutboxProcessing = true;
        options.DefaultIsolationLevel = IsolationLevel.ReadCommitted;
        options.OutboxProcessingDelay = TimeSpan.FromMilliseconds(100);
    },
    configureOutbox: options =>
    {
        options.Enabled = true;
        options.BatchSize = 100;
        options.MaxRetries = 3;
        options.ProcessingInterval = TimeSpan.FromSeconds(30);
    }
);

// 6. Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"));
});

// 7. Add domain event dispatcher
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IEventMapper, CompositeEventMapper>();

var app = builder.Build();
```

## Pipeline Behaviors Usage

### Creating a Basic Query

```csharp
// Query definition
public record GetUserByIdQuery(Guid UserId) : IQuery<Result<UserDto>>
{
    // No caching by default
    public bool UseCache => false;
    public TimeSpan? CacheDuration => null;
    public string? CacheKeyPrefix => null;
}

// Query handler
public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUserRepository _repository;
    
    public GetUserByIdQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Result<UserDto>> Handle(
        GetUserByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
            return Result<UserDto>.Failure(Error.NotFound(
                $"User with ID {request.UserId} not found",
                "USER_NOT_FOUND"));
        
        var dto = new UserDto(user.Id, user.Name, user.Email);
        return Result<UserDto>.Success(dto);
    }
}
```

### Creating a Command with Transaction

```csharp
// Command definition
public record CreateOrderCommand : ICommand<Result<Guid>>
{
    public Guid CustomerId { get; init; }
    public List<OrderItemDto> Items { get; init; }
    public ShippingAddress ShippingAddress { get; init; }
    
    // Optional: Request metadata for tracing
    public Guid RequestId { get; } = Guid.NewGuid();
    public DateTime RequestedAt { get; } = DateTime.UtcNow;
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}

// Command handler (automatically wrapped in transaction)
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    
    public async Task<Result<Guid>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        // Validate inventory
        foreach (var item in request.Items)
        {
            var available = await _inventoryService.CheckAvailabilityAsync(
                item.ProductId, item.Quantity, cancellationToken);
                
            if (!available)
                return Result<Guid>.Failure(Error.Conflict(
                    $"Product {item.ProductId} not available",
                    "PRODUCT_UNAVAILABLE"));
        }
        
        // Create order aggregate
        var order = Order.Create(
            request.CustomerId,
            request.Items.Select(i => new OrderItem(i.ProductId, i.Quantity, i.Price)),
            request.ShippingAddress);
        
        // Add domain event (will be stored in outbox)
        order.AddDomainEvent(new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            DateTime.UtcNow));
        
        // Save (within transaction)
        await _orderRepository.AddAsync(order, cancellationToken);
        
        return Result<Guid>.Success(order.Id.Value);
    }
}
```

## Caching Implementation

### Declarative Query Caching

```csharp
// Cached query with tags
public record GetProductsByCategoryQuery : IQuery<Result<List<ProductDto>>>, ICacheTaggable
{
    public string Category { get; init; }
    public bool IncludeOutOfStock { get; init; }
    
    // Enable caching
    public bool UseCache => true;
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(15);
    public string? CacheKeyPrefix => $"products:category:{Category}";
    
    // Tags for invalidation
    public string[] CacheTags => ["products", $"category:{Category}"];
}

// Command that invalidates cache
[InvalidatesCache("products")]
public record UpdateProductCommand : ICommand<Result>
{
    public Guid ProductId { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
    public string Category { get; init; }
}

// Or programmatic invalidation
public class DeleteProductCommand : ICommand<Result>, ICacheInvalidatable
{
    public Guid ProductId { get; init; }
    public string Category { get; init; }
    
    public string[] GetInvalidationTags() => 
    [
        "products",
        $"product:{ProductId}",
        $"category:{Category}"
    ];
}
```

### Advanced Caching Scenarios

```csharp
// Query with conditional caching based on parameters
public record SearchProductsQuery : IQuery<Result<PagedResult<ProductDto>>>
{
    public string? SearchTerm { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    
    // Cache only when no search term (browsing scenarios)
    public bool UseCache => string.IsNullOrEmpty(SearchTerm);
    
    // Shorter cache for filtered results
    public TimeSpan? CacheDuration => 
        MinPrice.HasValue || MaxPrice.HasValue 
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromMinutes(30);
    
    public string? CacheKeyPrefix => 
        $"products:search:{PageNumber}:{PageSize}";
}

// Multi-tenant cached query
public record GetTenantSettingsQuery : IQuery<Result<TenantSettings>>
{
    public string TenantId { get; init; }
    
    public bool UseCache => true;
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
    
    // Include tenant in cache key for isolation
    public string? CacheKeyPrefix => $"tenant:{TenantId}:settings";
    
    // Metadata for validation context
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>
    {
        ["TenantId"] = TenantId
    };
}
```

## Event Handling

### Domain Event with Integration Event

```csharp
// Domain event
public record OrderShippedEvent : IDomainEvent, IHaveIntegrationEvent
{
    public Guid OrderId { get; init; }
    public DateTime ShippedAt { get; init; }
    public string TrackingNumber { get; init; }
    
    // Map to integration event
    public IEnumerable<IIntegrationEvent> GetIntegrationEvents()
    {
        yield return new OrderShippedIntegrationEvent
        {
            OrderId = OrderId,
            ShippedAt = ShippedAt,
            TrackingNumber = TrackingNumber,
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        };
    }
}

// Event mapper for complex scenarios
public class OrderEventMapper : IEventMapper
{
    public IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event)
    {
        return @event switch
        {
            OrderCreatedEvent e => new OrderCreatedIntegrationEvent
            {
                OrderId = e.OrderId,
                CustomerId = e.CustomerId,
                TotalAmount = e.TotalAmount
            },
            OrderCancelledEvent e => new OrderCancelledIntegrationEvent
            {
                OrderId = e.OrderId,
                Reason = e.Reason,
                RefundAmount = e.RefundAmount
            },
            _ => null
        };
    }
    
    public IInternalCommand? MapToInternalCommand(IDomainEvent @event)
    {
        return @event switch
        {
            OrderShippedEvent e => new SendShipmentNotificationCommand
            {
                OrderId = e.OrderId,
                TrackingNumber = e.TrackingNumber
            },
            PaymentProcessedEvent e => new UpdateInventoryCommand
            {
                OrderId = e.OrderId
            },
            _ => null
        };
    }
}
```

### Aggregate with Domain Events

```csharp
public class Order : AggregateRoot<OrderId>, IAggregateRootWithEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void ClearDomainEvents() => _domainEvents.Clear();
    
    protected void AddDomainEvent(IDomainEvent @event)
    {
        _domainEvents.Add(@event);
    }
    
    public static Order Create(
        CustomerId customerId,
        IEnumerable<OrderItem> items,
        ShippingAddress shippingAddress)
    {
        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Items = items.ToList(),
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        
        // Raise domain event
        order.AddDomainEvent(new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.CalculateTotal(),
            order.CreatedAt));
        
        return order;
    }
    
    public Result Ship(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            return Result.Failure(Error.Invalid(
                "Order must be confirmed before shipping",
                "ORDER_NOT_CONFIRMED"));
        
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        TrackingNumber = trackingNumber;
        
        AddDomainEvent(new OrderShippedEvent
        {
            OrderId = Id,
            ShippedAt = ShippedAt.Value,
            TrackingNumber = trackingNumber
        });
        
        return Result.Success();
    }
}
```

## Outbox Pattern Implementation

### Manual Outbox Processing

```csharp
public class OrderService
{
    private readonly IOutboxService _outboxService;
    private readonly IOrderRepository _orderRepository;
    
    public async Task<Result> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        // Start transaction
        await using var transaction = await _orderRepository.BeginTransactionAsync(ct);
        
        try
        {
            // Business logic
            order.Confirm();
            await _orderRepository.UpdateAsync(order, ct);
            
            // Store events in outbox
            var storeResult = await _outboxService.StoreEventsAsync(
                order.DomainEvents.ToList(),
                transaction.TransactionId,
                Activity.Current?.TraceId.ToString(),
                Guid.NewGuid(),
                new Dictionary<string, object> { ["OrderId"] = order.Id },
                ct);
            
            if (storeResult.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return storeResult;
            }
            
            // Commit transaction
            await transaction.CommitAsync(ct);
            
            // Trigger processing (fire-and-forget)
            _ = _outboxService.ProcessPendingEventsAsync(transaction.TransactionId, ct);
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure(Error.Internal(ex.Message, "ORDER_PROCESSING_FAILED"));
        }
    }
}
```

### Monitoring Outbox Health

```csharp
public class OutboxHealthCheck : IHealthCheck
{
    private readonly IOutboxProcessor _processor;
    private readonly IOutboxService _service;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var healthResult = await _processor.GetHealthAsync(cancellationToken);
        
        if (healthResult.IsFailure)
            return HealthCheckResult.Unhealthy(
                "Outbox processor health check failed",
                data: new Dictionary<string, object>
                {
                    ["Error"] = healthResult.Error.Message
                });
        
        var health = healthResult.Value;
        var data = new Dictionary<string, object>
        {
            ["IsRunning"] = health.IsRunning,
            ["PendingEvents"] = health.PendingEventCount,
            ["DeadLetterEvents"] = health.DeadLetterEventCount,
            ["LastProcessingRun"] = health.LastProcessingRun?.ToString("O") ?? "Never",
            ["TimeSinceLastRun"] = health.TimeSinceLastRun?.ToString() ?? "N/A"
        };
        
        if (health.RecentErrors.Any())
            data["RecentErrors"] = string.Join("; ", health.RecentErrors.Take(3));
        
        // Determine health status
        if (!health.IsRunning)
            return HealthCheckResult.Unhealthy("Outbox processor is not running", data: data);
        
        if (health.DeadLetterEventCount > 100)
            return HealthCheckResult.Degraded(
                $"High dead letter count: {health.DeadLetterEventCount}", data: data);
        
        if (health.PendingEventCount > 1000)
            return HealthCheckResult.Degraded(
                $"High pending event count: {health.PendingEventCount}", data: data);
        
        if (health.TimeSinceLastRun > TimeSpan.FromMinutes(5))
            return HealthCheckResult.Degraded(
                "Outbox processor hasn't run recently", data: data);
        
        return HealthCheckResult.Healthy("Outbox processor is healthy", data);
    }
}
```

## Validation Scenarios

### Complex Validation with Context

```csharp
public class CreateAccountValidator : ValidatorBase<CreateAccountCommand>
{
    private readonly IAccountRepository _accountRepository;
    
    public CreateAccountValidator(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
        
        // Basic validation
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain digit")
            .WhenFeatureEnabled("StrongPasswordPolicy");
        
        // Business validation
        RuleFor(x => x.Email)
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email already exists");
        
        // Tenant-specific validation
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .WhenTenant("business");
        
        RuleFor(x => x.TaxId)
            .NotEmpty()
            .Matches(@"^\d{9}$")
            .WhenMetadata("AccountType", "Business");
        
        // Complex conditional validation
        RuleFor(x => x.ReferralCode)
            .MustAsync(BeValidReferralCode)
            .WhenContext(ctx => 
                ctx.IsFeatureEnabled("ReferralProgram") &&
                ctx.GetMetadata<bool>("RequiresReferral"));
    }
    
    private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
    {
        return !await _accountRepository.ExistsAsync(email, ct);
    }
    
    private async Task<bool> BeValidReferralCode(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code)) return false;
        
        // Check if referral code exists and is active
        return await _accountRepository.IsValidReferralCodeAsync(code, ct);
    }
}
```

### Domain Validation

```csharp
public class TransferMoneyCommand : DomainCommandBase, ICommand<Result>
{
    public AccountId FromAccountId { get; init; }
    public AccountId ToAccountId { get; init; }
    public Money Amount { get; init; }
    public string? Reference { get; init; }
    
    public override Type GetAggregateType() => typeof(Account);
    
    public override Validation<Unit> ValidateDomainRules()
    {
        var errors = new List<Error>();
        
        // Domain invariants
        if (FromAccountId == ToAccountId)
            errors.Add(Error.Validation(
                "Cannot transfer to same account",
                "TRANSFER_SAME_ACCOUNT"));
        
        if (Amount.Value <= 0)
            errors.Add(Error.Validation(
                "Transfer amount must be positive",
                "TRANSFER_AMOUNT_INVALID"));
        
        if (Amount.Value > 1_000_000)
            errors.Add(Error.Validation(
                "Transfer amount exceeds maximum limit",
                "TRANSFER_AMOUNT_EXCEEDS_LIMIT"));
        
        if (!string.IsNullOrEmpty(Reference) && Reference.Length > 100)
            errors.Add(Error.Validation(
                "Reference too long",
                "TRANSFER_REFERENCE_TOO_LONG"));
        
        return errors.Any()
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
}
```

## Advanced Scenarios

### Retry with Circuit Breaker

```csharp
// Custom transient fault detector
public class ApiTransientFaultDetector : ITransientFaultDetector
{
    private readonly ICircuitBreaker _circuitBreaker;
    
    public ApiTransientFaultDetector(ICircuitBreaker circuitBreaker)
    {
        _circuitBreaker = circuitBreaker;
    }
    
    public bool IsTransient(Exception exception)
    {
        // Don't retry if circuit is open
        if (_circuitBreaker.State == CircuitState.Open)
            return false;
        
        return exception switch
        {
            HttpRequestException httpEx => IsRetryableHttpError(httpEx),
            TaskCanceledException tcEx when tcEx.InnerException is TimeoutException => true,
            TimeoutException => true,
            SqlException sqlEx => IsTransientSqlError(sqlEx),
            _ => false
        };
    }
    
    private bool IsRetryableHttpError(HttpRequestException ex)
    {
        return ex.StatusCode is 
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
    }
    
    private bool IsTransientSqlError(SqlException ex)
    {
        // SQL Server transient error numbers
        var transientErrors = new[] { 49918, 49919, 49920, 4060, 40143, 233, 64 };
        return transientErrors.Contains(ex.Number);
    }
}

// Query with circuit breaker aware retry
public class GetExternalDataQuery : IQuery<Result<ExternalData>>, IRetryableQuery
{
    public string ApiEndpoint { get; init; }
    
    public RetryPolicy GetRetryPolicy() => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(5)
    };
}
```

### Multi-Module Event Mapping

```csharp
// Composite event mapper for multiple modules
public class ApplicationEventMapper : CompositeEventMapper
{
    public ApplicationEventMapper() : base(GetMappers())
    {
    }
    
    private static IEnumerable<IEventMapper> GetMappers()
    {
        yield return new OrderEventMapper();
        yield return new InventoryEventMapper();
        yield return new PaymentEventMapper();
        yield return new NotificationEventMapper();
    }
}

// Module-specific mapper
public class PaymentEventMapper : IEventMapper
{
    public IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event)
    {
        return @event switch
        {
            PaymentProcessedEvent e => new PaymentProcessedIntegrationEvent
            {
                PaymentId = e.PaymentId,
                OrderId = e.OrderId,
                Amount = e.Amount,
                ProcessedAt = e.ProcessedAt
            },
            RefundIssuedEvent e => new RefundIssuedIntegrationEvent
            {
                RefundId = e.RefundId,
                PaymentId = e.PaymentId,
                Amount = e.Amount,
                Reason = e.Reason
            },
            _ => null
        };
    }
    
    public IInternalCommand? MapToInternalCommand(IDomainEvent @event)
    {
        return @event switch
        {
            PaymentFailedEvent e => new RetryPaymentCommand
            {
                PaymentId = e.PaymentId,
                RetryCount = e.RetryCount + 1
            },
            ChargebackReceivedEvent e => new HandleChargebackCommand
            {
                PaymentId = e.PaymentId,
                ChargebackAmount = e.Amount,
                Reason = e.Reason
            },
            _ => null
        };
    }
}
```

## Testing Strategies

### Testing Pipeline Behaviors

```csharp
[Fact]
public async Task Command_Should_Be_Wrapped_In_Transaction()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));
    services.AddPipelineBehaviors();
    services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("test"));
    services.AddScoped<IOutboxService, MockOutboxService>();
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    var command = new CreateOrderCommand
    {
        CustomerId = Guid.NewGuid(),
        Items = [new OrderItemDto { ProductId = Guid.NewGuid(), Quantity = 1, Price = 10 }]
    };
    
    // Act
    var result = await mediator.Send(command);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    
    var dbContext = provider.GetRequiredService<AppDbContext>();
    var order = await dbContext.Orders.FindAsync(result.Value);
    order.Should().NotBeNull();
}

[Fact]
public async Task Cached_Query_Should_Return_Same_Result()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetProductQuery).Assembly));
    services.AddPipelineBehaviors();
    services.AddMemoryCache();
    services.AddDistributedMemoryCache();
    services.AddSingleton<ICacheKeyGenerator, DefaultCacheKeyGenerator>();
    
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();
    
    var query = new GetProductQuery 
    { 
        ProductId = Guid.NewGuid(),
        UseCache = true,
        CacheDuration = TimeSpan.FromMinutes(5)
    };
    
    // Act
    var result1 = await mediator.Send(query);
    var result2 = await mediator.Send(query);
    
    // Assert
    result1.Should().BeEquivalentTo(result2);
    // Handler should be called only once (verified via mock)
}
```

### Testing Validation

```csharp
[Fact]
public async Task Validator_Should_Use_Metadata_Context()
{
    // Arrange
    var validator = new CreateOrderValidator();
    var context = new ValidationContext(new TestRequest
    {
        Metadata = new Dictionary<string, object>
        {
            ["TenantId"] = "enterprise",
            ["Feature_RequireApproval"] = true
        }
    });
    
    validator.SetContext(context);
    
    var command = new CreateOrderCommand
    {
        CustomerId = Guid.NewGuid(),
        Items = [], // Empty items
        ApprovalCode = null // Missing when required
    };
    
    // Act
    var result = await validator.ValidateAsync(command);
    
    // Assert
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "ApprovalCode");
}
```

## Performance Tuning

### Cache Optimization

```csharp
// Configure Redis with connection pooling
services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { "redis-server:6379" },
        ConnectTimeout = 5000,
        SyncTimeout = 5000,
        AsyncTimeout = 5000,
        ConnectRetry = 3,
        DefaultDatabase = 0,
        AbortOnConnectFail = false,
        
        // Connection pool settings
        ConnectionMultiplexerFactory = async () =>
        {
            var multiplexer = await ConnectionMultiplexer.ConnectAsync(options.ConfigurationOptions);
            multiplexer.ConnectionFailed += (sender, args) =>
            {
                logger.LogError("Redis connection failed: {FailureType}", args.FailureType);
            };
            multiplexer.ConnectionRestored += (sender, args) =>
            {
                logger.LogInformation("Redis connection restored");
            };
            return multiplexer;
        }
    };
});
```

### Outbox Performance

```csharp
// High-throughput configuration
services.ConfigureOutboxProcessing(options =>
{
    // Process more events per batch
    options.BatchSize = 500;
    
    // Increase parallelism
    options.MaxConcurrency = Environment.ProcessorCount * 4;
    
    // Check more frequently
    options.ProcessingInterval = TimeSpan.FromSeconds(5);
    
    // Fail fast on retries
    options.MaxRetries = 3;
    options.BaseRetryDelayMinutes = 1;
    
    // Aggressive cleanup
    options.CompletedRetentionPeriod = TimeSpan.FromDays(1);
    options.CleanupBatchSize = 5000;
});

// Add custom metrics
services.AddSingleton<IOutboxMetrics, PrometheusOutboxMetrics>();
```

## Migration Guide

### Migrating from Direct Event Publishing

Before:
```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;
    
    public async Task CreateOrder(Order order)
    {
        await _repository.SaveAsync(order);
        
        // Direct publishing - not transactional!
        await _eventBus.PublishAsync(new OrderCreatedEvent(order.Id));
    }
}
```

After:
```csharp
public class CreateOrderCommand : ICommand<Result<Guid>>
{
    // Command properties
}

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = Order.Create(...);
        
        // Domain event - will be saved in outbox within transaction
        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        
        await _repository.AddAsync(order, ct);
        
        // Transaction and outbox handled by TransactionBehavior
        return Result<Guid>.Success(order.Id);
    }
}
```

### Migrating from Manual Caching

Before:
```csharp
public class ProductService
{
    private readonly IMemoryCache _cache;
    
    public async Task<Product> GetProductAsync(Guid id)
    {
        var key = $"product:{id}";
        
        if (_cache.TryGetValue(key, out Product cached))
            return cached;
        
        var product = await _repository.GetByIdAsync(id);
        
        _cache.Set(key, product, TimeSpan.FromMinutes(5));
        
        return product;
    }
}
```

After:
```csharp
public record GetProductQuery(Guid ProductId) : IQuery<Result<ProductDto>>
{
    public bool UseCache => true;
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    public string? CacheKeyPrefix => $"product:{ProductId}";
}

public class GetProductQueryHandler : IQueryHandler<GetProductQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, ct);
        
        if (product == null)
            return Result<ProductDto>.Failure(Error.NotFound());
        
        return Result<ProductDto>.Success(product.ToDto());
    }
}
```

## Summary

This usage guide provides comprehensive examples and patterns for implementing the BuildingBlocks Application Layer features. Key takeaways:

1. **Start Simple**: Begin with basic queries and commands, add behaviors incrementally
2. **Use Declarative Patterns**: Leverage attributes and interfaces for configuration
3. **Monitor Performance**: Use provided metrics and health checks
4. **Test Thoroughly**: Validate behavior ordering and integration
5. **Optimize Gradually**: Tune cache and outbox settings based on metrics

The layer provides production-ready patterns that scale from simple applications to complex distributed systems while maintaining consistency and reliability.