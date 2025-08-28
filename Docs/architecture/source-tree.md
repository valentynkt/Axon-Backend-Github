# Source Tree Structure - Axon Backend

## Project Organization

### Root Structure
```
/
├── src/                    # Application source code
├── tests/                  # Test projects
├── docs/                   # Documentation
├── .bmad-core/            # BMAD methodology configuration
├── .claude/               # Claude AI agent configuration
└── global.json           # .NET SDK version pinning
```

### Source Code Structure (`src/`)
```
src/
├── Api/                          # HTTP host & composition root
│   ├── Endpoints/               # FastEndpoints by feature
│   │   └── V1/                  # API versioning
│   │       └── Chat/            # Feature-based organization
│   │           ├── Commands/    # Command endpoints
│   │           └── Queries/     # Query endpoints
│   ├── Contracts/               # Request/response DTOs
│   │   ├── V1/                  # Versioned contracts
│   │   └── Common/              # Shared contracts
│   └── Program.cs               # Application entry point
├── BuildingBlocks/              # Shared technical infrastructure
│   ├── Core/                    # Domain primitives, CQRS abstractions
│   │   ├── Domain/              # Base domain types
│   │   ├── Functional/          # Result<T>, Option<T> monads
│   │   └── Cqrs/                # Command/query interfaces
│   ├── Application/             # MediatR behaviors, validation
│   ├── Infrastructure/          # Persistence, caching, resilience
│   ├── Validation/              # FluentValidation integration
│   └── Web/                     # HTTP concerns, problem details
└── Modules/                     # Business modules (bounded contexts)
    └── Chat/                    # Example: Chat module
        ├── Domain/              # Business logic & rules
        │   ├── Aggregates/      # Domain aggregates
        │   ├── Entities/        # Domain entities
        │   ├── ValueObjects/    # Value objects
        │   └── Events/          # Domain events
        ├── Application/         # Use cases & orchestration
        │   ├── Commands/        # Command handlers
        │   ├── Queries/         # Query handlers
        │   └── Validators/      # Input validation
        └── Infrastructure/      # External concerns
            ├── Repositories/    # Data access
            └── Services/        # External service adapters
```

## Navigation Patterns

### By Feature (Vertical Slice)
For implementing a new chat feature:
```
1. Start: Api/Endpoints/V1/Chat/Commands/
2. Contracts: Api/Contracts/V1/Chat/
3. Use Cases: Modules/Chat/Application/Commands/
4. Domain Logic: Modules/Chat/Domain/
5. Data Access: Modules/Chat/Infrastructure/
```

### By Layer (Horizontal)
For understanding architectural concerns:
```
- Domain Layer: src/Modules/*/Domain/
- Application Layer: src/Modules/*/Application/
- Infrastructure Layer: src/Modules/*/Infrastructure/
- API Layer: src/Api/
```

## Key File Locations

### Configuration
- `src/Api/Program.cs` - DI container & middleware setup
- `Directory.Build.props` - Global build configuration
- `.bmad-core/core-config.yaml` - AI workflow configuration

### Shared Abstractions
- `src/BuildingBlocks/Core/Domain/` - Base domain types
- `src/BuildingBlocks/Core/Functional/` - Result/Option patterns
- `src/BuildingBlocks/Core/Cqrs/` - CQRS interfaces

### Business Modules
- Each module is self-contained
- Clean Architecture within each module
- Cross-module communication via events only