# BuildingBlocks.Core Documentation

## Table of Contents

1. [Overview](#overview)
2. [Architecture Philosophy](#architecture-philosophy)
3. [Core Components](#core-components)
   - [Domain Primitives](#domain-primitives)
   - [Functional Programming](#functional-programming)
   - [CQRS Abstractions](#cqrs-abstractions)
   - [Error Handling](#error-handling)
   - [Events and Messaging](#events-and-messaging)
4. [Design Patterns](#design-patterns)
5. [Best Practices](#best-practices)

## Overview

**BuildingBlocks.Core** is the foundational library for the Axon Backend system, providing essential building blocks for implementing a **Modular Monolith** using **Clean Architecture**, **Domain-Driven Design (DDD)**, and **Command Query Responsibility Segregation (CQRS)** patterns. Built on .NET 10 preview, it leverages modern C# features to deliver a robust, type-safe, and functional programming-oriented foundation.

### Key Features

- **Railway-Oriented Programming**: Result and Option monads for explicit error handling
- **Strong Type System**: Type-safe IDs, value objects, and domain primitives
- **CQRS Pattern**: Clear separation of commands and queries with MediatR integration
- **Rich Domain Models**: Aggregates, entities, and value objects following DDD tactical patterns
- **Functional Programming**: Immutable data structures, pure functions, and monadic operations
- **Performance Optimized**: Memory-efficient error handling, caching, and pooling mechanisms
- **Comprehensive Error Management**: Rich error types with metadata, severity, and observability support

## Architecture Philosophy

### Core Principles

1. **Explicit Over Implicit**: All operations return `Result<T>` or `Option<T>` to make success/failure explicit
2. **Type Safety**: Strong typing prevents primitive obsession and runtime errors
3. **Immutability**: Records and immutable structures prevent unintended mutations
4. **Composition**: Small, composable functions that can be combined into complex operations
5. **Separation of Concerns**: Clear boundaries between domain logic, application logic, and infrastructure

### Layered Architecture

```
┌─────────────────────────────────────────┐
│           Domain Layer                   │
│  (Entities, Value Objects, Aggregates)  │
├─────────────────────────────────────────┤
│         Application Layer                │
│    (Commands, Queries, Handlers)        │
├─────────────────────────────────────────┤
│        Infrastructure Layer              │
│   (Persistence, External Services)      │
├─────────────────────────────────────────┤
│           Presentation Layer             │
│      (API Endpoints, Contracts)         │
└─────────────────────────────────────────┘
```

## Core Components

### Domain Primitives

#### StrongId Pattern

Strongly-typed identifiers prevent ID confusion and provide type safety at compile time.

**Location**: `src/BuildingBlocks/Core/Domain/Primitives/StrongId.cs`

**Key Classes**:
- `StrongId<TPrimitive>`: Base for all strongly-typed IDs
- `GuidStrongId`: GUID-based IDs with factory methods
- `IntStrongId`: Integer-based IDs with validation

**Features**:
- Type-safe ID comparison
- Automatic validation (non-default values)
- JSON serialization support
- Implicit conversion to primitive type
- Factory methods with Result pattern

#### Entity Base Class

**Location**: `src/BuildingBlocks/Core/Domain/Primitives/Entity.cs`

**Key Features**:
- Identity-based equality
- Audit trail (CreatedAt, UpdatedAt)
- Soft delete support
- Optimistic concurrency control
- Transient entity detection

**Properties**:
- `Id`: Strongly-typed identifier
- `CreatedAt`: Creation timestamp
- `UpdatedAt`: Last modification timestamp
- `IsDeleted`: Soft delete flag
- `DeletedAt`: Deletion timestamp
- `ConcurrencyToken`: For optimistic locking

#### Value Objects

**Location**: `src/BuildingBlocks/Core/Domain/Primitives/ValueObject.cs`

**Base Classes**:
- `ValueObject`: Base for complex value objects
- `SingleValueObject<T>`: Base for single-value wrappers

**Features**:
- Structural equality
- Validation support
- Immutability enforcement
- Hash code generation

#### Aggregate Root

**Location**: `src/BuildingBlocks/Core/Domain/Model/AggregateRoot.cs`

**Key Features**:
- Domain event support
- Business rule validation
- Invariant enforcement
- State change management
- Lifecycle hooks

**Methods**:
- `RaiseDomainEvent()`: Raise events for dispatch
- `CheckRule()`: Validate business rules
- `Validate()`: Check aggregate invariants
- `ApplyChange()`: Safe state mutations

### Functional Programming

#### Result Monad

**Location**: `src/BuildingBlocks/Core/Functional/Results/Result.cs`

**Purpose**: Explicit error handling without exceptions

**Key Types**:
- `Result<T>`: Success or failure with value
- `Result`: Non-generic for void operations

**Operations**:
- `Map()`: Transform success value
- `Bind()`: Chain operations (flatMap)
- `Match()`: Pattern matching
- `Recover()`: Error recovery
- `Tap()`: Side effects

**Features**:
- Thread-safe and immutable
- LINQ integration (Select, Where)
- Async operations support
- Error aggregation
- Conversion to Option

#### Option Monad

**Location**: `src/BuildingBlocks/Core/Functional/Options/Option.cs`

**Purpose**: Explicit null handling

**Key Methods**:
- `Some()`: Create with value
- `None()`: Create empty
- `Map()`: Transform if present
- `Filter()`: Conditional presence
- `GetOrElse()`: Default values

**Features**:
- Null safety guarantee
- LINQ integration
- Pattern matching
- Result conversion

#### Validation Applicative

**Location**: `src/BuildingBlocks/Core/Functional/Validation/Validation.cs`

**Purpose**: Accumulate all validation errors

**Key Differences from Result**:
- Collects ALL errors (no short-circuit)
- Applicative functor operations
- Error combination strategies

#### Unit Type

**Location**: `src/BuildingBlocks/Core/Functional/Unit.cs`

**Purpose**: Represent void in functional contexts

### CQRS Abstractions

#### Commands

**Location**: `src/BuildingBlocks/Core/Abstractions/CQRS/ICommand.cs`

**Interfaces**:
- `ICommand`: Commands without return value
- `ICommand<TResponse>`: Commands with response

**Characteristics**:
- Modify system state
- Return `Result<T>` for error handling
- Single responsibility
- Idempotency support

#### Queries

**Location**: `src/BuildingBlocks/Core/Abstractions/CQRS/IQuery.cs`

**Interface**: `IQuery<TResponse>`

**Features**:
- Read-only operations
- Declarative caching support
- Result wrapping
- Cache key generation

**Properties**:
- `UseCache`: Enable caching
- `CacheDuration`: TTL for cache
- `CacheKeyPrefix`: Cache key namespace

#### Handlers

**Locations**:
- `ICommandHandler.cs`
- `IQueryHandler.cs`

**Pattern**: MediatR integration for request/response handling

### Error Handling

#### Error Type

**Location**: `src/BuildingBlocks/Core/Diagnostics/Errors/Error.cs`

**Features**:
- Rich error information
- Error categorization
- Metadata support
- HTTP status mapping
- Exception conversion

**Error Types**:
- `Validation`: Input validation failures
- `NotFound`: Resource not found
- `Conflict`: State conflicts
- `BusinessRule`: Domain rule violations
- `Unauthorized`: Authentication required
- `Forbidden`: Insufficient permissions
- `Internal`: System errors
- `External`: Third-party failures
- `Timeout`: Operation timeouts
- `RateLimit`: Rate limiting

**Factory Methods**:
```csharp
Error.Validation(message, code)
Error.NotFound(message, code)
Error.BusinessRule(message, code)
Error.FromException(exception)
Error.Aggregate(errors)
```

### Events and Messaging

#### Domain Events

**Location**: `src/BuildingBlocks/Core/Domain/Events/`

**Interface**: `IDomainEvent`

**Properties**:
- `EventId`: Unique identifier
- `OccurredAt`: Timestamp
- `Version`: Schema version

**Purpose**:
- Capture domain state changes
- Enable eventual consistency
- Decouple bounded contexts

#### Integration Events

**Location**: `src/BuildingBlocks/Core/Abstractions/Events/`

**Features**:
- Cross-module communication
- External system integration
- Event replay support
- Schema registry

### Specifications

**Location**: `src/BuildingBlocks/Core/Domain/Specifications/`

**Purpose**: Encapsulate query logic

**Features**:
- Composable specifications (AND, OR, NOT)
- LINQ integration
- Expression tree support
- In-memory and database queries

### Pagination

**Location**: `src/BuildingBlocks/Core/Abstractions/Pagination/`

**Key Types**:
- `PagedResult<T>`: Paginated results
- `PaginationMeta`: Metadata
- `IPageQuery`: Query interface

**Features**:
- Consistent pagination API
- Sorting support
- Metadata generation

## Design Patterns

### 1. Railway-Oriented Programming

All operations return `Result<T>` enabling error handling without exceptions:

```csharp
public Result<Order> CreateOrder(CreateOrderCommand command)
{
    return Customer.Create(command.CustomerId)
        .Bind(customer => Order.Create(customer, command.Items))
        .Map(order => repository.Add(order))
        .Tap(order => eventBus.Publish(new OrderCreatedEvent(order)));
}
```

### 2. Specification Pattern

Encapsulate complex queries as reusable specifications:

```csharp
public class ActiveUsersSpec : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
        => user => !user.IsDeleted && user.IsActive;
}
```

### 3. Value Object Pattern

Encapsulate domain concepts with validation:

```csharp
public record Email : SingleValueObject<string>
{
    public static Result<Email> Create(string value)
    {
        if (!IsValid(value))
            return Result<Email>.Failure(Error.Validation("Invalid email"));
        return Result<Email>.Success(new Email(value));
    }
}
```

### 4. Aggregate Pattern

Maintain consistency boundaries and enforce invariants:

```csharp
public class Order : AggregateRoot<OrderId>
{
    public Result<Unit> AddItem(Product product, int quantity)
    {
        return CheckRule(new OrderNotClosedRule(this))
            .Bind(_ => CheckRule(new QuantityMustBePositiveRule(quantity)))
            .Bind(_ => ApplyChange(() => 
            {
                _items.Add(new OrderItem(product, quantity));
                RaiseDomainEvent(new ItemAddedEvent(Id, product.Id));
            }));
    }
}
```

## Best Practices

### 1. Always Use Result Pattern

```csharp
// ✅ Good
public Result<User> GetUser(UserId id)
{
    var user = repository.Find(id);
    return user is null 
        ? Result<User>.Failure(Error.NotFound($"User {id} not found"))
        : Result<User>.Success(user);
}

// ❌ Bad  
public User GetUser(UserId id)
{
    return repository.Find(id) ?? throw new NotFoundException();
}
```

### 2. Prefer Composition Over Inheritance

```csharp
// ✅ Good - Compose behaviors
public Result<Order> ProcessOrder(OrderId id)
{
    return GetOrder(id)
        .Bind(ValidateOrder)
        .Bind(CalculatePricing)
        .Bind(ApplyDiscounts)
        .Bind(SaveOrder);
}
```

### 3. Make Invalid States Unrepresentable

```csharp
// ✅ Good - Type system prevents invalid states
public record OrderStatus
{
    private OrderStatus() { }
    public sealed record Draft : OrderStatus;
    public sealed record Confirmed(DateTime ConfirmedAt) : OrderStatus;
    public sealed record Shipped(DateTime ShippedAt, string TrackingNumber) : OrderStatus;
}
```

### 4. Use Strong IDs

```csharp
// ✅ Good
public record UserId(Guid Value) : GuidStrongId(Value);
public record OrderId(Guid Value) : GuidStrongId(Value);

// ❌ Bad
public Guid UserId { get; set; }
public Guid OrderId { get; set; }
```

### 5. Validate at Boundaries

```csharp
// ✅ Good - Validate in factory methods
public static Result<Email> Create(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return Result<Email>.Failure(Error.Validation("Email required"));
        
    if (!EmailRegex.IsMatch(value))
        return Result<Email>.Failure(Error.Validation("Invalid email format"));
        
    return Result<Email>.Success(new Email(value));
}
```

### 6. Keep Aggregates Small

```csharp
// ✅ Good - Reference other aggregates by ID
public class Order : AggregateRoot<OrderId>
{
    public CustomerId CustomerId { get; private set; } // Reference, not embed
    public List<OrderItem> Items { get; private set; } // Own entity collection
}
```

### 7. Use Domain Events for Side Effects

```csharp
// ✅ Good - Raise events for side effects
public Result<Unit> CompleteOrder()
{
    return CheckRule(new OrderMustBeConfirmedRule(Status))
        .Bind(_ => ApplyChange(() => 
        {
            Status = OrderStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            RaiseDomainEvent(new OrderCompletedEvent(Id, CustomerId));
        }));
}
```

## Performance Considerations

### 1. Error Caching

The Error class uses string interning and caching for common error codes to reduce memory allocation.

### 2. Specification Compilation

Compile specifications when using repeatedly:

```csharp
private readonly Func<User, bool> _activeUserPredicate = 
    new ActiveUsersSpec().Compile();
```

### 3. Result Pattern Overhead

The Result pattern has minimal overhead due to struct implementation and should be used consistently for error handling.

### 4. Event Batching

Domain events are collected and dispatched in batches after successful persistence to minimize round trips.

## Migration Guide

### From Exceptions to Results

```csharp
// Before
public User GetUser(int id)
{
    var user = db.Users.Find(id);
    if (user == null)
        throw new NotFoundException($"User {id} not found");
    return user;
}

// After
public Result<User> GetUser(UserId id)
{
    return db.Users
        .Where(u => u.Id == id)
        .FirstOrDefault()
        .ToResult(Error.NotFound($"User {id} not found"));
}
```

### From Primitive IDs to Strong IDs

```csharp
// Before
public class User
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
}

// After
public record UserId(Guid Value) : GuidStrongId(Value);
public record CompanyId(Guid Value) : GuidStrongId(Value);

public class User : Entity<UserId>
{
    public CompanyId CompanyId { get; private set; }
}
```

## Testing Guidelines

### 1. Test Business Rules

```csharp
[Fact]
public void Order_Cannot_Add_Item_When_Closed()
{
    // Arrange
    var order = CreateClosedOrder();
    
    // Act
    var result = order.AddItem(product, quantity: 1);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.BusinessRule);
}
```

### 2. Test Value Object Validation

```csharp
[Theory]
[InlineData("invalid-email")]
[InlineData("@example.com")]
[InlineData("user@")]
public void Email_Create_Should_Fail_For_Invalid_Format(string input)
{
    var result = Email.Create(input);
    
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.Validation);
}
```

### 3. Test Specifications

```csharp
[Fact]
public void ActiveUsers_Specification_Should_Filter_Correctly()
{
    var spec = new ActiveUsersSpec();
    var users = GetTestUsers();
    
    var activeUsers = users.Where(spec.Compile()).ToList();
    
    activeUsers.Should().OnlyContain(u => u.IsActive && !u.IsDeleted);
}
```

## Troubleshooting

### Common Issues

1. **Null Reference Exceptions**: Always use Option<T> or Result<T> instead of nullable references
2. **Invalid Cast Exceptions**: Use pattern matching with proper type checking
3. **Concurrency Conflicts**: Implement proper concurrency tokens in entities
4. **Memory Leaks**: Clear domain events after processing

### Debug Tips

1. Enable DEBUG compilation for invariant checking in aggregates
2. Use structured logging with Error.ToLogData()
3. Implement correlation IDs for request tracing
4. Monitor Result failure rates for quality metrics

## References

- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Functional Programming in C#](https://www.manning.com/books/functional-programming-in-c-sharp)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)