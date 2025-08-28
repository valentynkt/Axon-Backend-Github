# Coding Standards - Axon Backend

## Core Principles

- **Clean Architecture**: Domain → Application → Infrastructure → API layers
- **Research-First Development**: NEVER implement without ADR validation
- **Result Pattern**: Use `Result<T>` for error handling, avoid exceptions
- **Strong Typing**: All entity IDs use `StrongId<T>` pattern

## Modern C# Requirements

### File Organization
- **File-scoped namespaces**: `namespace Axon.Modules.Chat;`
- **Records for DTOs**: `public record UserRequest(string Name, string Email);`
- **Target-typed new**: `List<string> items = new();`
- **Primary constructors** where appropriate

### Error Handling
```csharp
// ✅ Correct: Return Result<T>
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result<User>.Failure(Error.Validation("Email required"));
        
    return Result<User>.Success(new User(email));
}

// ❌ Incorrect: Throw exceptions for business logic
public User CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        throw new ArgumentException("Email required");
}
```

### CQRS Implementation
- Commands: Modify state, return `Result<T>`
- Queries: Read data, return `Result<TResponse>`
- All handlers implement `ICommandHandler<T>` or `IQueryHandler<T>`

## Architecture Patterns

### Module Structure
```
Modules/ModuleName/
├── Domain/           # Aggregates, entities, value objects
├── Application/      # Commands, queries, handlers
└── Infrastructure/   # Repositories, external adapters
```

### Strong ID Pattern
```csharp
public record UserId(Guid Value) : StrongId<Guid>(Value);
public record User(UserId Id, string Email);
```

## Quality Standards

- Nullable reference types enabled
- No warnings in Release build
- 100% test coverage for business logic
- FluentValidation for all input validation