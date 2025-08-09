# BuildingBlocks.Core Usage Guide

## Table of Contents

1. [Getting Started](#getting-started)
2. [Creating Domain Models](#creating-domain-models)
3. [Implementing CQRS](#implementing-cqrs)
4. [Error Handling Patterns](#error-handling-patterns)
5. [Working with Value Objects](#working-with-value-objects)
6. [Using Specifications](#using-specifications)
7. [Event-Driven Architecture](#event-driven-architecture)
8. [Advanced Patterns](#advanced-patterns)
9. [Real-World Examples](#real-world-examples)

## Getting Started

### Prerequisites

- .NET 10.0 Preview
- C# 13 with nullable reference types enabled
- Understanding of DDD, CQRS, and functional programming concepts

### Basic Setup

```csharp
// Global usings typically in GlobalUsings.cs
global using BuildingBlocks.Core.Functional;
global using BuildingBlocks.Core.Functional.Results;
global using BuildingBlocks.Core.Diagnostics.Errors;
global using BuildingBlocks.Core.Domain.Primitives;
```

## Creating Domain Models

### Step 1: Define Strong IDs

Strong IDs provide type safety and prevent primitive obsession.

```csharp
// Define domain-specific ID types
public record UserId(Guid Value) : GuidStrongId(Value)
{
    // Factory method for creating new IDs
    public static UserId New() => new(Guid.NewGuid());
    
    // Parse from string with validation
    public static Result<UserId> FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<UserId>.Failure(Error.Validation("UserId cannot be empty"));
            
        return Guid.TryParse(value, out var guid) && guid != Guid.Empty
            ? Result<UserId>.Success(new UserId(guid))
            : Result<UserId>.Failure(Error.Validation("Invalid UserId format"));
    }
}

public record OrderId(Guid Value) : GuidStrongId(Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}

public record ProductId(int Value) : IntStrongId(Value);
```

### Step 2: Create Value Objects

Value objects encapsulate domain concepts with validation.

```csharp
public sealed record CustomerName : ValueObject
{
    public string FirstName { get; }
    public string LastName { get; }
    public string FullName => $"{FirstName} {LastName}";
    
    private CustomerName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }
    
    public static Result<CustomerName> Create(string firstName, string lastName)
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(firstName))
            errors.Add(Error.Validation("First name is required", "FIRST_NAME_REQUIRED"));
        else if (firstName.Length > 50)
            errors.Add(Error.Validation("First name too long", "FIRST_NAME_TOO_LONG"));
            
        if (string.IsNullOrWhiteSpace(lastName))
            errors.Add(Error.Validation("Last name is required", "LAST_NAME_REQUIRED"));
        else if (lastName.Length > 50)
            errors.Add(Error.Validation("Last name too long", "LAST_NAME_TOO_LONG"));
            
        if (errors.Count != 0)
            return Result<CustomerName>.Failure(Error.Aggregate(errors.ToArray()));
            
        return Result<CustomerName>.Success(new CustomerName(firstName.Trim(), lastName.Trim()));
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
    }
    
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(FirstName))
            errors.Add(Error.Validation("First name is required"));
        if (string.IsNullOrWhiteSpace(LastName))
            errors.Add(Error.Validation("Last name is required"));
            
        return errors.Count != 0 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }
}

// Address value object with multiple properties
public sealed record Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }
    
    private Address(string street, string city, string postalCode, string country)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }
    
    public static Result<Address> Create(
        string street,
        string city,
        string postalCode,
        string country)
    {
        // Validation logic
        if (string.IsNullOrWhiteSpace(street))
            return Result<Address>.Failure(Error.Validation("Street is required"));
        if (string.IsNullOrWhiteSpace(city))
            return Result<Address>.Failure(Error.Validation("City is required"));
        if (!IsValidPostalCode(postalCode))
            return Result<Address>.Failure(Error.Validation("Invalid postal code"));
        if (string.IsNullOrWhiteSpace(country))
            return Result<Address>.Failure(Error.Validation("Country is required"));
            
        return Result<Address>.Success(new Address(street, city, postalCode, country));
    }
    
    private static bool IsValidPostalCode(string postalCode)
    {
        // Implement postal code validation
        return !string.IsNullOrWhiteSpace(postalCode) && postalCode.Length <= 10;
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
    
    public override Validation<Unit> Validate()
    {
        // Implementation
        return Validation<Unit>.Valid(Unit.Value);
    }
}
```

### Step 3: Define Entities

Entities have identity and lifecycle.

```csharp
public class Customer : Entity<CustomerId>
{
    public CustomerName Name { get; private set; }
    public Email Email { get; private set; }
    public Address? ShippingAddress { get; private set; }
    public CustomerStatus Status { get; private set; }
    
    private Customer(CustomerId id, CustomerName name, Email email) : base(id)
    {
        Name = name;
        Email = email;
        Status = CustomerStatus.Active;
    }
    
    // Factory method with validation
    public static Result<Customer> Create(CustomerName name, Email email)
    {
        return Result<Customer>.Success(new Customer(CustomerId.New(), name, email));
    }
    
    // Business operations
    public Result<Unit> UpdateEmail(Email newEmail)
    {
        if (Email.Equals(newEmail))
            return Result<Unit>.Success(Unit.Value);
            
        Email = newEmail;
        Touch(); // Update timestamp
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> SetShippingAddress(Address address)
    {
        ShippingAddress = address;
        Touch();
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            return Result<Unit>.Failure(Error.BusinessRule("Customer is already inactive"));
            
        Status = CustomerStatus.Inactive;
        Touch();
        return Result<Unit>.Success(Unit.Value);
    }
}

public enum CustomerStatus
{
    Active,
    Inactive,
    Suspended
}
```

### Step 4: Create Aggregates

Aggregates maintain consistency and enforce business rules.

```csharp
public class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = new();
    
    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public Address ShippingAddress { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    
    private Order(
        OrderId id,
        CustomerId customerId,
        Address shippingAddress) : base(id)
    {
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Draft;
        TotalAmount = Money.Zero(Currency.USD).Value;
    }
    
    // Factory method
    public static Result<Order> Create(
        CustomerId customerId,
        Address shippingAddress)
    {
        return Result<Order>.Success(new Order(
            OrderId.New(),
            customerId,
            shippingAddress));
    }
    
    // Business operations with rule checking
    public Result<Unit> AddItem(ProductId productId, Money price, int quantity)
    {
        // Check business rules
        var ruleResult = CheckRules(
            new OrderMustBeDraftRule(Status),
            new QuantityMustBePositiveRule(quantity),
            new PriceMustBePositiveRule(price));
            
        if (ruleResult.IsFailure)
            return ruleResult;
            
        // Apply state changes
        return ApplyChange(() =>
        {
            var item = new OrderItem(productId, price, quantity);
            _items.Add(item);
            RecalculateTotal();
            
            // Raise domain event
            RaiseDomainEvent(new OrderItemAddedEvent(
                Id,
                productId,
                price,
                quantity));
        });
    }
    
    public Result<Unit> RemoveItem(ProductId productId)
    {
        var ruleResult = CheckRule(new OrderMustBeDraftRule(Status));
        if (ruleResult.IsFailure)
            return ruleResult;
            
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            return Result<Unit>.Failure(Error.NotFound($"Item {productId} not found"));
            
        return ApplyChange(() =>
        {
            _items.Remove(item);
            RecalculateTotal();
            RaiseDomainEvent(new OrderItemRemovedEvent(Id, productId));
        });
    }
    
    public Result<Unit> Confirm()
    {
        var ruleResult = CheckRules(
            new OrderMustBeDraftRule(Status),
            new OrderMustHaveItemsRule(_items));
            
        if (ruleResult.IsFailure)
            return ruleResult;
            
        return ApplyChange(() =>
        {
            Status = OrderStatus.Confirmed;
            ConfirmedAt = DateTime.UtcNow;
            RaiseDomainEvent(new OrderConfirmedEvent(Id, CustomerId, TotalAmount));
        });
    }
    
    private void RecalculateTotal()
    {
        var total = _items
            .Select(item => item.GetTotal())
            .Aggregate(
                Money.Zero(Currency.USD).Value,
                (acc, money) => acc.Add(money).Value);
                
        TotalAmount = total;
    }
    
    // Override to provide invariants
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new TotalMustMatchItemsSumRule(this);
        yield return new OrderMustHaveCustomerRule(CustomerId);
    }
}

// Business Rules
public class OrderMustBeDraftRule : IBusinessRule
{
    private readonly OrderStatus _status;
    
    public OrderMustBeDraftRule(OrderStatus status)
    {
        _status = status;
    }
    
    public string Code => "ORDER_MUST_BE_DRAFT";
    public string Message => "Order must be in draft status";
    
    public bool IsBroken() => _status != OrderStatus.Draft;
}

public class QuantityMustBePositiveRule : IBusinessRule
{
    private readonly int _quantity;
    
    public QuantityMustBePositiveRule(int quantity)
    {
        _quantity = quantity;
    }
    
    public string Code => "QUANTITY_MUST_BE_POSITIVE";
    public string Message => "Quantity must be greater than zero";
    
    public bool IsBroken() => _quantity <= 0;
}
```

## Implementing CQRS

### Commands

```csharp
// Command definition
public sealed record CreateOrderCommand : ICommand<OrderDto>
{
    public Guid CustomerId { get; init; }
    public string Street { get; init; }
    public string City { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }
    public List<OrderItemDto> Items { get; init; } = new();
}

// Command handler
public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    
    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<Result<OrderDto>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Parse and validate IDs
        var customerIdResult = CustomerId.FromString(command.CustomerId.ToString());
        if (customerIdResult.IsFailure)
            return Result<OrderDto>.Failure(customerIdResult.Error);
            
        // Check customer exists
        var customerExists = await _customerRepository.ExistsAsync(
            customerIdResult.Value,
            cancellationToken);
            
        if (!customerExists)
            return Result<OrderDto>.Failure(
                Error.NotFound($"Customer {command.CustomerId} not found"));
                
        // Create address
        var addressResult = Address.Create(
            command.Street,
            command.City,
            command.PostalCode,
            command.Country);
            
        if (addressResult.IsFailure)
            return Result<OrderDto>.Failure(addressResult.Error);
            
        // Create order
        var orderResult = Order.Create(
            customerIdResult.Value,
            addressResult.Value);
            
        if (orderResult.IsFailure)
            return Result<OrderDto>.Failure(orderResult.Error);
            
        var order = orderResult.Value;
        
        // Add items
        foreach (var itemDto in command.Items)
        {
            var productId = new ProductId(itemDto.ProductId);
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            
            if (product == null)
                return Result<OrderDto>.Failure(
                    Error.NotFound($"Product {itemDto.ProductId} not found"));
                    
            var priceResult = Money.Create(product.Price, Currency.USD);
            if (priceResult.IsFailure)
                return Result<OrderDto>.Failure(priceResult.Error);
                
            var addItemResult = order.AddItem(productId, priceResult.Value, itemDto.Quantity);
            if (addItemResult.IsFailure)
                return Result<OrderDto>.Failure(addItemResult.Error);
        }
        
        // Save order
        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // Map to DTO
        var orderDto = new OrderDto
        {
            Id = order.Id.Value,
            CustomerId = order.CustomerId.Value,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount.Amount,
            Currency = order.TotalAmount.Currency.ToString(),
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId.Value,
                Price = i.Price.Amount,
                Quantity = i.Quantity
            }).ToList()
        };
        
        return Result<OrderDto>.Success(orderDto);
    }
}
```

### Queries

```csharp
// Query definition with caching support
public sealed record GetOrderByIdQuery : IQuery<OrderDto>
{
    public Guid OrderId { get; init; }
    
    // Enable caching for 5 minutes
    public bool UseCache => true;
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    public string CacheKeyPrefix => $"Order_{OrderId}";
}

// Query handler
public sealed class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderReadRepository _repository;
    
    public GetOrderByIdQueryHandler(IOrderReadRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Result<OrderDto>> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var orderIdResult = OrderId.FromString(query.OrderId.ToString());
        if (orderIdResult.IsFailure)
            return Result<OrderDto>.Failure(orderIdResult.Error);
            
        var order = await _repository.GetByIdAsync(
            orderIdResult.Value,
            cancellationToken);
            
        if (order == null)
            return Result<OrderDto>.Failure(
                Error.NotFound($"Order {query.OrderId} not found"));
                
        var dto = MapToDto(order);
        return Result<OrderDto>.Success(dto);
    }
    
    private OrderDto MapToDto(OrderReadModel order)
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Items = order.Items
        };
    }
}

// Paginated query
public sealed record GetOrdersQuery : IQuery<PagedResult<OrderDto>>, IPageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public OrderStatus? StatusFilter { get; init; }
    
    public bool UseCache => false; // Don't cache paginated results
    public TimeSpan? CacheDuration => null;
}
```

## Error Handling Patterns

### Railway-Oriented Programming

```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;
    
    public async Task<Result<ShipmentInfo>> ProcessOrderAsync(OrderId orderId)
    {
        // Chain operations using railway-oriented programming
        return await GetOrder(orderId)
            .BindAsync(ValidateOrder)
            .BindAsync(ProcessPayment)
            .BindAsync(ScheduleShipping)
            .TapAsync(SendConfirmationEmail)
            .MapAsync(CreateShipmentInfo);
    }
    
    private async Task<Result<Order>> GetOrder(OrderId orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        return order != null
            ? Result<Order>.Success(order)
            : Result<Order>.Failure(Error.NotFound($"Order {orderId} not found"));
    }
    
    private async Task<Result<Order>> ValidateOrder(Order order)
    {
        if (order.Status != OrderStatus.Confirmed)
            return Result<Order>.Failure(
                Error.BusinessRule("Order must be confirmed"));
                
        if (order.TotalAmount.Amount <= 0)
            return Result<Order>.Failure(
                Error.BusinessRule("Order total must be positive"));
                
        return Result<Order>.Success(order);
    }
    
    private async Task<Result<PaymentResult>> ProcessPayment(Order order)
    {
        try
        {
            var result = await _paymentService.ChargeAsync(
                order.CustomerId,
                order.TotalAmount);
                
            return result.IsSuccess
                ? Result<PaymentResult>.Success(result)
                : Result<PaymentResult>.Failure(
                    Error.External("Payment failed", "PAYMENT_FAILED"));
        }
        catch (PaymentException ex)
        {
            return Result<PaymentResult>.Failure(
                Error.FromException(ex));
        }
    }
    
    private async Task<Result<ShippingSchedule>> ScheduleShipping(
        PaymentResult payment)
    {
        var schedule = await _shippingService.ScheduleAsync(
            payment.OrderId,
            payment.TransactionId);
            
        return Result<ShippingSchedule>.Success(schedule);
    }
}
```

### Error Recovery

```csharp
public class ResilientOrderService
{
    public async Task<Result<Order>> GetOrderWithFallbackAsync(OrderId orderId)
    {
        // Try primary source
        var result = await GetFromPrimaryAsync(orderId);
        
        // Fallback to cache if primary fails
        return result.RecoverWith(async error =>
        {
            _logger.LogWarning("Primary failed: {Error}, trying cache", error);
            return await GetFromCacheAsync(orderId);
        })
        // Fallback to secondary source if cache fails
        .RecoverWith(async error =>
        {
            _logger.LogWarning("Cache failed: {Error}, trying secondary", error);
            return await GetFromSecondaryAsync(orderId);
        })
        // Final fallback
        .Recover(error =>
        {
            _logger.LogError("All sources failed: {Error}", error);
            return CreateDefaultOrder(orderId);
        });
    }
}
```

## Working with Value Objects

### Money Operations

```csharp
public class PricingService
{
    public Result<Invoice> CalculateInvoice(Order order, decimal taxRate)
    {
        // Calculate subtotal
        var subtotalResult = order.Items
            .Select(item => item.GetTotal())
            .Aggregate(
                Money.Zero(Currency.USD),
                (acc, money) => acc.Bind(a => a.Add(money)));
                
        if (subtotalResult.IsFailure)
            return Result<Invoice>.Failure(subtotalResult.Error);
            
        // Calculate tax
        var taxResult = subtotalResult.Value
            .Multiply(taxRate)
            .Map(tax => tax.Round(2));
            
        if (taxResult.IsFailure)
            return Result<Invoice>.Failure(taxResult.Error);
            
        // Calculate total
        var totalResult = subtotalResult.Value.Add(taxResult.Value);
        if (totalResult.IsFailure)
            return Result<Invoice>.Failure(totalResult.Error);
            
        return Result<Invoice>.Success(new Invoice
        {
            Subtotal = subtotalResult.Value,
            Tax = taxResult.Value,
            Total = totalResult.Value
        });
    }
}
```

### Email Validation and Usage

```csharp
public class EmailService
{
    public async Task<Result<Unit>> SendWelcomeEmailAsync(string emailInput)
    {
        // Create and validate email
        var emailResult = Email.Create(emailInput);
        if (emailResult.IsFailure)
            return Result<Unit>.Failure(emailResult.Error);
            
        var email = emailResult.Value;
        
        // Check if it's a business email
        if (email.IsFreeEmailProvider())
        {
            _logger.LogWarning("Free email provider used: {Domain}", email.Domain);
        }
        
        // Send email
        try
        {
            await _emailSender.SendAsync(
                email.Value,
                "Welcome!",
                GetWelcomeTemplate(email.LocalPart));
                
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(
                Error.External("Failed to send email", exception: ex));
        }
    }
}
```

## Using Specifications

### Creating Custom Specifications

```csharp
// Domain specifications
public class PremiumCustomerSpec : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression()
    {
        return customer => 
            customer.Status == CustomerStatus.Active &&
            customer.TotalPurchases > 10000 &&
            customer.MemberSince < DateTime.UtcNow.AddYears(-1);
    }
}

public class RecentOrdersSpec : Specification<Order>
{
    private readonly int _days;
    
    public RecentOrdersSpec(int days)
    {
        _days = days;
    }
    
    public override Expression<Func<Order, bool>> ToExpression()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_days);
        return order => order.CreatedAt >= cutoffDate;
    }
}

// Composite specifications
public class EligibleForDiscountSpec : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression()
    {
        var premiumSpec = new PremiumCustomerSpec();
        var activeSpec = new ActiveCustomerSpec();
        var notSuspendedSpec = new NotSuspendedSpec();
        
        return premiumSpec
            .And(activeSpec)
            .And(notSuspendedSpec)
            .ToExpression();
    }
}
```

### Using Specifications in Repositories

```csharp
public interface ICustomerRepository
{
    Task<List<Customer>> FindAsync(
        Specification<Customer> spec,
        CancellationToken cancellationToken = default);
        
    Task<PagedResult<Customer>> FindPagedAsync(
        Specification<Customer> spec,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _context;
    
    public async Task<List<Customer>> FindAsync(
        Specification<Customer> spec,
        CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Where(spec.ToExpression())
            .ToListAsync(cancellationToken);
    }
    
    public async Task<PagedResult<Customer>> FindPagedAsync(
        Specification<Customer> spec,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.Where(spec.ToExpression());
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
            
        var meta = new PaginationMeta(totalCount, page, pageSize);
        
        return new PagedResult<Customer>(items, meta);
    }
}
```

## Event-Driven Architecture

### Domain Events

```csharp
// Define domain events
public sealed record OrderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
    
    public OrderId OrderId { get; }
    public CustomerId CustomerId { get; }
    public Money TotalAmount { get; }
    
    public OrderCreatedEvent(
        OrderId orderId,
        CustomerId customerId,
        Money totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}

// Domain event handlers
public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    
    public async Task Handle(
        OrderCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling OrderCreatedEvent for Order {OrderId}",
            notification.OrderId);
            
        // Send confirmation email
        var emailTask = _emailService.SendOrderConfirmationAsync(
            notification.CustomerId,
            notification.OrderId);
            
        // Reserve inventory
        var inventoryTask = _inventoryService.ReserveForOrderAsync(
            notification.OrderId);
            
        // Execute in parallel
        await Task.WhenAll(emailTask, inventoryTask);
    }
}
```

### Integration Events

```csharp
// Integration event for cross-module communication
public sealed record OrderShippedIntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string TrackingNumber { get; init; }
    public DateTime EstimatedDelivery { get; init; }
}

// Publisher
public class OrderShippingService
{
    private readonly IEventBus _eventBus;
    
    public async Task<Result<Unit>> ShipOrderAsync(Order order)
    {
        // Business logic for shipping
        var trackingNumber = await GenerateTrackingNumberAsync();
        var estimatedDelivery = CalculateDeliveryDate();
        
        // Update order
        var result = order.MarkAsShipped(trackingNumber, estimatedDelivery);
        if (result.IsFailure)
            return result;
            
        // Publish integration event
        var integrationEvent = new OrderShippedIntegrationEvent
        {
            OrderId = order.Id.Value,
            CustomerId = order.CustomerId.Value,
            TrackingNumber = trackingNumber,
            EstimatedDelivery = estimatedDelivery
        };
        
        await _eventBus.PublishAsync(integrationEvent);
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

## Advanced Patterns

### Saga Pattern

```csharp
public class OrderProcessingSaga
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderProcessingSaga> _logger;
    
    public async Task<Result<OrderProcessingResult>> ProcessAsync(
        OrderId orderId)
    {
        var sagaId = Guid.NewGuid();
        _logger.LogInformation("Starting saga {SagaId} for order {OrderId}", 
            sagaId, orderId);
            
        // Step 1: Validate order
        var validateResult = await _mediator.Send(
            new ValidateOrderCommand(orderId));
        if (validateResult.IsFailure)
            return await CompensateAsync(sagaId, validateResult.Error);
            
        // Step 2: Reserve inventory
        var reserveResult = await _mediator.Send(
            new ReserveInventoryCommand(orderId));
        if (reserveResult.IsFailure)
        {
            await _mediator.Send(new CancelOrderCommand(orderId));
            return await CompensateAsync(sagaId, reserveResult.Error);
        }
        
        // Step 3: Process payment
        var paymentResult = await _mediator.Send(
            new ProcessPaymentCommand(orderId));
        if (paymentResult.IsFailure)
        {
            await _mediator.Send(new ReleaseInventoryCommand(orderId));
            await _mediator.Send(new CancelOrderCommand(orderId));
            return await CompensateAsync(sagaId, paymentResult.Error);
        }
        
        // Step 4: Schedule shipping
        var shippingResult = await _mediator.Send(
            new ScheduleShippingCommand(orderId));
        if (shippingResult.IsFailure)
        {
            await _mediator.Send(new RefundPaymentCommand(paymentResult.Value));
            await _mediator.Send(new ReleaseInventoryCommand(orderId));
            await _mediator.Send(new CancelOrderCommand(orderId));
            return await CompensateAsync(sagaId, shippingResult.Error);
        }
        
        // Success
        return Result<OrderProcessingResult>.Success(
            new OrderProcessingResult(
                sagaId,
                orderId,
                paymentResult.Value,
                shippingResult.Value));
    }
    
    private async Task<Result<OrderProcessingResult>> CompensateAsync(
        Guid sagaId,
        Error error)
    {
        _logger.LogError("Saga {SagaId} failed: {Error}", sagaId, error);
        // Additional compensation logic
        return Result<OrderProcessingResult>.Failure(error);
    }
}
```

### Outbox Pattern

```csharp
public class OutboxProcessor
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxProcessor> _logger;
    
    public async Task ProcessPendingMessagesAsync(
        CancellationToken cancellationToken)
    {
        var messages = await _outboxRepository.GetPendingMessagesAsync(
            limit: 100,
            cancellationToken);
            
        foreach (var message in messages)
        {
            try
            {
                // Deserialize event
                var @event = JsonSerializer.Deserialize<IIntegrationEvent>(
                    message.Payload,
                    new JsonSerializerOptions
                    {
                        TypeInfoResolver = new IntegrationEventTypeResolver()
                    });
                    
                // Publish to message bus
                await _eventBus.PublishAsync(@event!);
                
                // Mark as processed
                await _outboxRepository.MarkAsProcessedAsync(
                    message.Id,
                    cancellationToken);
                    
                _logger.LogInformation(
                    "Successfully processed outbox message {MessageId}",
                    message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process outbox message {MessageId}",
                    message.Id);
                    
                // Update retry count
                await _outboxRepository.IncrementRetryCountAsync(
                    message.Id,
                    cancellationToken);
            }
        }
    }
}
```

## Real-World Examples

### E-Commerce Order Processing

```csharp
public class OrderProcessingService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerService _customerService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result<OrderConfirmation>> ProcessOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validate customer
        var customerResult = await ValidateCustomerAsync(
            request.CustomerId,
            cancellationToken);
        if (customerResult.IsFailure)
            return Result<OrderConfirmation>.Failure(customerResult.Error);
            
        // 2. Create order aggregate
        var orderResult = await CreateOrderAsync(
            customerResult.Value,
            request,
            cancellationToken);
        if (orderResult.IsFailure)
            return Result<OrderConfirmation>.Failure(orderResult.Error);
            
        var order = orderResult.Value;
        
        // 3. Check inventory
        var inventoryResult = await CheckInventoryAsync(
            order.Items,
            cancellationToken);
        if (inventoryResult.IsFailure)
            return Result<OrderConfirmation>.Failure(inventoryResult.Error);
            
        // 4. Calculate pricing with discounts
        var pricingResult = await CalculatePricingAsync(
            order,
            customerResult.Value,
            cancellationToken);
        if (pricingResult.IsFailure)
            return Result<OrderConfirmation>.Failure(pricingResult.Error);
            
        // 5. Process payment
        var paymentResult = await ProcessPaymentAsync(
            order,
            pricingResult.Value,
            cancellationToken);
        if (paymentResult.IsFailure)
        {
            // Compensate
            await ReleaseInventoryAsync(order.Items, cancellationToken);
            return Result<OrderConfirmation>.Failure(paymentResult.Error);
        }
        
        // 6. Confirm order
        var confirmResult = order.Confirm(paymentResult.Value.TransactionId);
        if (confirmResult.IsFailure)
        {
            // Compensate
            await RefundPaymentAsync(paymentResult.Value, cancellationToken);
            await ReleaseInventoryAsync(order.Items, cancellationToken);
            return Result<OrderConfirmation>.Failure(confirmResult.Error);
        }
        
        // 7. Save order with events
        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // 8. Schedule shipping (async)
        _ = Task.Run(async () =>
        {
            await _shippingService.ScheduleAsync(order.Id, cancellationToken);
        }, cancellationToken);
        
        // 9. Send notifications (async)
        _ = Task.Run(async () =>
        {
            await _notificationService.SendOrderConfirmationAsync(
                order,
                cancellationToken);
        }, cancellationToken);
        
        // Return confirmation
        return Result<OrderConfirmation>.Success(new OrderConfirmation
        {
            OrderId = order.Id.Value,
            OrderNumber = order.OrderNumber,
            TotalAmount = order.TotalAmount,
            EstimatedDelivery = order.EstimatedDelivery,
            PaymentConfirmation = paymentResult.Value
        });
    }
    
    private async Task<Result<Customer>> ValidateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customerIdResult = CustomerId.FromString(customerId.ToString());
        if (customerIdResult.IsFailure)
            return Result<Customer>.Failure(customerIdResult.Error);
            
        var customer = await _customerService.GetByIdAsync(
            customerIdResult.Value,
            cancellationToken);
            
        if (customer == null)
            return Result<Customer>.Failure(
                Error.NotFound($"Customer {customerId} not found"));
                
        if (customer.Status != CustomerStatus.Active)
            return Result<Customer>.Failure(
                Error.BusinessRule("Customer account is not active"));
                
        if (customer.HasOutstandingBalance())
            return Result<Customer>.Failure(
                Error.BusinessRule("Customer has outstanding balance"));
                
        return Result<Customer>.Success(customer);
    }
    
    // Additional helper methods...
}
```

### User Registration with Email Verification

```csharp
public class UserRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEventBus _eventBus;
    
    public async Task<Result<UserRegistrationResult>> RegisterUserAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate email
        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
            return Result<UserRegistrationResult>.Failure(emailResult.Error);
            
        // 2. Check if email is already registered
        var existingUser = await _userRepository.FindByEmailAsync(
            emailResult.Value,
            cancellationToken);
            
        if (existingUser != null)
            return Result<UserRegistrationResult>.Failure(
                Error.Conflict("Email is already registered"));
                
        // 3. Validate password strength
        var passwordResult = Password.Create(command.Password);
        if (passwordResult.IsFailure)
            return Result<UserRegistrationResult>.Failure(passwordResult.Error);
            
        // 4. Create user aggregate
        var userResult = User.Create(
            emailResult.Value,
            passwordResult.Value,
            command.FirstName,
            command.LastName);
            
        if (userResult.IsFailure)
            return Result<UserRegistrationResult>.Failure(userResult.Error);
            
        var user = userResult.Value;
        
        // 5. Hash password
        var hashedPassword = await _passwordHasher.HashAsync(
            passwordResult.Value.Value);
        user.SetPasswordHash(hashedPassword);
        
        // 6. Generate verification token
        var verificationToken = VerificationToken.Generate();
        user.SetVerificationToken(verificationToken);
        
        // 7. Save user
        await _userRepository.AddAsync(user, cancellationToken);
        
        // 8. Send verification email
        var emailSentResult = await _emailService.SendVerificationEmailAsync(
            user.Email,
            verificationToken,
            cancellationToken);
            
        if (emailSentResult.IsFailure)
        {
            // Log but don't fail registration
            _logger.LogWarning(
                "Failed to send verification email to {Email}",
                user.Email.Value);
        }
        
        // 9. Publish user registered event
        await _eventBus.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id.Value,
            Email = user.Email.Value,
            RegisteredAt = user.CreatedAt
        });
        
        return Result<UserRegistrationResult>.Success(
            new UserRegistrationResult
            {
                UserId = user.Id.Value,
                Email = user.Email.Value,
                RequiresEmailVerification = true
            });
    }
}
```

## Conclusion

This guide demonstrates comprehensive usage of the BuildingBlocks.Core library. The patterns and examples shown here promote:

- **Type Safety**: Strong typing prevents runtime errors
- **Explicit Error Handling**: Result pattern makes failures visible
- **Business Logic Encapsulation**: Rich domain models with clear boundaries
- **Testability**: Pure functions and immutable data
- **Maintainability**: Clear separation of concerns
- **Performance**: Optimized error handling and caching

For more examples and advanced scenarios, refer to the module implementations in the Axon Backend codebase.