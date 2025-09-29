# Development Essentials - Ultra-Lean Context

## Architecture Patterns ⚡
- **Clean Architecture**: Domain → Application → Infrastructure → API
- **CQRS**: Commands modify, Queries read (MediatR)  
- **Result Pattern**: `Result<T>` replaces exceptions
- **Strong IDs**: `UserId`, `ChatId` etc.

## Tech Stack ⚡
- **.NET 10** + FastEndpoints + MediatR + FluentValidation
- **EF Core 9** + PostgreSQL + Strong ID Generators
- **Testing**: xUnit + Shouldly + Testcontainers

## Code Standards ⚡
```csharp
// File-scoped namespaces
namespace Axon.Modules.Chat;

// Result pattern
public Result<User> CreateUser(string email) =>
    string.IsNullOrEmpty(email)
        ? Result<User>.Failure(Error.Validation("Email required"))
        : Result<User>.Success(new User(email));

// Strong IDs  
public record UserId(Guid Value) : StrongId<Guid>(Value);
```

## Project Structure ⚡
```
src/
├── Api/Endpoints/V1/           # FastEndpoints by feature
├── BuildingBlocks/             # Shared infrastructure
└── Modules/{Module}/           # Business modules
    ├── Domain/                 # Aggregates, entities
    ├── Application/            # Commands, queries  
    └── Infrastructure/         # Repositories
```

## Development Flow ⚡
1. **Research Gate** (MANDATORY): @axon-research-architect
2. **Implementation**: @axon-implementation-specialist  
3. **Quality**: @axon-quality-guardian
4. **Orchestration**: @axon-story-orchestrator