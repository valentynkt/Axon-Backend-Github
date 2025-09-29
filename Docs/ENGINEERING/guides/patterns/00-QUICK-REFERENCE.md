# 🔥 Quick Reference

**Most-used patterns. Copy-paste templates for 80% of daily work.**

---

## Result<T, Error>

```csharp
// Basic usage
Result.Success<User, Error>(user)
Result.Failure<User, Error>(Error.NotFound("Not found", "USER.NOT_FOUND"))

// Chaining
return ValidateInput(request)
    .Bind(validated => CreateEntity(validated))
    .Bind(entity => _repo.SaveAsync(entity))
    .Map(entity => entity.Id);

// Pattern matching
return await result.Match(
    onSuccess: value => SendOkAsync(value, ct),
    onFailure: error => SendResultAsync(Results.Problem(error.ToProblem())));
```

---

## StrongId<T>

```csharp
// Definition
[StronglyTypedId(Template.Guid)]
public readonly partial struct AxonUserId { }

// Usage
AxonUserId.New()              // Create
AxonUserId.From(Guid value)   // From existing
userId.Value                   // Get Guid

// Prevents bugs
void Process(AxonUserId id) { }
void Process(WalletId id) { }
Process(walletId); // ❌ Compile error - type safety!
```

---

## Error Factories

```csharp
// 4xx
Error.Validation("Required", "FIELD.REQUIRED")
Error.NotFound("Not found", "RESOURCE.NOT_FOUND")
Error.Conflict("Duplicate", "RESOURCE.CONFLICT")
Error.BusinessRule("Invalid", "BUSINESS.RULE")
Error.Unauthorized("Auth required", "AUTH.REQUIRED")

// 5xx
Error.Internal("Failed", "INTERNAL.ERROR", ex)
Error.External("Service down", "EXTERNAL.DOWN", ex)
Error.Persistence("DB error", "DB.ERROR", ex)

// HTTP mapping (automatic)
Error.Validation    → 400
Error.NotFound      → 404
Error.Conflict      → 409
Error.BusinessRule  → 422
Error.Internal      → 500
```

---

## CQRS

### Command

```csharp
// Definition
public sealed record ExchangeCredentialCommand(string BearerToken)
    : ICommand<ExchangeOutcome>;

// Handler
public sealed class ExchangeCredentialHandler
    : IRequestHandler<ExchangeCredentialCommand, Result<ExchangeOutcome, Error>>
{
    public async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand cmd, CancellationToken ct)
    {
        // Business logic
        return await _orchestrator.ExchangeAsync(cmd.BearerToken, ct);
    }
}
```

### Query

```csharp
// Definition
public sealed record GetMyPrincipalQuery(AxonUserId Id)
    : IQuery<CurrentUserResult>;

// Handler
public sealed class GetMyPrincipalHandler
    : IRequestHandler<GetMyPrincipalQuery, Result<CurrentUserResult, Error>>
{
    public async Task<Result<CurrentUserResult, Error>> Handle(
        GetMyPrincipalQuery query, CancellationToken ct)
    {
        var principal = await _repo.GetByIdAsync(query.Id, ct);
        return principal is null
            ? Error.NotFound("Not found", "PRINCIPAL.NOT_FOUND")
            : Result.Success<CurrentUserResult, Error>(MapToDto(principal));
    }
}
```

---

## Aggregate Root Template

```csharp
public sealed class AxonPrincipal : AggregateRoot<AxonUserId>
{
    // Private state
    private readonly List<WalletOwnership> _wallets = [];

    // Read-only
    public IReadOnlyCollection<WalletOwnership> Wallets => _wallets.AsReadOnly();

    // Properties
    public PrincipalType Type { get; private set; }

    // Factory
    public static AxonPrincipal CreateHuman() => new(AxonUserId.New(), PrincipalType.Human);

    // Command (enforce invariants)
    public Result<Unit, Error> LinkWallet(WalletOwnership ownership)
    {
        // 1. Idempotency
        if (_wallets.Any(w => w.WalletId == ownership.WalletId))
            return Result.Success<Unit, Error>(Unit.Value);

        // 2. Invariant
        if (_wallets.Count >= 10)
            return Error.BusinessRule("Max 10 wallets", "WALLET.MAX");

        // 3. Apply
        _wallets.Add(ownership);

        // 4. Event
        RaiseDomainEvent(new WalletLinkedEvent(Id, ownership.WalletId));

        return Result.Success<Unit, Error>(Unit.Value);
    }
}
```

---

## FastEndpoints

### Command Endpoint

```csharp
public sealed class ExchangeEndpoint
    : BaseIdentityCommandEndpoint<ExchangeRequest, ExchangeResponse, ExchangeCommand, ExchangeOutcome>
{
    protected override string GetRoute() => "/api/v1/auth/exchange";

    public override void Configure()
    {
        base.Configure();
        Options(x => x.RequireRateLimiting("AuthExchange"));
    }

    protected override Task<Result<ExchangeCommand, Error>> ExecuteCommand(
        ExchangeRequest req, CancellationToken ct)
    {
        var token = HttpContext.Request.Headers.Authorization.FirstOrDefault()?["Bearer ".Length..];
        if (string.IsNullOrEmpty(token))
            return Task.FromResult(Result.Failure<ExchangeCommand, Error>(
                Error.Unauthorized("Missing token", "AUTH.MISSING_TOKEN")));

        return Task.FromResult(Result.Success<ExchangeCommand, Error>(new ExchangeCommand(token)));
    }

    protected override Task<Result<ExchangeResponse, Error>> MapDomainToResponseAsync(
        ExchangeOutcome outcome, CancellationToken ct)
    {
        var response = new ExchangeResponse(outcome.AccessToken, outcome.ExpiresIn);
        return Task.FromResult(Result.Success<ExchangeResponse, Error>(response));
    }
}
```

### Query Endpoint

```csharp
public sealed class GetMyPrincipalEndpoint : BaseIdentityQueryEndpoint<CurrentUserResult>
{
    public override void Configure()
    {
        Get("/api/v1/auth/me");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
    }

    protected override async Task<Result<CurrentUserResult, Error>> ExecuteQueryAsync(CancellationToken ct)
    {
        var principalId = User.GetPrincipalId();
        if (principalId is null)
            return Error.Unauthorized("Invalid token", "AUTH.INVALID_TOKEN");

        return await _mediator.Send(new GetMyPrincipalQuery(principalId.Value), ct);
    }
}
```

---

## EF Core Configuration

```csharp
// Entity configuration
public sealed class AxonPrincipalConfiguration : IEntityTypeConfiguration<AxonPrincipal>
{
    public void Configure(EntityTypeBuilder<AxonPrincipal> builder)
    {
        builder.ToTable("axon_principals");

        // StrongId
        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => AxonUserId.From(value))
            .ValueGeneratedNever();

        // Enum as string
        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Owned collection
        builder.OwnsMany(p => p.WalletOwnerships, wb =>
        {
            wb.ToTable("wallet_ownerships");
            wb.WithOwner().HasForeignKey("PrincipalId");
            wb.Property(w => w.Id).HasConversion(id => id.Value, v => WalletOwnershipId.From(v));
        });

        // Concurrency
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
```

---

## Domain Events

```csharp
// Definition
public sealed record WalletLinkedEvent(AxonUserId PrincipalId, WalletId WalletId) : DomainEvent;

// Raising
RaiseDomainEvent(new WalletLinkedEvent(Id, walletId));

// Handling
public sealed class WalletLinkedHandler
    : INotificationHandler<DomainEventNotification<WalletLinkedEvent>>
{
    public async Task Handle(DomainEventNotification<WalletLinkedEvent> n, CancellationToken ct)
    {
        await _publisher.PublishAsync(new WalletLinkedIntegrationEvent(n.DomainEvent.WalletId), ct);
    }
}
```

---

## Module Boundaries

```
✅ ALLOWED:
- Commands/Queries within module
- Domain events for cross-module communication

❌ FORBIDDEN:
- Direct cross-module service calls
- Shared database tables between modules
- Cross-module command/query calls
```

---

## Common Patterns

### No-op Guard (Idempotency)

```csharp
public Result<Unit, Error> UpdateRiskTier(RiskTier tier)
{
    if (RiskTier == tier) return Result.Success<Unit, Error>(Unit.Value);
    // ... apply change
}
```

### Cross-Aggregate Validation (Function Injection)

```csharp
public Result<Unit, Error> LinkWallet(
    WalletOwnership ownership,
    Func<WalletId, Result<bool, Error>> checkConflict)
{
    var conflict = checkConflict(ownership.WalletId);
    if (conflict.IsFailure || conflict.Value)
        return Error.Conflict("Conflict", "WALLET.EXISTS");
    // ... proceed
}
```

### Command Handler

```csharp
public async Task<Result<Unit, Error>> Handle(LinkWalletCommand cmd, CancellationToken ct)
{
    // 1. Load
    var principal = await _repo.GetByIdAsync(cmd.PrincipalId, ct);
    if (principal is null) return Error.NotFound("Not found", "PRINCIPAL.NOT_FOUND");

    // 2. Execute
    var result = principal.LinkWallet(ownership, (wId) => _repo.ExistsAsync(wId, ct));
    if (result.IsFailure) return result.Error;

    // 3. Persist
    await _uow.SaveChangesAsync(ct);
    return Result.Success<Unit, Error>(Unit.Value);
}
```

---

## Key Rules

### ✅ DO
- `Result<T, Error>` for all business logic
- StrongId for type safety
- Private setters on aggregates
- No-op guards for idempotency
- `RaiseDomainEvent()` after changes

### ❌ DON'T
- Exceptions for business logic
- Public setters on aggregates
- Primitive IDs (Guid, int)
- Direct cross-module calls
- Anemic domain models

---

## Commands

```bash
# Build
dotnet build

# Run
dotnet run --project src/Api

# Tests
dotnet test

# Migration
dotnet ef migrations add <Name> --project src/Modules/Identity/Infrastructure
dotnet ef database update --project src/Modules/Identity/Infrastructure
```

---

**For Details**:
- [Domain Modeling](./domain-modeling.md)
- [CQRS](./cqrs.md)
- [System Overview](../architecture/system-overview.md)
- [Identity Module](../../modules/identity/00-INDEX.md)

**Lines**: ~400 (60% reduction from 1025)