# ADR-002: CQRS with MediatR

**Status**: ✅ Accepted
**Date**: 2025-01-15
**Deciders**: Engineering Team
**Technical Story**: Application Layer Design

---

## Context and Problem Statement

Axon needs a pattern for organizing application logic that keeps read and write operations separated, enables testability, and provides clear request/response flows. The choice of command/query pattern significantly impacts code organization, testing, and future scalability.

**Key Question**: How should we organize application-layer logic to maximize testability and maintainability?

---

## Decision Drivers

- **Separation of Concerns**: Read operations have different requirements than write operations
- **Testability**: Need to test handlers in isolation
- **Scalability**: Reads and writes often scale differently
- **Code Organization**: Clear structure for application logic
- **Pipeline Behaviors**: Cross-cutting concerns (validation, logging, transactions)
- **Team Familiarity**: MediatR is well-known in .NET community

---

## Considered Options

### Option 1: Traditional Service Layer

Application services with mixed read/write methods.

**Pros:**
- Simple, familiar pattern
- No additional library dependencies
- Direct method calls

**Cons:**
- Read and write logic mixed together
- Difficult to optimize separately
- Cross-cutting concerns duplicated
- Harder to test (large service classes)

### Option 2: CQRS with Manual Dispatch

Command/query separation with custom dispatcher.

**Pros:**
- Full control over dispatch mechanism
- No third-party dependencies
- Lighter weight

**Cons:**
- Need to build dispatcher infrastructure
- No ecosystem (behaviors, pipelines)
- More boilerplate code

### Option 3: CQRS with MediatR ✅

Command/query separation using MediatR library.

**Pros:**
- Industry-standard pattern
- Rich ecosystem (behaviors, pipelines)
- Excellent testability
- Clear request→handler mapping
- Built-in dependency injection
- Pipeline behaviors for cross-cutting concerns

**Cons:**
- Additional library dependency
- Slight performance overhead (negligible)
- Learning curve for new developers

---

## Decision Outcome

**Chosen option**: "Option 3: CQRS with MediatR"

**Rationale:**

1. **Clear Separation**: Commands (write) and Queries (read) have fundamentally different concerns. MediatR enforces this separation structurally.

2. **Testability**: Each handler is a small, focused class that's easy to test in isolation.

3. **Pipeline Behaviors**: Validation, logging, transactions, and other cross-cutting concerns can be implemented once and applied to all commands/queries.

4. **Read Optimization**: Queries can use read-optimized projections, while commands use domain models.

5. **Industry Standard**: Well-documented pattern with strong community support.

---

## Consequences

### Positive

✅ **Clean Separation**: Write and read models can evolve independently.

✅ **Testability**: Single-purpose handlers are easy to unit test.

✅ **Cross-Cutting Concerns**: Pipeline behaviors eliminate duplication (validation, logging, transactions).

✅ **Performance Optimization**: Queries can bypass domain layer, use raw SQL/Dapper if needed.

✅ **Code Discoverability**: Clear handler-per-use-case structure.

### Negative

❌ **More Files**: Each use case requires command/query + handler + validator files.

❌ **Learning Curve**: New developers must learn MediatR concepts.

❌ **Indirection**: Request→handler mapping not immediately visible in code (resolved by IDE tooling).

### Risks

**Risk 1: Performance Overhead**
- **Assessment**: MediatR overhead is <1ms per request (negligible)
- **Mitigation**: Use scoped handlers, avoid unnecessary middleware

**Risk 2: Over-Abstraction**
- **Mitigation**: Keep handlers simple, avoid complex pipeline chains

---

## Implementation Notes

### Command Pattern

```csharp
// Command (write operation)
public sealed record CreateUserCommand(string Email) : ICommand<Result<UserId, Error>>;

// Handler
public sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Result<UserId, Error>>
{
    private readonly IUserRepository _repository;

    public async Task<Result<UserId, Error>> Handle(
        CreateUserCommand command,
        CancellationToken ct)
    {
        // 1. Validate business rules
        // 2. Create domain entity
        // 3. Save to repository
        // 4. Return result
    }
}

// Validator (runs in pipeline before handler)
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
```

### Query Pattern

```csharp
// Query (read operation)
public sealed record GetUserByIdQuery(UserId Id) : IQuery<Result<UserDto, Error>>;

// Handler
public sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, Result<UserDto, Error>>
{
    private readonly IUserReadRepository _repository;
    private readonly IMemoryCache _cache;

    public async Task<Result<UserDto, Error>> Handle(
        GetUserByIdQuery query,
        CancellationToken ct)
    {
        // 1. Check cache
        // 2. Query database (read-optimized projection)
        // 3. Map to DTO
        // 4. Cache result
        // 5. Return result
    }
}
```

### Pipeline Behaviors

```csharp
// Validation behavior (runs for all commands/queries)
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Run all validators
        var failures = await ValidateAsync(request, ct);

        if (failures.Any())
            return CreateValidationError(failures);  // Short-circuit

        // Continue pipeline
        return await next();
    }
}

// Register in DI
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateUserCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});
```

### Endpoint Integration

```csharp
public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    private readonly IMediator _mediator;

    public override void Configure()
    {
        Post("/api/v1/users");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        // Map request → command
        var command = new CreateUserCommand(req.Email);

        // Dispatch via MediatR
        var result = await _mediator.Send(command, ct);

        // Map result → response
        await result.Match(
            onSuccess: userId => SendOkAsync(new CreateUserResponse(userId), ct),
            onFailure: error => SendResultAsync(Results.Problem(error.ToProblemDetails()))
        );
    }
}
```

---

## Related Decisions

- [ADR-001: Modular Monolith](./001-modular-monolith.md) - Module boundaries enforced via CQRS
- [ADR-003: Result Pattern](./003-result-pattern.md) - Handlers return Result<T, Error>
- [ADR-006: FastEndpoints](./006-fastendpoints.md) - Endpoints dispatch to MediatR

---

## References

- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Vertical Slice Architecture](https://jimmybogard.com/vertical-slice-architecture/)

---

**Last Updated**: 2025-09-29