# CQRS Patterns

**MediatR command/query templates with Result<T, Error> integration.**

---

## Core Concepts

**Command**: Mutates state, returns Result<T, Error>
**Query**: Reads data, returns Result<T, Error>

```csharp
// Interfaces (from BuildingBlocks.Core)
public interface ICommand<out TResponse> : IRequest<TResponse> { }
public interface IQuery<out TResponse> : IRequest<TResponse> { }
```

---

## Command Pattern

### Command Definition

```csharp
// Simple command
public sealed record CreatePrincipalCommand(
    PrincipalType Type
) : ICommand<Result<AxonUserId, Error>>;

// Command with idempotency
public sealed record ExchangeCredentialCommand(
    string BearerToken
) : IdentityBaseCommand<ExchangeOutcome>;

// Base class pattern (module-specific)
public abstract record IdentityBaseCommand<TResponse> : ICommand<Result<TResponse, Error>>;
```

### Command Handler

```csharp
public sealed class CreatePrincipalHandler
    : IRequestHandler<CreatePrincipalCommand, Result<AxonUserId, Error>>
{
    private readonly IAxonPrincipalRepository _repo;
    private readonly IUnitOfWork _uow;

    public async Task<Result<AxonUserId, Error>> Handle(
        CreatePrincipalCommand cmd,
        CancellationToken ct)
    {
        // 1. Domain operation
        var principal = AxonPrincipal.CreateHuman();

        // 2. Persist
        await _repo.AddAsync(principal, ct);
        await _uow.SaveChangesAsync(ct); // Domain events auto-dispatched

        // 3. Return ID
        return Result.Success<AxonUserId, Error>(principal.Id);
    }
}
```

### Complex Command Handler

```csharp
public sealed class LinkWalletHandler
    : IRequestHandler<LinkWalletCommand, Result<Unit, Error>>
{
    private readonly IAxonPrincipalRepository _repo;
    private readonly IUnitOfWork _uow;

    public async Task<Result<Unit, Error>> Handle(
        LinkWalletCommand cmd,
        CancellationToken ct)
    {
        // 1. Load aggregate
        var principal = await _repo.GetByIdAsync(cmd.PrincipalId, ct);
        if (principal is null)
            return Error.NotFound("Principal not found", "PRINCIPAL.NOT_FOUND");

        // 2. Create entity
        var ownership = WalletOwnership.Create(
            cmd.PrincipalId,
            cmd.WalletId,
            cmd.AccessMode,
            OwnershipStatus.Pending);

        // 3. Execute domain command with cross-aggregate check
        var result = principal.LinkWalletOwnership(
            ownership,
            checkConflicts: (walletId, mode, status) =>
                _repo.ExistsWithOwnershipAsync(walletId, mode, status, ct));

        if (result.IsFailure)
            return result.Error;

        // 4. Persist (events dispatched)
        await _uow.SaveChangesAsync(ct);

        return Result.Success<Unit, Error>(Unit.Value);
    }
}
```

---

## Query Pattern

### Query Definition

```csharp
// Simple query
public sealed record GetPrincipalByIdQuery(
    AxonUserId PrincipalId
) : IQuery<Result<PrincipalDto, Error>>;

// Query with caching hint
public sealed record GetMyPrincipalQuery(
    AxonUserId PrincipalId,
    string? IfNoneMatch = null
) : IdentityBaseQuery<CurrentUserResult>, ICacheableQuery;

// Base class pattern
public abstract record IdentityBaseQuery<TResponse> : IQuery<Result<TResponse, Error>>;
```

### Query Handler

```csharp
public sealed class GetPrincipalByIdHandler
    : IRequestHandler<GetPrincipalByIdQuery, Result<PrincipalDto, Error>>
{
    private readonly IAxonPrincipalReadRepository _repo;

    public async Task<Result<PrincipalDto, Error>> Handle(
        GetPrincipalByIdQuery query,
        CancellationToken ct)
    {
        // 1. Query read model
        var principal = await _repo.GetByIdAsync(query.PrincipalId, ct);

        // 2. Null check
        if (principal is null)
            return Error.NotFound("Principal not found", "PRINCIPAL.NOT_FOUND");

        // 3. Map to DTO
        var dto = new PrincipalDto(
            principal.Id.ToString(),
            principal.Type.ToString(),
            principal.RiskTier.ToString());

        return Result.Success<PrincipalDto, Error>(dto);
    }
}
```

### Query with Projection (Optimized)

```csharp
public sealed class GetMyPrincipalHandler
    : IRequestHandler<GetMyPrincipalQuery, Result<CurrentUserResult, Error>>
{
    private readonly IdentityReadDbContext _db;

    public async Task<Result<CurrentUserResult, Error>> Handle(
        GetMyPrincipalQuery query,
        CancellationToken ct)
    {
        // Direct projection (no entity materialization)
        var result = await _db.AxonPrincipals
            .Where(p => p.Id == query.PrincipalId)
            .Select(p => new CurrentUserResult(
                p.Id.ToString(),
                p.Type.ToString(),
                p.RiskTier.ToString(),
                p.WalletOwnerships
                    .Where(w => !w.IsDeleted)
                    .Select(w => new WalletDto(
                        w.WalletId.ToString(),
                        w.AccessMode.ToString(),
                        w.Status.ToString()))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Error.NotFound("Principal not found", "PRINCIPAL.NOT_FOUND")
            : Result.Success<CurrentUserResult, Error>(result);
    }
}
```

---

## MediatR Pipeline Behaviors

### Validation Behavior (FluentValidation)

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            var errors = failures.Select(f =>
                Error.Validation(f.ErrorMessage, f.ErrorCode));
            return (TResponse)(object)Error.Aggregate("Validation failed", errors);
        }

        return await next();
    }
}
```

### Logging Behavior

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next();

        _logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}
```

---

## Registration (DI)

```csharp
// In ServiceCollectionExtensions
public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
{
    // Register MediatR
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly);

        // Add behaviors (order matters!)
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
    });

    // Register validators
    services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly);

    return services;
}
```

---

## Command vs Query Decision Tree

```
Mutates state?
  ├─ YES → Command
  │   ├─ Returns ID → ICommand<Result<TId, Error>>
  │   └─ Returns nothing → ICommand<Result<Unit, Error>>
  └─ NO → Query
      ├─ Single entity → IQuery<Result<TDto, Error>>
      └─ Collection → IQuery<Result<List<TDto>, Error>>
```

---

## Best Practices

### ✅ DO
- One handler per command/query
- Commands return `Result<Unit, Error>` or `Result<TId, Error>`
- Queries use read-optimized DbContext
- Validation in pipeline behavior (not handler)
- Domain logic in aggregates (not handlers)

### ❌ DON'T
- Commands in query handlers (CQRS violation)
- Queries in command handlers (use events instead)
- Business logic in handlers (belongs in domain)
- Direct DbContext access in commands (use repository)
- Throwing exceptions (use Result<T, Error>)

---

## Related Docs

- [Domain Modeling](./domain-modeling.md) - Aggregates & commands
- [Quick Reference](./00-QUICK-REFERENCE.md) - Templates
- [Identity Module](../../modules/identity/00-INDEX.md) - Real examples

---

**Lines**: ~250
**Focus**: Copy-paste MediatR templates