# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution (warnings treated as errors in Release)
dotnet build

# Run API locally  
dotnet run --project src/Api

# Run all tests
dotnet test

# Clean and rebuild
dotnet clean && dotnet build

# Release build with analyzers enabled
dotnet build --configuration Release
```

## Architecture Overview

**Axon Backend** is a **Modular Monolith** using Clean Architecture + DDD + CQRS patterns, built on .NET 10 preview.

### Key Architectural Patterns

- **Clean Architecture**: Domain → Application → Infrastructure → API layers
- **CQRS**: Commands modify state, Queries read data (using MediatR)
- **Result Pattern**: `Result<T>` for error handling instead of exceptions
- **Functional Programming**: Immutable records, Result/Option monads
- **Strong IDs**: Type-safe identifiers using `StrongId<T>`
- **Rich Domain Models**: Aggregates, Entities, Value Objects following DDD

### Project Structure

```
src/
├── Api/                    # HTTP host, FastEndpoints, composition root
│   ├── Endpoints/          # Feature-based minimal API endpoints
│   ├── Contracts/          # Request/response DTOs
│   └── Program.cs          # Entry point with DI container
├── BuildingBlocks/         # Shared technical infrastructure
│   ├── Core/              # Domain primitives, CQRS, functional types
│   ├── Application/       # MediatR behaviors, validation
│   ├── Infrastructure/    # Persistence, caching, resilience
│   ├── Validation/        # FluentValidation integration
│   └── Web/              # HTTP concerns, problem details
└── Modules/               # Business modules (bounded contexts)
    └── Identity/          # Example module with Clean Architecture
```

### Core Building Blocks

- **Result Pattern**: `Result<T>`, `Validation<T>` for functional error handling
- **Strong IDs**: Type-safe identifiers - `UserId`, `OrderId`, etc.
- **Domain Events**: `IDomainEvent` for domain event publishing
- **Aggregates**: Rich domain models with business logic encapsulation
- **CQRS Abstractions**: `ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`
- **Pagination**: `PagedResult<T>`, `IPageQuery` for consistent paging

## Technology Stack

- **.NET 10.0** (preview 5.25277.114) - Latest C# features required
- **FastEndpoints** - Minimal API alternative to controllers  
- **MediatR** - CQRS command/query dispatch
- **FluentValidation** - Request validation
- **Entity Framework Core 9.0** - ORM with PostgreSQL
- **System.Text.Json** - API serialization with custom converters
- **OpenTelemetry** - Observability and monitoring
- **xUnit** - Testing framework

### JSON Serialization Configuration

Both generators decorate types with a concrete `[JsonConverter]`, so you do not need to register custom converters globally.

For standard API JSON settings (camelCase, enums as strings, etc.):

```csharp
// During web host setup
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // No need to add converters for your IDs/VOs; attributes handle it per type.
});
```

This keeps your pipeline clean and avoids reflection-based "catch-all" converters.

## Development Guidelines

### Modern C# Requirements (MANDATORY)

- **File-scoped namespaces**: `namespace Axon.Modules.Chat;`
- **Records for DTOs**: `public record UserRequest(string Name, string Email);`
- **Target-typed new**: `List<string> items = new();`
- **Nullable reference types** enabled - handle nulls explicitly
- **Primary constructors** where appropriate

### Error Handling Pattern

```csharp
// Domain layer - return Result<T>
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result<User>.Failure(Error.Validation("Email required"));
        
    return Result<User>.Success(new User(email));
}

// Application layer - handle Results
public async Task<Result<UserResponse>> Handle(CreateUserCommand command)
{
    var userResult = User.Create(command.Email);
    if (userResult.IsFailure)
        return Result<UserResponse>.Failure(userResult.Error);
        
    await repository.Add(userResult.Value);
    return Result<UserResponse>.Success(new UserResponse(userResult.Value.Id));
}
```

### Module Structure (Clean Architecture)

Each module follows Clean Architecture:
```
Modules/ModuleName/
├── Domain/           # Aggregates, entities, value objects, domain events
├── Application/      # Commands, queries, handlers, validators
└── Infrastructure/   # Repositories, external service adapters
```

## Important Notes

- **Warnings as Errors**: Enabled in Release mode - maintain clean code
- **AOT Analysis**: Enabled for libraries to ensure compatibility
- **Deterministic Builds**: Required for reproducible builds
- **XML Documentation**: Generated for all libraries automatically
- **Solution Format**: Uses `.slnx` (newer Visual Studio solution format)
- **Strong ID Pattern**: All entity IDs must be strongly-typed using `StrongId<T>`
- **No Shared Kernel**: Business logic stays within module boundaries

## Testing

- Use **NUnit** for all tests (modern testing standard)
- **Shouldly** for fluent, readable test assertions  
- **Testcontainers** for integration tests with real databases
- **NSubstitute** for mocking dependencies

## Key Files to Understand

- `Directory.Build.props` - Global MSBuild configuration and analyzer rules
- `global.json` - .NET SDK version pinning (preview required)
- `src/BuildingBlocks/Core/Functional/Results/Result.cs` - Result pattern implementation
- `src/BuildingBlocks/Core/Domain/Primitives/StrongId.cs` - Strong ID implementation
- `src/Api/Program.cs` - Application startup and DI configuration