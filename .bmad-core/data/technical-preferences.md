<!-- Powered by BMAD™ Core -->

# Axon Backend Technical Preferences

## Core Architecture Patterns

### Clean Architecture (MANDATORY)
```yaml
layer_separation:
  Domain: 
    - Pure business logic, no external dependencies
    - Aggregates, entities, value objects, domain events
    - Business rules and invariants
    
  Application:
    - CQRS handlers using MediatR
    - Commands modify state, queries read data
    - Application services and use cases
    
  Infrastructure:
    - External dependencies (database, HTTP, messaging)
    - Repository implementations
    - External service adapters
    
  API:
    - FastEndpoints for minimal APIs
    - No business logic allowed
    - Request/response DTOs only
```

### CQRS + MediatR (MANDATORY)
```yaml
cqrs_patterns:
  commands:
    - Modify state operations
    - Return Result<T> for error handling
    - Single responsibility per command
    - Validation via FluentValidation
    
  queries:
    - Read-only operations
    - No side effects allowed
    - Return data models or DTOs
    - Efficient data retrieval patterns
    
  handlers:
    - One handler per command/query
    - Dependency injection for services
    - Proper error handling with Result pattern
```

### Domain-Driven Design (MANDATORY)
```yaml
ddd_patterns:
  aggregates:
    - Consistency boundaries around related entities
    - Single aggregate per transaction
    - Rich domain models with behavior
    
  entities:
    - Strong IDs using StrongId<T> pattern
    - Identity-based equality
    - Encapsulated business logic
    
  value_objects:
    - Immutable data structures
    - Value-based equality
    - Self-validating
    
  domain_events:
    - Published via MediatR notifications
    - Decouple aggregates from side effects
    - Support eventual consistency
```

## Technology Stack

### .NET 10 Requirements (MANDATORY)
```yaml
dotnet_version: "10.0 preview 5.25277.114"
modern_csharp_patterns:
  - File-scoped namespaces: "namespace Axon.Modules.ModuleName;"
  - Records for DTOs: "public record CreateUserRequest(string Email, string Name);"
  - Target-typed new: "List<string> items = new();"
  - Nullable reference types enabled and handled explicitly
  - Primary constructors where appropriate
  - Global using statements in GlobalUsings.cs
```

### Core Libraries (MANDATORY)
```yaml
required_packages:
  web_framework: "FastEndpoints - Minimal API alternative to controllers"
  mediator: "MediatR - CQRS command/query dispatch"  
  validation: "FluentValidation - Request and domain validation"
  orm: "Entity Framework Core 9.0 - ORM with PostgreSQL"
  database: "PostgreSQL - Primary database"
  json: "System.Text.Json - API serialization"
  testing: "xUnit + Shouldly + NSubstitute + Testcontainers"
  observability: "OpenTelemetry - Monitoring and tracing"
```

### Data Patterns (MANDATORY)
```yaml
data_access:
  orm: "Entity Framework Core with PostgreSQL"
  repositories: "Repository pattern for aggregate persistence"
  migrations: "Code-first migrations for schema changes" 
  connection_strings: "Environment-specific configuration"
  
json_serialization:
  policy: "camelCase property naming"
  null_handling: "Ignore null values in responses"
  custom_converters: "Strong IDs and value objects auto-generated"
```

## Error Handling Patterns

### Result Pattern (MANDATORY)
```csharp
// All operations return Result<T>
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result<User>.Failure(Error.Validation("Email required"));
        
    return Result<User>.Success(new User(email));
}

// Application handlers handle Results
public async Task<Result<UserResponse>> Handle(CreateUserCommand command)
{
    var userResult = User.Create(command.Email);
    if (userResult.IsFailure)
        return Result<UserResponse>.Failure(userResult.Error);
        
    await repository.Add(userResult.Value);
    return Result<UserResponse>.Success(new UserResponse(userResult.Value.Id));
}
```

### Strong ID Pattern (MANDATORY)
```csharp
// Type-safe entity identification
public record UserId(Guid Value) : StrongId<Guid>(Value);
public record OrderId(Guid Value) : StrongId<Guid>(Value);

// Usage in entities
public class User
{
    public UserId Id { get; private set; }
    public string Email { get; private set; }
    
    private User() {} // EF constructor
    
    public User(UserId id, string email)
    {
        Id = id;
        Email = email;
    }
}
```

## API Patterns

### FastEndpoints (MANDATORY)
```csharp
// Minimal API endpoint structure
public class CreateUserEndpoint : Endpoint<CreateUserRequest, UserResponse>
{
    public override void Configure()
    {
        Post("/api/users");
        AllowAnonymous();
        Validator<CreateUserRequestValidator>();
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var command = new CreateUserCommand(req.Email, req.Name);
        var result = await SendAsync(command, ct);
        
        if (result.IsFailure)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        
        await SendOkAsync(new UserResponse(result.Value), ct);
    }
}
```

### Validation (MANDATORY)
```csharp
// FluentValidation for all requests
public class CreateUserRequestValidator : Validator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);
            
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
```

## Testing Standards

### Test Architecture (MANDATORY)
```yaml
test_levels:
  unit_tests:
    - Business logic in Domain and Application layers
    - >90% coverage requirement
    - Fast execution (<1000ms total)
    - No external dependencies
    
  integration_tests:
    - API endpoints with real database
    - Use Testcontainers for PostgreSQL
    - Test complete request/response cycle
    - Include authentication/authorization
    
  architecture_tests:
    - Layer boundary enforcement
    - Dependency direction validation
    - Naming convention compliance
```

### Testing Tools (MANDATORY)
```yaml
testing_stack:
  framework: "xUnit - Modern testing framework"
  assertions: "Shouldly - Fluent, readable assertions"
  mocking: "NSubstitute - Clean mocking syntax"
  containers: "Testcontainers - Integration test databases"
  coverage: ">90% for business logic"
```

## Quality Standards

### Performance Requirements (MANDATORY)
```yaml
performance_targets:
  api_response_time: "<200ms for 95th percentile"
  database_query_time: "<50ms for simple queries"
  memory_usage: "Efficient object allocation"
  scalability: "Horizontal scaling ready"
```

### Security Requirements (MANDATORY)
```yaml
security_standards:
  authentication: "JWT Bearer tokens with refresh"
  authorization: "Role-based access control"
  input_validation: "All inputs validated via FluentValidation"
  sql_injection: "EF Core parameterized queries only"
  sensitive_data: "No secrets in logs or responses"
```

### Code Quality (MANDATORY)
```yaml
code_standards:
  warnings_as_errors: "Enabled in Release mode"
  analyzers: "Microsoft.CodeAnalysis.NetAnalyzers enabled"
  documentation: "XML documentation for public APIs"
  nullable_context: "Enabled with proper null handling"
  deterministic_builds: "Reproducible build outputs"
```

## Project Structure Standards

### Module Organization (MANDATORY)
```
src/
├── Api/                    # HTTP host, FastEndpoints
├── BuildingBlocks/         # Shared technical infrastructure
│   ├── Core/              # Domain primitives, CQRS, Result<T>
│   ├── Application/       # MediatR behaviors, validation
│   ├── Infrastructure/    # Persistence, caching, messaging
│   └── Web/              # HTTP concerns, problem details
└── Modules/               # Business modules (bounded contexts)
    └── ModuleName/        # Clean Architecture per module
        ├── Domain/        # Aggregates, entities, domain events
        ├── Application/   # Commands, queries, handlers
        └── Infrastructure/ # Repositories, external adapters
```

### File Naming (MANDATORY)
```yaml
naming_conventions:
  commands: "CreateUserCommand.cs"
  queries: "GetUserQuery.cs"
  handlers: "CreateUserCommandHandler.cs"
  entities: "User.cs"
  value_objects: "EmailAddress.cs"
  strong_ids: "UserId.cs"
  endpoints: "CreateUserEndpoint.cs"
  validators: "CreateUserRequestValidator.cs"
```

## Blockchain/Web3 Specific

### Solana Integration (DOMAIN-SPECIFIC)
```yaml
web3_patterns:
  wallet_integration: "Non-custodial wallet connection patterns"
  transaction_safety: "Always validate transaction signatures"
  rpc_resilience: "Multiple RPC endpoints with fallback"
  keypair_security: "Never store private keys server-side"
```

### Trading Platform Requirements (DOMAIN-SPECIFIC) 
```yaml
trading_standards:
  order_matching: "Fair queue processing with audit trail"
  settlement: "Atomic transaction patterns"
  compliance: "Regulatory requirement tracking"
  risk_management: "Position limits and circuit breakers"
```

## Development Workflow

### Git Workflow (RECOMMENDED)
```yaml
branching_strategy: "Feature branches from main"
commit_messages: "Conventional commits format"
pull_requests: "Required for all changes"
ci_pipeline: "Build, test, lint on every PR"
```

### Build Process (MANDATORY)
```yaml
build_commands:
  restore: "dotnet restore"
  build: "dotnet build"
  test: "dotnet test"
  run: "dotnet run --project src/Api"
  clean: "dotnet clean && dotnet build"
```

This configuration ensures all BMAD agents understand and apply Axon's specific technical standards and architectural patterns.