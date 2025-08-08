using FluentValidation;
using MediatR;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Integration;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Domain.Specifications;
using BuildingBlocks.Core.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Examples;

// ============================================================================
// EXAMPLE: Epic 2 + Epic 5 Integration - Complete Order Management System
// ============================================================================

// 1. DOMAIN MODELS (Epic 2 Rich Domain Models)
// ============================================================================

/// <summary>
/// Order aggregate demonstrating Epic 2 rich domain model patterns
/// with Epic 5 pipeline behavior integration.
/// </summary>
public sealed partial class Order : Aggregate<OrderId>
{
    private readonly List<OrderItem> _items = new();
    
    public UserId UserId { get; private set; }
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    // Private constructor for EF Core
    private Order() { }

    public static Result<Order> Create(UserId userId)
    {
        // Business rule validation before creation
        var rules = new RuleBuilder()
            .NotNull(userId, nameof(userId))
            .Build();

        if (rules.IsFailure)
            return Result<Order>.Failure(rules.Error);

        var order = new Order
        {
            Id = OrderId.New(),
            UserId = userId,
            TotalAmount = Money.Create(0, Currency.USD).Value,
            Status = OrderStatus.Draft
        };

        // Raise domain event (Epic 2 pattern)
        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, userId));
        
        return Result<Order>.Success(order);
    }

    /// <summary>
    /// Add item with comprehensive business rule validation (Epic 2 pattern).
    /// Domain events will be dispatched by Epic 5 TransactionBehavior.
    /// </summary>
    public Result<Unit> AddItem(ProductId productId, Money unitPrice, int quantity)
    {
        // 1. Check business rules FIRST (Epic 2 corrected pattern)
        var rules = new RuleBuilder()
            .Must(Status == OrderStatus.Draft, "ORDER_NOT_DRAFT", "Cannot modify confirmed order")
            .Must(quantity > 0, "INVALID_QUANTITY", "Quantity must be positive")
            .Must(_items.Count < 50, "TOO_MANY_ITEMS", "Order cannot exceed 50 items")
            .Must(!_items.Any(i => i.ProductId == productId), "DUPLICATE_PRODUCT", "Product already in order")
            .Build();

        if (rules.IsFailure)
            return rules;

        // 2. Create value objects with validation
        var itemResult = OrderItem.Create(productId, unitPrice, quantity);
        if (itemResult.IsFailure)
            return itemResult.Error;

        // 3. Apply state change (all preconditions validated)
        return ApplyChange(() =>
        {
            _items.Add(itemResult.Value);
            RecalculateTotal();
            
            // 4. Raise domain events after successful state change
            RaiseDomainEvent(new OrderItemAddedEvent(Id, productId, quantity));
        });
    }

    public Result<Unit> Confirm()
    {
        var rules = new RuleBuilder()
            .Must(Status == OrderStatus.Draft, "ORDER_NOT_DRAFT", "Order must be in draft status")
            .Must(_items.Any(), "ORDER_EMPTY", "Cannot confirm empty order")
            .Must(TotalAmount.Amount > 0, "INVALID_TOTAL", "Order total must be positive")
            .Build();

        if (rules.IsFailure)
            return rules;

        return ApplyChange(() =>
        {
            Status = OrderStatus.Confirmed;
            RaiseDomainEvent(new OrderConfirmedEvent(Id, UserId, TotalAmount));
        });
    }

    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new PredicateRule("ORDER_MUST_HAVE_USER", "Order must have a user", 
            () => UserId == null);
        yield return new PredicateRule("ORDER_TOTAL_POSITIVE_WHEN_CONFIRMED", "Order total must be positive when confirmed", 
            () => Status == OrderStatus.Confirmed && TotalAmount.Amount <= 0);
    }

    private void RecalculateTotal()
    {
        var total = _items.Sum(item => item.TotalPrice.Amount);
        TotalAmount = Money.Create(total, Currency.USD).Value;
    }
}

// 2. COMMANDS WITH EPIC 5 INTEGRATION
// ============================================================================

/// <summary>
/// Command demonstrating Epic 2 domain patterns with Epic 5 pipeline behaviors.
/// Integrates with ValidationBehavior, TransactionBehavior, and ObservabilityBehavior.
/// </summary>
public sealed record CreateOrderCommand : DomainCommandBase, IRequest<Result<OrderId>>, IRetryableOperation
{
    public UserId UserId { get; init; }
    public List<CreateOrderItem> Items { get; init; } = new();

    public override Type GetAggregateType() => typeof(Order);

    public override Validation<Unit> ValidateDomainRules()
    {
        var rules = new RuleBuilder()
            .NotNull(UserId, nameof(UserId))
            .NotEmpty(Items, nameof(Items))
            .Must(Items.Count <= 50, "TOO_MANY_ITEMS", "Cannot create order with more than 50 items")
            .Build();

        return rules.IsSuccess 
            ? Validation<Unit>.Valid(Unit.Value)
            : Validation<Unit>.Invalid(rules.Error);
    }

    // Epic 5 RetryBehavior integration
    public string? GetRetryPolicyName() => "StandardRetry";
}

public sealed record CreateOrderItem
{
    public ProductId ProductId { get; init; }
    public decimal UnitPrice { get; init; }
    public Currency Currency { get; init; }
    public int Quantity { get; init; }
}

/// <summary>
/// FluentValidation validator for structural validation (Epic 5 ValidationBehavior).
/// Works alongside Epic 2 domain rules validation.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotNull()
            .WithMessage("User ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item")
            .Must(items => items.Count <= 50)
            .WithMessage("Order cannot have more than 50 items");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ProductId).NotNull();
                item.RuleFor(x => x.UnitPrice).GreaterThan(0);
                item.RuleFor(x => x.Quantity).GreaterThan(0);
                item.RuleFor(x => x.Currency).NotEqual(Currency.None);
            });
    }
}

// 3. QUERIES WITH SPECIFICATIONS (Epic 2 + Epic 5)
// ============================================================================

/// <summary>
/// Query demonstrating Epic 2 specifications with Epic 5 caching behavior.
/// </summary>
public sealed record GetActiveOrdersQuery : CacheableDomainQueryBase, IRequest<Result<List<OrderDto>>>
{
    public UserId UserId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int? MinItems { get; init; }

    public override string GetCacheKey()
    {
        return $"ActiveOrders:{UserId}:{FromDate:yyyyMMdd}:{ToDate:yyyyMMdd}:{MinItems}";
    }

    public override TimeSpan? GetCacheDuration() => TimeSpan.FromMinutes(5);

    public override IEnumerable<string> GetCacheTags() 
    {
        yield return $"User:{UserId}";
        yield return "Orders";
    }
}

/// <summary>
/// Custom specification for complex order filtering (Epic 2 specifications).
/// </summary>
public sealed class OrdersByUserAndCriteriaSpec : Specification<Order>
{
    private readonly UserId _userId;
    private readonly DateTime? _fromDate;
    private readonly DateTime? _toDate;
    private readonly int? _minItems;

    public OrdersByUserAndCriteriaSpec(
        UserId userId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null,
        int? minItems = null)
    {
        _userId = userId;
        _fromDate = fromDate;
        _toDate = toDate;
        _minItems = minItems;
    }

    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.UserId == _userId
                       && (_fromDate == null || order.CreatedAt >= _fromDate)
                       && (_toDate == null || order.CreatedAt <= _toDate)
                       && (_minItems == null || order.Items.Count >= _minItems);
    }
}

// 4. COMMAND/QUERY HANDLERS
// ============================================================================

/// <summary>
/// Command handler demonstrating Epic 2 + Epic 5 integration.
/// Pipeline behaviors will handle validation, transactions, and domain events.
/// </summary>
public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderId>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<Result<OrderId>> Handle(
        CreateOrderCommand request, 
        CancellationToken cancellationToken)
    {
        // ValidationBehavior has already validated the request
        // TransactionBehavior will wrap this in a transaction
        
        _logger.LogInformation("Creating order for user {UserId}", request.UserId);

        // 1. Create order aggregate (Epic 2 pattern)
        var orderResult = Order.Create(request.UserId);
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;

        // 2. Add items with business rule validation
        foreach (var item in request.Items)
        {
            var money = Money.Create(item.UnitPrice, item.Currency);
            if (money.IsFailure)
                return money.Error;

            var addResult = order.AddItem(item.ProductId, money.Value, item.Quantity);
            if (addResult.IsFailure)
                return addResult.Error;
        }

        // 3. Save aggregate (domain events will be dispatched by TransactionBehavior)
        await _orderRepository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created successfully", order.Id);
        
        return Result<OrderId>.Success(order.Id);
    }
}

/// <summary>
/// Query handler demonstrating Epic 2 specifications with Epic 5 caching.
/// </summary>
public sealed class GetActiveOrdersQueryHandler : IRequestHandler<GetActiveOrdersQuery, Result<List<OrderDto>>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetActiveOrdersQueryHandler> _logger;

    public GetActiveOrdersQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetActiveOrdersQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<Result<List<OrderDto>>> Handle(
        GetActiveOrdersQuery request, 
        CancellationToken cancellationToken)
    {
        // CachingBehavior will check cache first
        // This handler only runs if cache miss occurs

        _logger.LogDebug("Fetching active orders for user {UserId}", request.UserId);

        // Use Epic 2 specifications for complex queries
        var spec = CommonSpecifications.Active<Order>()
            .And(new OrdersByUserAndCriteriaSpec(
                request.UserId, 
                request.FromDate, 
                request.ToDate, 
                request.MinItems));

        var orders = await _orderRepository.FindAsync(spec, cancellationToken);
        
        var orderDtos = orders.Select(order => new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount.Amount,
            Currency = order.TotalAmount.Currency,
            Status = order.Status,
            ItemCount = order.Items.Count,
            CreatedAt = order.CreatedAt
        }).ToList();

        return Result<List<OrderDto>>.Success(orderDtos);
    }
}

// 5. DOMAIN EVENT HANDLERS (Epic 2 + Epic 5)
// ============================================================================

/// <summary>
/// Domain event handler demonstrating Epic 2 event patterns with Epic 5 pipeline behaviors.
/// Events are dispatched by TransactionBehavior after successful commits.
/// </summary>
public sealed class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderConfirmedEventHandler> _logger;

    public OrderConfirmedEventHandler(
        IEmailService emailService,
        ILogger<OrderConfirmedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing order confirmed event for order {OrderId}",
            notification.OrderId);

        // Send confirmation email
        await _emailService.SendOrderConfirmationAsync(
            notification.OrderId,
            notification.UserId,
            notification.TotalAmount,
            cancellationToken);

        _logger.LogInformation(
            "Order confirmation email sent for order {OrderId}",
            notification.OrderId);
    }
}

// 6. REPOSITORY WITH SPECIFICATIONS
// ============================================================================

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default);
    Task<List<Order>> FindAsync(Specification<Order> specification, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

// 7. SUPPORTING TYPES
// ============================================================================

public sealed record OrderDto
{
    public OrderId Id { get; init; }
    public UserId UserId { get; init; }
    public decimal TotalAmount { get; init; }
    public Currency Currency { get; init; }
    public OrderStatus Status { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public enum OrderStatus
{
    Draft = 1,
    Confirmed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public interface IEmailService
{
    Task SendOrderConfirmationAsync(
        OrderId orderId,
        UserId userId,
        Money totalAmount,
        CancellationToken cancellationToken = default);
}

// 8. PIPELINE CONFIGURATION (Epic 5)
// ============================================================================

/// <summary>
/// Example service registration showing Epic 2 + Epic 5 integration.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Epic 2 domain services
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        
        // Epic 5 pipeline behaviors (order matters!)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly);
            
            // Critical: Registration order defines pipeline execution order
            cfg.AddOpenBehavior(typeof(ObservabilityBehavior<,>));     // Outermost: telemetry
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));           // Structured logging  
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));        // Epic 2 + FluentValidation
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));          // Query caching
            cfg.AddOpenBehavior(typeof(RetryBehavior<,>));            // Resilience
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));      // Innermost: transactions + events
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(CreateOrderCommandValidator).Assembly);

        return services;
    }
}

// ============================================================================
// USAGE EXAMPLES
// ============================================================================

/// <summary>
/// Complete example showing Epic 2 + Epic 5 integration in action.
/// </summary>
public sealed class IntegratedUsageDemo
{
    private readonly IMediator _mediator;
    private readonly IOrderRepository _repository;

    public IntegratedUsageDemo(IMediator mediator, IOrderRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    /// <summary>
    /// Example: Creating an order with full Epic 2 + Epic 5 pipeline.
    /// </summary>
    public async Task<Result<OrderId>> CreateOrderExample()
    {
        var command = new CreateOrderCommand
        {
            UserId = UserId.New(),
            Items = new List<CreateOrderItem>
            {
                new() { ProductId = ProductId.New(), UnitPrice = 29.99m, Currency = Currency.USD, Quantity = 2 },
                new() { ProductId = ProductId.New(), UnitPrice = 15.50m, Currency = Currency.USD, Quantity = 1 }
            }
        };

        // This will go through the full Epic 5 pipeline:
        // 1. ObservabilityBehavior - telemetry and tracing
        // 2. LoggingBehavior - structured logging
        // 3. ValidationBehavior - FluentValidation + Epic 2 domain rules
        // 4. CachingBehavior - skipped for commands
        // 5. RetryBehavior - retry on transient failures
        // 6. TransactionBehavior - database transaction + domain event dispatch
        // 7. Handler execution with Epic 2 domain patterns
        
        return await _mediator.Send(command);
    }

    /// <summary>
    /// Example: Querying orders with specifications and caching.
    /// </summary>
    public async Task<Result<List<OrderDto>>> GetOrdersExample(UserId userId)
    {
        var query = new GetActiveOrdersQuery
        {
            UserId = userId,
            FromDate = DateTime.UtcNow.AddDays(-30),
            MinItems = 1
        };

        // This will go through Epic 5 pipeline:
        // 1. ObservabilityBehavior - telemetry
        // 2. LoggingBehavior - structured logging  
        // 3. ValidationBehavior - validation (minimal for queries)
        // 4. CachingBehavior - cache check/store using Epic 2 cache keys
        // 5. RetryBehavior - retry on failures
        // 6. Handler execution with Epic 2 specifications
        
        return await _mediator.Send(query);
    }

    /// <summary>
    /// Example: Using Epic 2 specifications directly in repository.
    /// </summary>
    public async Task<List<Order>> SpecificationExample(UserId userId)
    {
        // Compose specifications using Epic 2 fluent API
        var recentHighValueOrders = CommonSpecifications.Active<Order>()
            .And(CommonSpecifications.CreatedAfter<Order>(DateTime.UtcNow.AddDays(-7)))
            .And(new OrdersByUserAndCriteriaSpec(userId, minItems: 3));

        // Repository uses specifications for database queries
        return await _repository.FindAsync(recentHighValueOrders);
    }
}