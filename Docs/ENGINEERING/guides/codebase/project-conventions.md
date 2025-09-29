# Project Conventions

**File naming, namespaces, project structure, and organization standards.**

---

**STATUS**: ✅ Complete
**PRIORITY**: High
**LAST_UPDATED**: 2025-09-29

---

## Overview

This document defines project-level conventions for file organization, naming, namespaces, and project structure in Axon Backend.

---

## Namespace Conventions

### Modern C# File-Scoped Namespaces (Mandatory)

```csharp
// ✅ REQUIRED: File-scoped namespace
namespace Axon.Modules.Identity.Domain;

public sealed class User : AggregateRoot<UserId>
{
    // ...
}

// ❌ FORBIDDEN: Block-scoped namespace
namespace Axon.Modules.Identity.Domain
{
    public sealed class User { }
}
```

### Namespace Structure

```
Axon
├── Api                                # API host project
│   ├── Endpoints                      # FastEndpoints
│   ├── Contracts                      # Request/response DTOs
│   └── Middleware                     # HTTP middleware
│
├── BuildingBlocks                     # Shared infrastructure
│   ├── Core                           # Domain primitives, CQRS abstractions
│   ├── Application                    # MediatR behaviors
│   ├── Infrastructure                 # Caching, persistence, resilience
│   └── Web                            # HTTP concerns
│
└── Modules                            # Business modules
    └── {ModuleName}                   # Identity, Chat
        ├── Domain                     # Business logic
        ├── Application                # Use cases
        └── Infrastructure             # External concerns
```

**Convention**: `Axon.{Layer}.{Module}.{Feature}.{Type}`

**Examples:**
- `Axon.Modules.Identity.Domain.Aggregates`
- `Axon.Modules.Identity.Application.Commands.CreateUser`
- `Axon.Api.Endpoints.V1.Identity.CreateUser`

---

## File Naming Conventions

### General Rules

1. **Pascal Case**: All files use PascalCase
2. **One Class Per File**: Each class/interface/record gets its own file
3. **Match Class Name**: File name matches primary type name exactly

```csharp
// File: AxonPrincipal.cs
public sealed class AxonPrincipal : AggregateRoot<AxonPrincipalId> { }

// File: IAxonPrincipalRepository.cs
public interface IAxonPrincipalRepository { }

// File: CreateUserCommand.cs
public sealed record CreateUserCommand(string Email) : ICommand<Result<UserId, Error>>;
```

### Partial Classes

For large aggregates split across multiple files:

```
AxonPrincipal/
├── AxonPrincipal.cs           # Main aggregate
├── AxonPrincipal.Commands.cs  # Command methods
├── AxonPrincipal.Queries.cs   # Query methods
└── AxonPrincipal.Events.cs    # Event raising logic
```

**Convention**: `{TypeName}.{Aspect}.cs`

---

## Project Structure Conventions

### Module Project Organization

Each module follows Clean Architecture layers:

```
Modules/{ModuleName}/
├── Domain/                              # *.Domain.csproj
│   ├── Aggregates/                      # Aggregate roots
│   │   └── {AggregateName}/
│   │       ├── {Aggregate}.cs
│   │       ├── {Aggregate}.Commands.cs
│   │       └── {Aggregate}.Queries.cs
│   ├── Entities/                        # Entities (owned or standalone)
│   ├── ValueObjects/                    # Value objects
│   ├── Events/                          # Domain events
│   └── Repositories/                    # Repository interfaces
│
├── Application/                         # *.Application.csproj
│   ├── Commands/                        # Command use cases
│   │   └── {CommandName}/
│   │       ├── {CommandName}Command.cs
│   │       ├── {CommandName}CommandHandler.cs
│   │       └── {CommandName}CommandValidator.cs
│   ├── Queries/                         # Query use cases
│   │   └── {QueryName}/
│   │       ├── {QueryName}Query.cs
│   │       ├── {QueryName}QueryHandler.cs
│   │       └── {QueryName}QueryValidator.cs
│   ├── Services/                        # Application services
│   └── DTOs/                            # Data transfer objects
│
└── Infrastructure/                      # *.Infrastructure.csproj
    ├── Persistence/
    │   ├── DbContexts/                  # EF Core contexts
    │   ├── Configurations/              # Entity configurations
    │   ├── Repositories/                # Repository implementations
    │   └── Migrations/                  # EF Core migrations
    ├── Services/                        # Infrastructure services
    └── ExternalServices/                # Third-party API clients
```

### API Project Organization

```
Api/                                     # Axon.Api.csproj
├── Endpoints/                           # FastEndpoints
│   └── V1/                              # API versioning
│       └── {Module}/                    # Identity, Chat
│           ├── {Feature}/               # CreateUser, GetUser
│           │   ├── {Feature}Endpoint.cs
│           │   ├── {Feature}Request.cs
│           │   ├── {Feature}Response.cs
│           │   └── {Feature}Validator.cs (optional)
│
├── Contracts/                           # Shared DTOs
│   ├── V1/                              # Versioned contracts
│   │   └── {Module}/
│   └── Common/                          # Common response types
│
├── Middleware/                          # HTTP middleware
├── Configuration/                       # Startup configuration
└── Program.cs                           # Application entry point
```

---

## Naming Conventions by Type

### Commands (Write Operations)

```csharp
// Pattern: {Verb}{Noun}Command
public sealed record CreateUserCommand(...) : ICommand<Result<UserId, Error>>;
public sealed record UpdateUserCommand(...) : ICommand<Result<Unit, Error>>;
public sealed record DeleteUserCommand(...) : ICommand<Result<Unit, Error>>;
public sealed record LinkWalletCommand(...) : ICommand<Result<WalletId, Error>>;

// Handler: {CommandName}Handler
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Result<UserId, Error>> { }

// Validator: {CommandName}Validator
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand> { }
```

### Queries (Read Operations)

```csharp
// Pattern: {Verb}{Noun}Query or Get{Noun}By{Criteria}Query
public sealed record GetUserByIdQuery(UserId Id) : IQuery<Result<UserDto, Error>>;
public sealed record GetUsersByEnvironmentQuery(EnvironmentId EnvId) : IQuery<Result<List<UserDto>, Error>>;
public sealed record SearchUsersQuery(string SearchTerm) : IQuery<Result<PagedResult<UserDto>, Error>>;

// Handler: {QueryName}Handler
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, Result<UserDto, Error>> { }
```

### Domain Events

```csharp
// Pattern: {Noun}{PastTenseVerb}
public sealed record UserCreated(UserId UserId, string Email) : IDomainEvent;
public sealed record WalletLinked(UserId UserId, WalletId WalletId) : IDomainEvent;
public sealed record MessageSent(ConversationId ConversationId, MessageId MessageId) : IDomainEvent;

// Handler: {EventName}Handler
public sealed class UserCreatedHandler : IDomainEventHandler<UserCreated> { }
```

### Repositories

```csharp
// Interface: I{EntityName}Repository
public interface IAxonPrincipalRepository { }
public interface IAxonPrincipalWriteRepository : IRepository<AxonPrincipal, AxonPrincipalId> { }
public interface IAxonPrincipalReadRepository { }

// Implementation: {EntityName}Repository
public sealed class AxonPrincipalWriteRepository : IAxonPrincipalWriteRepository { }
```

### Services

```csharp
// Interface: I{ServiceName}Service
public interface IWalletVerificationService { }
public interface IPrincipalResolutionService { }

// Implementation: {ServiceName}Service
public sealed class WalletVerificationService : IWalletVerificationService { }
```

### DTOs (Data Transfer Objects)

```csharp
// Pattern: {Entity}Dto
public sealed record UserDto(UserId Id, string Email);
public sealed record ConversationDto(ConversationId Id, string Title, DateTime CreatedAt);

// Request DTOs: {Feature}Request
public sealed record CreateUserRequest(string Email);
public sealed record UpdateUserRequest(string Email, string Name);

// Response DTOs: {Feature}Response
public sealed record CreateUserResponse(UserId UserId);
public sealed record GetUserResponse(UserDto User);
```

---

## Project Dependency Rules

### Allowed Dependencies

```
Api                    → Modules.*.Application
                       → BuildingBlocks.Web

Modules.Application    → Modules.Domain
                       → BuildingBlocks.Application

Modules.Infrastructure → Modules.Application
                       → Modules.Domain
                       → BuildingBlocks.Infrastructure

Modules.Domain         → BuildingBlocks.Core (only)

BuildingBlocks.*       → (No module dependencies)
```

### Forbidden Dependencies

```
❌ Domain → Application (violates dependency inversion)
❌ Domain → Infrastructure (violates dependency inversion)
❌ Module.A → Module.B (violates module independence)
❌ BuildingBlocks → Modules.* (shared code can't depend on modules)
```

---

## Project References Convention

```xml
<!-- Identity.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- ✅ Allowed: Reference own domain -->
    <ProjectReference Include="../Domain/Axon.Modules.Identity.Domain.csproj" />

    <!-- ✅ Allowed: Reference BuildingBlocks -->
    <ProjectReference Include="../../../BuildingBlocks/Application/BuildingBlocks.Application.csproj" />

    <!-- ❌ Forbidden: Reference other modules -->
    <!-- <ProjectReference Include="../../Chat/Domain/Axon.Modules.Chat.Domain.csproj" /> -->

    <!-- ❌ Forbidden: Reference Infrastructure -->
    <!-- <ProjectReference Include="../Infrastructure/Axon.Modules.Identity.Infrastructure.csproj" /> -->
  </ItemGroup>
</Project>
```

---

## Code File Template

```csharp
namespace Axon.Modules.Identity.Application.Commands.CreateUser;

/// <summary>
/// Command to create a new user from Dynamic.xyz JWT.
/// </summary>
/// <param name="Token">Dynamic.xyz JWT token.</param>
public sealed record CreateUserCommand(string Token) : ICommand<Result<UserId, Error>>;

/// <summary>
/// Handler for <see cref="CreateUserCommand"/>.
/// </summary>
public sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Result<UserId, Error>>
{
    private readonly IAxonPrincipalWriteRepository _repository;
    private readonly IDynamicAuthService _authService;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IAxonPrincipalWriteRepository repository,
        IDynamicAuthService authService,
        ILogger<CreateUserCommandHandler> logger)
    {
        _repository = repository;
        _authService = authService;
        _logger = logger;
    }

    public async Task<Result<UserId, Error>> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}

/// <summary>
/// Validator for <see cref="CreateUserCommand"/>.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required");
    }
}
```

---

## Test Project Conventions

```
tests/Modules/{ModuleName}/
├── Domain/                              # *.Domain.Tests.csproj
│   └── {AggregateName}Tests.cs
│
├── Application/                         # *.Application.Tests.csproj
│   ├── Commands/
│   │   └── {CommandName}/
│   │       └── {CommandName}HandlerTests.cs
│   └── Queries/
│
├── Infrastructure/                      # *.Infrastructure.Tests.csproj
│   ├── Repositories/
│   └── Services/
│
├── Integration/                         # *.Integration.Tests.csproj
└── E2E/                                 # *.E2E.Tests.csproj
```

**Test Naming**: `{ClassUnderTest}Tests.cs`
**Test Method Naming**: `{MethodName}_{Scenario}_{ExpectedResult}`

---

## Configuration Files

### Solution Level

```
/
├── global.json                         # .NET SDK version
├── Directory.Build.props               # Shared build properties
├── Directory.Packages.props            # Central package management (future)
├── .editorconfig                       # Code style rules
└── Axon-Backend.sln                    # Solution file
```

### Project Level

```
src/Api/
├── appsettings.json                    # Base configuration
├── appsettings.Development.json        # Local dev overrides
├── appsettings.Production.json         # Production config
└── Properties/
    └── launchSettings.json             # Debug profiles
```

---

## Related Documentation

- **Coding Standards** → [coding-standards.md](./coding-standards.md)
- **Source Tree** → [source-tree.md](./source-tree.md)
- **CQRS Patterns** → [../patterns/cqrs.md](../patterns/cqrs.md)
- **Domain Modeling** → [../patterns/domain-modeling.md](../patterns/domain-modeling.md)

---

**Last Updated**: 2025-09-29