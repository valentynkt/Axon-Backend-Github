# 🔥 Architecture Quick Reference

**Most frequently accessed patterns and conventions. 80% of queries start here.**

---

## Result<T, Error> Pattern

### Basic Usage
```csharp
// Success case
public Result<User, Error> CreateUser(string email)
{
    var user = new User(email);
    return Result.Success<User, Error>(user);
}

// Failure case
public Result<User, Error> FindUser(UserId id)
{
    if (user not found)
        return Result.Failure<User, Error>(Error.NotFound("User", id.ToString()));
        
    return Result.Success<User, Error>(user);
}
```

### Chaining Results
```csharp
return await ValidateInput(request)
    .Bind(validated => CreateEntity(validated))
    .Bind(entity => repository.SaveAsync(entity))
    .Map(entity => entity.Id);
```

**Full Details**: [Domain Primitives](../shared/domain-primitives.md)

---

## CQRS Pattern

### Command (Modifies State)
```csharp
// Command definition
public sealed record CreateUserCommand(string Email) : ICommand<Result<UserId, Error>>;

// Handler
public sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Result<UserId, Error>>
{
    public async Task<Result<UserId, Error>> Handle(
        CreateUserCommand command, 
        CancellationToken ct)
    {
        // Business logic here
    }
}
```

### Query (Reads Data)
```csharp
// Query definition
public sealed record GetUserByIdQuery(UserId Id) : IQuery<Result<UserDto, Error>>;

// Handler
public sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, Result<UserDto, Error>>
{
    public async Task<Result<UserDto, Error>> Handle(
        GetUserByIdQuery query,
        CancellationToken ct)
    {
        // Data retrieval logic
    }
}
```

**Full Details**: [CQRS Patterns](./cqrs-patterns.md)

---

## StrongId<T> Pattern

### Definition
```csharp
public sealed record UserId(Guid Value) : StrongId<Guid>(Value);
public sealed record ConversationId(Guid Value) : StrongId<Guid>(Value);
```

### Usage
```csharp
// Compile-time safety
void ProcessUser(UserId id) { }

UserId userId = new(Guid.NewGuid());
ConversationId convId = new(Guid.NewGuid());

ProcessUser(userId);   // ✅ Compiles
ProcessUser(convId);   // ❌ Compile error - type safety!
```

**Full Details**: [Domain Primitives](../shared/domain-primitives.md)

---

## FastEndpoints Pattern

### Command Endpoint
```csharp
public sealed class CreateUserEndpoint
    : Endpoint<CreateUserRequest, CreateUserResponse>
{
    private readonly IMediator _mediator;

    public override void Configure()
    {
        Post("/api/v1/users");
        AllowAnonymous();
    }

    public override async Task HandleAsync(
        CreateUserRequest req,
        CancellationToken ct)
    {
        var command = new CreateUserCommand(req.Email);
        var result = await _mediator.Send(command, ct);
        
        await result.Match(
            onSuccess: userId => SendOkAsync(new CreateUserResponse(userId), ct),
            onFailure: error => SendResultAsync(Results.Problem(error.ToProblemDetails()))
        );
    }
}
```

**Full Details**: [Libraries/FastEndpoints](../libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md)

---

## Error Types Quick Reference

```csharp
// Validation error
Error.Validation("Email is required")

// Not found
Error.NotFound("User", userId.ToString())

// Business rule violation
Error.BusinessRule("Cannot delete active user")

// Conflict (concurrency/duplicates)
Error.Conflict("Email already exists")

// Internal system error
Error.Internal("Database connection failed")

// External service error
Error.External("Dynamic.xyz API unavailable")
```

**Full Details**: [Error Handling Strategy](../shared/error-handling-strategy.md)

---

## FluentValidation Pattern

```csharp
public sealed class CreateUserCommandValidator
    : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
    }
}
```

**Full Details**: [Validation Framework](../shared/validation-framework.md)

---

## Domain Event Pattern

### Event Definition
```csharp
public sealed record UserCreated(UserId UserId, string Email) : IDomainEvent;
```

### Publishing
```csharp
public sealed class User : AggregateRoot<UserId>
{
    public Result<User, Error> Create(string email)
    {
        // ... validation ...
        
        var user = new User(email);
        user.RaiseDomainEvent(new UserCreated(user.Id, email));
        
        return Result.Success<User, Error>(user);
    }
}
```

### Handling
```csharp
public sealed class UserCreatedHandler : IDomainEventHandler<UserCreated>
{
    public async Task Handle(UserCreated @event, CancellationToken ct)
    {
        // Side effects (send welcome email, create cache entry, etc.)
    }
}
```

**Full Details**: [DDD Tactical Patterns](./ddd-tactical-patterns.md)

---

## Module Boundaries

```
✅ ALLOWED:
- Within module: Commands → Queries → Domain
- Cross-module: Async via Domain Events

❌ FORBIDDEN:
- Direct references between modules
- Cross-module command/query calls
- Shared database tables between modules
```

**Full Details**: [Modular Monolith](./modular-monolith.md)

---

## File Organization Conventions

```csharp
// ✅ Correct: File-scoped namespace
namespace Axon.Modules.Identity.Domain;

public sealed class User : AggregateRoot<UserId>
{
    // ...
}

// ❌ Incorrect: Block-scoped namespace
namespace Axon.Modules.Identity.Domain
{
    public sealed class User { }
}

// ✅ Correct: Target-typed new
List<string> items = new();

// ❌ Incorrect: Redundant type
List<string> items = new List<string>();

// ✅ Correct: Records for DTOs
public sealed record UserDto(UserId Id, string Email);

// ❌ Incorrect: Classes for DTOs
public sealed class UserDto
{
    public UserId Id { get; set; }
    public string Email { get; set; }
}
```

**Full Details**: [Coding Standards](./coding-standards.md)

---

## Common Mistakes to Avoid

### ❌ Throwing Exceptions for Business Logic
```csharp
// BAD
public User CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        throw new ArgumentException("Email required");
}

// GOOD
public Result<User, Error> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result.Failure<User, Error>(Error.Validation("Email required"));
}
```

### ❌ Primitive Obsession
```csharp
// BAD
void ProcessUser(Guid userId) { }
void ProcessConversation(Guid conversationId) { }
ProcessUser(conversationId); // Oops! No compile error

// GOOD
void ProcessUser(UserId userId) { }
void ProcessConversation(ConversationId conversationId) { }
ProcessUser(conversationId); // ✅ Compile error!
```

### ❌ Anemic Domain Models
```csharp
// BAD
public class User
{
    public string Email { get; set; }
    public List<WalletAddress> Wallets { get; set; }
}
// Business logic in services instead of domain

// GOOD
public sealed class User : AggregateRoot<UserId>
{
    private readonly List<WalletOwnership> _wallets = new();
    
    public Result<WalletOwnership, Error> LinkWallet(WalletAddress address)
    {
        // Invariant enforcement in domain
        if (_wallets.Any(w => w.Address == address))
            return Result.Failure<WalletOwnership, Error>(
                Error.BusinessRule("Wallet already linked"));
                
        var ownership = new WalletOwnership(Id, address);
        _wallets.Add(ownership);
        return Result.Success<WalletOwnership, Error>(ownership);
    }
}
```

---

## Quick Command Reference

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run API
dotnet run --project src/Api

# Add migration
dotnet ef migrations add <MigrationName> --project src/Modules/Identity/Infrastructure

# Update database
dotnet ef database update --project src/Modules/Identity/Infrastructure
```

---

**For More Details**:
- Complete architecture → [System Overview](./system-overview.md)
- Module documentation → [Identity](../modules/identity/00-MODULE-README.md) | [Chat](../modules/chat/00-MODULE-README.md)
- Shared patterns → [BuildingBlocks](../shared/README.md)