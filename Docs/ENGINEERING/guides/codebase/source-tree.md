# Source Tree Structure - Axon Backend

**Complete navigation guide to the Axon Backend codebase.**

---

**STATUS**: ✅ Complete
**PRIORITY**: High
**LAST_UPDATED**: 2025-09-29

---

## Project Organization

### Root Structure
```
/
├── src/                          # Application source code
├── tests/                        # Test projects (mirrors src/ structure)
├── Docs/                         # Documentation
├── .github/                      # GitHub workflows (CI/CD)
├── global.json                   # .NET SDK version (10.0)
├── Directory.Build.props         # Shared build configuration
├── .editorconfig                 # Code style rules
├── CLAUDE.md                     # AI agent instructions
└── Axon-Backend.sln              # Solution file
```

---

## Source Code Structure (`src/`)

### Complete Directory Tree

```
src/
├── Api/                                      # HTTP host & composition root
│   ├── Endpoints/                            # FastEndpoints (vertical slices)
│   │   └── V1/                               # API version 1
│   │       ├── Identity/                     # Identity module endpoints
│   │       │   ├── ExchangeCredential/
│   │       │   ├── GenerateChallenge/
│   │       │   ├── VerifyWallet/
│   │       │   ├── RefreshToken/
│   │       │   └── GetMyPrincipal/
│   │       └── Chat/                         # Chat module endpoints
│   │           ├── CreateConversation/
│   │           ├── SendMessage/
│   │           └── GetMessages/
│   │
│   ├── Contracts/                            # API Request/Response DTOs
│   │   ├── V1/                               # Versioned contracts
│   │   │   ├── Identity/
│   │   │   └── Chat/
│   │   └── Common/                           # Shared response types
│   │
│   ├── Configuration/                        # DI registration, startup
│   │   └── Mapping/                          # AutoMapper profiles
│   │
│   ├── Middleware/                           # HTTP middleware
│   ├── ErrorHandling/                        # Global exception handling
│   ├── Swagger/                              # OpenAPI configuration
│   ├── Constants/                            # API-level constants
│   ├── Validators/                           # Endpoint validators
│   └── Program.cs                            # Application entry point
│
├── BuildingBlocks/                           # Shared infrastructure
│   ├── Core/                                 # Domain primitives & abstractions
│   │   ├── Abstractions/                     # CQRS, repository interfaces
│   │   │   ├── CQRS/                         # ICommand, IQuery interfaces
│   │   │   ├── Domain/                       # IAggregateRoot, IEntity
│   │   │   ├── Repositories/                 # IRepository<T>
│   │   │   └── Services/                     # Core service interfaces
│   │   │
│   │   ├── Domain/                           # Base domain types
│   │   │   ├── Entities/                     # Base entity classes
│   │   │   │   └── Base/                     # AggregateRoot<TId>, Entity<TId>
│   │   │   ├── Events/                       # DomainEvent base
│   │   │   └── ValueObjects/                 # Base value object
│   │   │
│   │   ├── Primitives/                       # Shared primitives
│   │   │   ├── Ids/                          # Strong typed IDs
│   │   │   │   ├── AxonUserId.cs
│   │   │   │   ├── ConversationId.cs
│   │   │   │   ├── MessageId.cs
│   │   │   │   ├── WalletId.cs
│   │   │   │   └── Defaults/                 # StronglyTypedId configuration
│   │   │   └── ValueObjects/                 # Shared value objects
│   │   │       └── MessageContent.cs
│   │   │
│   │   ├── Diagnostics/                      # Error handling
│   │   │   ├── Errors/                       # Error, ErrorType, ErrorSeverity
│   │   │   ├── Exceptions/                   # Domain exceptions
│   │   │   └── Extensions/                   # Exception extensions
│   │   │
│   │   ├── Constants/                        # Shared constants
│   │   └── Utilities/                        # Utility classes
│   │
│   ├── Application/                          # Application layer patterns
│   │   ├── Behaviors/                        # MediatR pipeline behaviors
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   └── TransactionBehavior.cs
│   │   │
│   │   ├── Caching/                          # Caching abstractions
│   │   ├── Configuration/                    # App configuration
│   │   ├── Context/                          # Execution context
│   │   ├── Events/                           # Domain event dispatcher
│   │   ├── Exceptions/                       # Application exceptions
│   │   ├── Observability/                    # Telemetry, logging
│   │   ├── Outbox/                           # Transactional outbox pattern
│   │   ├── Pagination/                       # Pagination utilities
│   │   ├── Specifications/                   # Specification pattern
│   │   └── Validation/                       # Validation infrastructure
│   │
│   ├── Infrastructure/                       # Infrastructure patterns
│   │   ├── Caching/                          # Cache implementations
│   │   ├── Configuration/                    # Infrastructure config
│   │   ├── Events/                           # Event bus
│   │   ├── Logging/                          # Structured logging
│   │   ├── Messaging/                        # Message bus (future)
│   │   ├── Observability/                    # OpenTelemetry
│   │   ├── Persistence/                      # EF Core base classes
│   │   │   ├── Configurations/               # Base configurations
│   │   │   └── Interceptors/                 # EF Core interceptors
│   │   ├── Resilience/                       # Polly policies
│   │   └── Security/                         # Cryptography, secrets
│   │
│   ├── Domain/                               # Domain layer support
│   │   └── Validation/                       # Domain validation
│   │
│   └── Web/                                  # HTTP/Web concerns
│       ├── Configuration/                    # Web configuration
│       ├── Contracts/                        # Base web contracts
│       ├── Endpoints/                        # Base endpoint classes
│       ├── Extensions/                       # HTTP extensions
│       ├── HealthChecks/                     # Health check infrastructure
│       ├── Middleware/                       # Reusable middleware
│       ├── OpenApi/                          # Swagger configuration
│       ├── Pagination/                       # Web pagination
│       └── ProblemDetails/                   # Problem details mapping
│
├── Modules/                                  # Business modules (bounded contexts)
│   ├── Identity/                             # Identity & Authentication module
│   │   ├── Domain/                           # Identity domain layer
│   │   │   ├── Aggregates/
│   │   │   │   ├── AxonPrincipal/            # Principal aggregate
│   │   │   │   │   ├── AxonPrincipal.cs
│   │   │   │   │   ├── AxonPrincipal.Commands.cs
│   │   │   │   │   └── AxonPrincipal.Queries.cs
│   │   │   │   └── Wallet/                   # Wallet aggregate
│   │   │   │
│   │   │   ├── Entities/                     # Owned entities
│   │   │   │   ├── IdentityCredential.cs
│   │   │   │   ├── WalletOwnership.cs
│   │   │   │   └── PrincipalChainDefault.cs
│   │   │   │
│   │   │   ├── ValueObjects/                 # Identity value objects
│   │   │   │   ├── Address.cs
│   │   │   │   ├── ProofType.cs
│   │   │   │   ├── ProviderType.cs
│   │   │   │   └── ChainId.cs
│   │   │   │
│   │   │   ├── Events/                       # Domain events
│   │   │   │   ├── PrincipalChangedEvent.cs
│   │   │   │   ├── CredentialChangedEvent.cs
│   │   │   │   └── OwnershipChangedEvent.cs
│   │   │   │
│   │   │   └── Repositories/                 # Repository interfaces
│   │   │       ├── IAxonPrincipalWriteRepository.cs
│   │   │       └── IAxonPrincipalReadRepository.cs
│   │   │
│   │   ├── Application/                      # Identity application layer
│   │   │   ├── Commands/                     # Command use cases
│   │   │   │   ├── ExchangeCredential/
│   │   │   │   │   ├── ExchangeCredentialCommand.cs
│   │   │   │   │   ├── ExchangeCredentialCommandHandler.cs
│   │   │   │   │   └── ExchangeCredentialCommandValidator.cs
│   │   │   │   ├── GenerateChallenge/
│   │   │   │   ├── VerifyWalletSignature/
│   │   │   │   └── RefreshToken/
│   │   │   │
│   │   │   ├── Queries/                      # Query use cases
│   │   │   │   └── GetMyPrincipal/
│   │   │   │       ├── GetMyPrincipalQuery.cs
│   │   │   │       └── GetMyPrincipalQueryHandler.cs
│   │   │   │
│   │   │   ├── Services/                     # Application services
│   │   │   │   ├── IPrincipalResolutionService.cs
│   │   │   │   └── IAutoRevocationService.cs
│   │   │   │
│   │   │   └── DTOs/                         # Data transfer objects
│   │   │
│   │   └── Infrastructure/                   # Identity infrastructure
│   │       ├── Persistence/
│   │       │   ├── DbContexts/
│   │       │   │   ├── IdentityWriteDbContext.cs
│   │       │   │   └── IdentityReadDbContext.cs
│   │       │   ├── Configurations/           # EF Core entity configs
│   │       │   │   └── AxonPrincipalConfiguration.cs
│   │       │   ├── Repositories/
│   │       │   │   ├── AxonPrincipalWriteRepository.cs
│   │       │   │   └── AxonPrincipalReadRepository.cs
│   │       │   └── Migrations/               # EF Core migrations
│   │       │
│   │       ├── Services/                     # Infrastructure services
│   │       │   ├── PrincipalResolutionService.cs
│   │       │   ├── WalletVerificationService.cs
│   │       │   ├── Ed25519SignatureVerifier.cs
│   │       │   └── AutoRevocationService.cs
│   │       │
│   │       └── ExternalServices/             # Third-party integrations
│   │           └── DynamicXyz/               # Dynamic.xyz client
│   │               ├── DynamicAuthService.cs
│   │               ├── JwksService.cs
│   │               └── Models/               # API models
│   │
│   └── Chat/                                 # Chat & Messaging module
│       ├── Domain/                           # Chat domain layer
│       │   ├── Aggregates/
│       │   │   ├── Conversation/
│       │   │   └── Message/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   ├── Events/
│       │   └── Repositories/
│       │
│       ├── Application/                      # Chat application layer
│       │   ├── Commands/
│       │   │   ├── CreateConversation/
│       │   │   └── SendMessage/
│       │   ├── Queries/
│       │   │   └── GetMessages/
│       │   └── Services/
│       │
│       └── Infrastructure/                   # Chat infrastructure
│           ├── Persistence/
│           │   ├── DbContexts/
│           │   │   ├── ChatWriteDbContext.cs
│           │   │   └── ChatReadDbContext.cs
│           │   ├── Configurations/
│           │   ├── Repositories/
│           │   └── Migrations/
│           └── Services/
│
└── Infrastructure/                           # Shared infrastructure (legacy, being phased out)
    └── ExternalServices/                     # External service clients
```

---

## Navigation Patterns

### By Feature (Vertical Slice)

**Example: Implementing "Verify Wallet" feature**

```
1. API Layer:
   src/Api/Endpoints/V1/Identity/VerifyWallet/
   ├── VerifyWalletEndpoint.cs
   ├── VerifyWalletRequest.cs
   ├── VerifyWalletResponse.cs
   └── VerifyWalletValidator.cs

2. Application Layer:
   src/Modules/Identity/Application/Commands/VerifyWallet/
   ├── VerifyWalletCommand.cs
   ├── VerifyWalletCommandHandler.cs
   └── VerifyWalletCommandValidator.cs

3. Domain Layer:
   src/Modules/Identity/Domain/Aggregates/AxonPrincipal/
   └── AxonPrincipal.Commands.cs  (VerifyWallet method)

4. Infrastructure Layer:
   src/Modules/Identity/Infrastructure/Services/
   └── WalletVerificationService.cs
```

### By Layer (Horizontal)

**Understanding Architectural Concerns:**

```
Domain Layer (Business Logic):
- src/Modules/Identity/Domain/
- src/Modules/Chat/Domain/

Application Layer (Use Cases):
- src/Modules/Identity/Application/
- src/Modules/Chat/Application/

Infrastructure Layer (External Concerns):
- src/Modules/Identity/Infrastructure/
- src/Modules/Chat/Infrastructure/

API Layer (HTTP):
- src/Api/
```

### By Module (Bounded Context)

**Identity Module:**
```
src/Modules/Identity/
├── Domain/           # Authentication, wallet ownership
├── Application/      # Auth workflows, commands/queries
└── Infrastructure/   # Dynamic.xyz, database, crypto
```

**Chat Module:**
```
src/Modules/Chat/
├── Domain/           # Conversations, messages
├── Application/      # Messaging workflows
└── Infrastructure/   # Database, AI services
```

---

## Test Project Structure (`tests/`)

```
tests/
├── Modules/
│   ├── Identity/
│   │   ├── Domain/                           # Domain logic tests
│   │   │   └── Axon.Modules.Identity.Domain.Tests.csproj
│   │   ├── Application/                      # Application layer tests
│   │   │   └── Axon.Modules.Identity.Application.Tests.csproj
│   │   ├── Infrastructure/                   # Infrastructure tests
│   │   │   └── Axon.Modules.Identity.Infrastructure.Tests.csproj
│   │   ├── Integration/                      # Integration tests
│   │   │   └── Axon.Modules.Identity.Integration.Tests.csproj
│   │   └── E2E/                              # End-to-end tests
│   │       └── Axon.Modules.Identity.E2E.Tests.csproj
│   │
│   └── Chat/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       ├── Integration/
│       ├── E2E/
│       └── Performance/                      # Performance tests
│
├── Api/
│   └── Axon.Api.Tests.csproj                # API-level tests
│
└── BuildingBlocks/
    ├── Infrastructure/
    │   └── BuildingBlocks.Infrastructure.Tests.csproj
    └── Testing/                              # Test utilities
        └── BuildingBlocks.Testing.csproj
```

**Test Mirroring Convention**: Test structure mirrors source structure exactly.

---

## Key File Locations

### Configuration Files

```
Root Configuration:
├── global.json                         # .NET SDK version (10.0)
├── Directory.Build.props               # Build properties (analyzers, nullable)
├── .editorconfig                       # Code style (file-scoped namespaces, etc.)
└── CLAUDE.md                           # AI agent instructions

API Configuration:
├── src/Api/appsettings.json            # Base configuration
├── src/Api/appsettings.Development.json # Development overrides
├── src/Api/appsettings.Production.json  # Production config
└── src/Api/Properties/launchSettings.json # Debug profiles
```

### Core Abstractions

```
CQRS Interfaces:
src/BuildingBlocks/Core/Abstractions/CQRS/
├── ICommand.cs
├── IQuery.cs
├── ICommandHandler.cs
└── IQueryHandler.cs

Domain Primitives:
src/BuildingBlocks/Core/Primitives/
├── Ids/                                # Strong typed IDs
└── ValueObjects/                       # Shared value objects

Error Handling:
src/BuildingBlocks/Core/Diagnostics/
├── Errors/Error.cs                     # Error type
└── Exceptions/                         # Domain exceptions
```

### Module Aggregates

```
Identity Module:
src/Modules/Identity/Domain/Aggregates/
├── AxonPrincipal/                      # User identity aggregate
│   ├── AxonPrincipal.cs
│   ├── AxonPrincipal.Commands.cs       # Business methods
│   └── AxonPrincipal.Queries.cs        # Query methods
└── Wallet/                             # Wallet aggregate
    └── Wallet.cs

Chat Module:
src/Modules/Chat/Domain/Aggregates/
├── Conversation/
└── Message/
```

---

## Module Boundaries

### Allowed Communication

```
✅ Within Module: Direct method calls
   Identity.Application → Identity.Domain ✓

✅ Cross-Module: Domain events only
   Identity.Domain raises UserCreated event
   Chat.Application listens to UserCreated event ✓

✅ API → Modules: Via MediatR
   Api.Endpoints → Identity.Application.Commands ✓
```

### Forbidden Communication

```
❌ Cross-Module Direct References:
   Chat.Application → Identity.Domain ✗

❌ Infrastructure → Domain:
   Identity.Infrastructure → Identity.Domain (except via interfaces) ✗

❌ Domain → Infrastructure:
   Identity.Domain → Identity.Infrastructure ✗
```

---

## Finding Code

### Finding a Feature

**Example: "Where is wallet verification implemented?"**

1. **API Entry Point**: `src/Api/Endpoints/V1/Identity/VerifyWallet/`
2. **Application Logic**: `src/Modules/Identity/Application/Commands/VerifyWallet/`
3. **Domain Logic**: `src/Modules/Identity/Domain/Aggregates/AxonPrincipal/`
4. **Infrastructure**: `src/Modules/Identity/Infrastructure/Services/WalletVerificationService.cs`

### Finding a Bug

**Example: "User authentication is failing"**

1. **Check Endpoint**: `src/Api/Endpoints/V1/Identity/ExchangeCredential/`
2. **Check Command**: `src/Modules/Identity/Application/Commands/ExchangeCredential/`
3. **Check Domain**: `src/Modules/Identity/Domain/Aggregates/AxonPrincipal/`
4. **Check External Service**: `src/Modules/Identity/Infrastructure/ExternalServices/DynamicXyz/`

### Finding Tests

**Example: "Where are wallet verification tests?"**

```
Domain Tests:
tests/Modules/Identity/Domain/AxonPrincipalTests.cs
(Test: LinkWallet_ValidAddress_ReturnsSuccess)

Application Tests:
tests/Modules/Identity/Application/Commands/VerifyWallet/
└── VerifyWalletCommandHandlerTests.cs

Integration Tests:
tests/Modules/Identity/Integration/WalletVerificationFlowTests.cs
```

---

## Related Documentation

- **Project Conventions** → [project-conventions.md](./project-conventions.md) - Naming, namespaces
- **Coding Standards** → [coding-standards.md](./coding-standards.md) - C# conventions
- **Modular Monolith** → [../architecture/modular-monolith.md](../architecture/modular-monolith.md) - Module boundaries
- **CQRS Patterns** → [../patterns/cqrs.md](../patterns/cqrs.md) - Command/query organization

---

**Last Updated**: 2025-09-29