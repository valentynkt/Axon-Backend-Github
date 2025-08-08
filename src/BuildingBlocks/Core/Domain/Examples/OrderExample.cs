using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Domain.ValueObjects;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Model;

namespace BuildingBlocks.Core.Domain.Examples;

/// <summary>
/// Example demonstrating Epic 2 Domain Enhancement patterns in action.
/// Shows proper usage of Entity, AggregateRoot, ValueObjects, and Business Rules.
/// </summary>

#region Strong IDs

public readonly record struct OrderId : IStrongId<Guid>
{
    public Guid Value { get; }
    
    private OrderId(Guid value)
    {
        Value = value;
    }
    
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
    
    public object GetValue() => Value;
    public Type GetValueType() => typeof(Guid);
    
    public static implicit operator Guid(OrderId id) => id.Value;
    public static implicit operator OrderId(Guid value) => From(value);
}

public readonly record struct UserId : IStrongId<Guid>
{
    public Guid Value { get; }
    
    private UserId(Guid value)
    {
        Value = value;
    }
    
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    
    public object GetValue() => Value;
    public Type GetValueType() => typeof(Guid);
    
    public static implicit operator Guid(UserId id) => id.Value;
    public static implicit operator UserId(Guid value) => From(value);
}

#endregion

#region Domain Events

public sealed record OrderCreatedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public UserId UserId { get; }
    
    public OrderCreatedEvent(OrderId orderId, UserId userId)
    {
        OrderId = orderId;
        UserId = userId;
    }
}

public sealed record OrderItemAddedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public string ProductName { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    
    public OrderItemAddedEvent(OrderId orderId, string productName, int quantity, Money unitPrice)
    {
        OrderId = orderId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

#endregion

#region Value Objects

public sealed record OrderItem : ValueObject
{
    public string ProductName { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; }
    
    private OrderItem(string productName, Money unitPrice, int quantity)
    {
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
    
    public static Result<OrderItem> Create(string productName, Money unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Result<OrderItem>.Failure(Error.Validation("Product name cannot be empty", "PRODUCT_NAME_EMPTY"));
            
        if (quantity <= 0)
            return Result<OrderItem>.Failure(Error.Validation("Quantity must be positive", "QUANTITY_INVALID"));
            
        if (!unitPrice.IsPositive)
            return Result<OrderItem>.Failure(Error.Validation("Unit price must be positive", "UNIT_PRICE_INVALID"));
            
        return Result<OrderItem>.Success(new OrderItem(productName, unitPrice, quantity));
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductName;
        yield return UnitPrice;
        yield return Quantity;
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(ProductName))
            errors.Add(Error.Validation("Product name cannot be empty", "PRODUCT_NAME_EMPTY"));
            
        if (Quantity <= 0)
            errors.Add(Error.Validation("Quantity must be positive", "QUANTITY_INVALID"));
            
        var priceValidation = UnitPrice.Validate();
        if (priceValidation.IsInvalid)
            errors.AddRange(priceValidation.Errors);
            
        if (UnitPrice.IsValid && !UnitPrice.IsPositive)
            errors.Add(Error.Validation("Unit price must be positive", "UNIT_PRICE_INVALID"));
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
    
    public Money TotalPrice => UnitPrice.Multiply(Quantity).Value; // Safe because validation ensures positive values
}

#endregion

#region Business Rules

public sealed record OrderMustHaveCustomerRule : BusinessRule
{
    private readonly UserId? _customerId;
    
    public OrderMustHaveCustomerRule(UserId? customerId)
    {
        _customerId = customerId;
    }
    
    public override string Code => "ORDER_MUST_HAVE_CUSTOMER";
    public override string Message => "Order must have a customer";
    
    public override bool IsBroken() => _customerId == null || _customerId.Value.Value == Guid.Empty;
}

public sealed record OrderCannotExceedItemLimitRule : BusinessRule
{
    private readonly int _currentItemCount;
    private readonly int _maxItems;
    
    public OrderCannotExceedItemLimitRule(int currentItemCount, int maxItems = 50)
    {
        _currentItemCount = currentItemCount;
        _maxItems = maxItems;
    }
    
    public override string Code => "ORDER_ITEM_LIMIT_EXCEEDED";
    public override string Message => $"Order cannot exceed {_maxItems} items";
    
    public override bool IsBroken() => _currentItemCount >= _maxItems;
}

#endregion

#region Aggregate Root

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

public sealed partial class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = new();
    
    public UserId CustomerId { get; private set; }
    public Email CustomerEmail { get; private set; }
    public Money TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    // Parameterless constructor for EF Core
    private Order() : base()
    {
        CustomerId = default!;
        CustomerEmail = default!;
        TotalAmount = default!;
    }
    
    private Order(OrderId id, UserId customerId, Email customerEmail) : base(id)
    {
        CustomerId = customerId;
        CustomerEmail = customerEmail;
        Status = OrderStatus.Draft;
        TotalAmount = Money.Zero(Currency.USD).Value; // Default currency
    }
    
    public static Result<Order> Create(UserId customerId, Email customerEmail)
    {
        // Validate inputs using business rules
        var rules = new RuleBuilder()
            .NotNull(customerId, nameof(customerId))
            .NotNull(customerEmail, nameof(customerEmail))
            .AddRule(new OrderMustHaveCustomerRule(customerId))
            .Build();
            
        if (rules.IsFailure)
            return Result<Order>.Failure(rules.Error);
        
        var order = new Order(OrderId.New(), customerId, customerEmail);
        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, customerId));
        
        return Result<Order>.Success(order);
    }
    
    public Result<Unit> AddItem(string productName, Money unitPrice, int quantity)
    {
        // 1. Check all business rules FIRST (before any state changes)
        var rules = new RuleBuilder()
            .Must(Status == OrderStatus.Draft, "ORDER_NOT_MODIFIABLE", "Order can only be modified in Draft status")
            .Must(quantity > 0, "INVALID_QUANTITY", "Quantity must be positive")
            .NotEmpty(productName, nameof(productName))
            .AddRule(new OrderCannotExceedItemLimitRule(_items.Count + 1))
            .Build();
            
        if (rules.IsFailure)
            return rules;
        
        // 2. Create value objects with validation
        var itemResult = OrderItem.Create(productName, unitPrice, quantity);
        if (itemResult.IsFailure)
            return Result<Unit>.Failure(itemResult.Error);
            
        // 3. Apply state change (all preconditions validated)
        return ApplyChange(() =>
        {
            _items.Add(itemResult.Value);
            RecalculateTotal();
            
            // 4. Raise domain events after successful state change
            RaiseDomainEvent(new OrderItemAddedEvent(Id, productName, quantity, unitPrice));
        });
    }
    
    public Result<Unit> ConfirmOrder()
    {
        // Check business rules before state change
        var rules = new RuleBuilder()
            .Must(Status == OrderStatus.Draft, "ORDER_ALREADY_CONFIRMED", "Order is already confirmed")
            .Must(_items.Count > 0, "ORDER_EMPTY", "Cannot confirm empty order")
            .Must(TotalAmount.IsPositive, "ORDER_INVALID_TOTAL", "Order total must be positive")
            .Build();
            
        if (rules.IsFailure)
            return rules;
            
        return ApplyChange(() =>
        {
            Status = OrderStatus.Confirmed;
        });
    }
    
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new OrderMustHaveCustomerRule(CustomerId);
        yield return new PredicateRule("ORDER_TOTAL_CONSISTENCY", "Order total must match sum of items", 
            () => _items.Count > 0 && !TotalAmount.Equals(CalculateItemsTotal()));
    }
    
    private void RecalculateTotal()
    {
        var total = _items.Count > 0 
            ? _items.Select(item => item.TotalPrice.Amount).Sum()
            : 0;
            
        TotalAmount = Money.Create(total, Currency.USD).Value; // Assumes USD for simplicity
    }
    
    private Money CalculateItemsTotal()
    {
        if (_items.Count == 0)
            return Money.Zero(Currency.USD).Value;
            
        var total = _items.Select(item => item.TotalPrice.Amount).Sum();
        return Money.Create(total, Currency.USD).Value;
    }
}

#endregion

#region Usage Examples

/// <summary>
/// Example service showing proper usage patterns
/// </summary>
public class OrderService
{
    public async Task<Result<Order>> CreateOrderAsync(string customerEmail, string customerName)
    {
        // 1. Create value objects using Result pattern
        var emailResult = Email.Create(customerEmail);
        if (emailResult.IsFailure)
            return Result<Order>.Failure(emailResult.Error);
        
        // 2. Create aggregate using factory method
        var customerId = UserId.New();
        var orderResult = Order.Create(customerId, emailResult.Value);
        if (orderResult.IsFailure)
            return Result<Order>.Failure(orderResult.Error);
        
        var order = orderResult.Value;
        
        // 3. Add items using business logic
        var priceResult = Money.Create(29.99m, Currency.USD);
        if (priceResult.IsFailure)
            return Result<Order>.Failure(priceResult.Error);
            
        var addItemResult = order.AddItem("Sample Product", priceResult.Value, 2);
        if (addItemResult.IsFailure)
            return Result<Order>.Failure(addItemResult.Error);
        
        // 4. Validate aggregate state
        var validation = order.Validate();
        if (validation.IsInvalid)
            return validation.ToResultWithAggregatedError();
        
        return Result<Order>.Success(order);
    }
}

#endregion