# Domain Modeling

**DDD patterns with Result<T>, StrongId<T>, aggregates. Copy-paste templates for AI code generation.**

---

## Result<T, Error> - Core API

```csharp
// Success/Failure
Result<T, Error> Result.Success<T, Error>(T value)
Result<T, Error> Result.Failure<T, Error>(Error error)

// Chaining
result.Bind(x => DoSomething(x))  // Returns Result<U, Error>
result.Map(x => Transform(x))     // Returns Result<U, Error>

// Pattern Match
result.Match(
    onSuccess: value => SendOkAsync(value),
    onFailure: error => SendResultAsync(error.ToProblem()))
```

### Template: Domain Command with Result

```csharp
public Result<Unit, Error> UpdateRiskTier(RiskTier tier)
{
    // 1. No-op guard (idempotency)
    if (RiskTier == tier) return Result.Success<Unit, Error>(Unit.Value);

    // 2. Business rule validation
    if (Type == PrincipalType.Service && tier != RiskTier.Low)
        return Error.BusinessRule("Service principals must have low risk", "RISK.CONSTRAINT");

    // 3. Apply change
    RiskTier = tier;

    // 4. Raise event
    RaiseDomainEvent(new RiskTierChangedEvent(Id, tier));

    return Result.Success<Unit, Error>(Unit.Value);
}
```

---

## StrongId<T> - Type Safety

```csharp
// Definition (using StronglyTypedId source generator)
[StronglyTypedId(Template.Guid)]
public readonly partial struct AxonUserId { }

[StronglyTypedId(Template.Guid)]
public readonly partial struct WalletId { }

// Usage
AxonUserId.New()              // Create new
AxonUserId.From(Guid value)   // From existing
axonUserId.Value              // Get underlying Guid

// EF Core
builder.Property(p => p.Id)
    .HasConversion(
        id => id.Value,
        value => AxonUserId.From(value))
    .ValueGeneratedNever();
```

---

## Error Factory Methods (Quick Reference)

```csharp
// 4xx Client Errors
Error.Validation("Email required", "EMAIL.REQUIRED")
Error.NotFound("Not found", "RESOURCE.NOT_FOUND")
Error.Conflict("Already exists", "RESOURCE.CONFLICT")
Error.BusinessRule("Invariant violated", "BUSINESS.RULE")
Error.Unauthorized("Auth required", "AUTH.REQUIRED")

// 5xx Server Errors
Error.Internal("Unexpected", "INTERNAL.ERROR", exception)
Error.External("Service down", "EXTERNAL.DOWN", exception)
Error.Persistence("DB failed", "DB.ERROR", exception)

// Enrichment
error.WithMetadata("Key", value)
     .WithCorrelationFromActivity()
     .WithSeverity(ErrorSeverity.Warning)
```

---

## Aggregate Root Template

**Pattern**: Encapsulate collections, enforce invariants, raise events.

```csharp
public sealed class AxonPrincipal : AggregateRoot<AxonUserId>
{
    // Private state (encapsulation)
    private readonly List<WalletOwnership> _wallets = [];

    // Read-only access
    public IReadOnlyCollection<WalletOwnership> Wallets => _wallets.AsReadOnly();

    // Domain properties (private setters)
    public PrincipalType Type { get; private set; }
    public RiskTier RiskTier { get; private set; }

    // Factory method (not constructor)
    public static AxonPrincipal CreateHuman(AxonUserId? id = null)
        => new(id ?? AxonUserId.New(), PrincipalType.Human);

    // Command method template
    public Result<Unit, Error> LinkWallet(
        WalletOwnership ownership,
        Func<WalletId, Result<bool, Error>> checkConflict)
    {
        // 1. Idempotency
        if (_wallets.Any(w => w.WalletId == ownership.WalletId))
            return Result.Success<Unit, Error>(Unit.Value);

        // 2. Invariant
        if (_wallets.Count >= 10)
            return Error.BusinessRule("Max 10 wallets", "WALLET.MAX");

        // 3. Cross-aggregate check (via function injection)
        var conflict = checkConflict(ownership.WalletId);
        if (conflict.IsFailure || conflict.Value)
            return Error.Conflict("Already owned", "WALLET.CONFLICT");

        // 4. Apply change
        _wallets.Add(ownership);

        // 5. Raise event
        RaiseDomainEvent(new WalletLinkedEvent(Id, ownership.WalletId));

        return Result.Success<Unit, Error>(Unit.Value);
    }
}
```

---

## Entity Template

```csharp
public sealed class WalletOwnership : Entity<WalletOwnershipId>
{
    public AxonUserId PrincipalId { get; private set; }
    public WalletId WalletId { get; private set; }
    public OwnershipStatus Status { get; private set; }

    // Factory
    public static WalletOwnership Create(AxonUserId principalId, WalletId walletId)
        => new() { Id = WalletOwnershipId.New(), PrincipalId = principalId, WalletId = walletId };

    // Command
    public Result<Unit, Error> UpdateStatus(OwnershipStatus newStatus)
    {
        if (Status == OwnershipStatus.Revoked && newStatus != OwnershipStatus.Revoked)
            return Error.BusinessRule("Cannot unrevokie", "OWNERSHIP.IRREVERSIBLE");

        Status = newStatus;
        return Result.Success<Unit, Error>(Unit.Value);
    }
}
```

---

## Value Object Template

```csharp
[ValueObject<string>]
public readonly partial struct Address
{
    public static Validation Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Validation.Invalid("Empty");
        if (value.Length < 32)
            return Validation.Invalid("Too short");
        return Validation.Ok;
    }
}

// Usage
var addr = Address.From("A1B2..."); // Validated
addr1 == addr2; // Structural equality
```

---

## Cross-Aggregate Validation Pattern

**Problem**: Aggregate needs to check another aggregate's state.
**Solution**: Function injection (avoid direct reference).

```csharp
// In aggregate
public Result<Unit, Error> LinkWallet(
    WalletOwnership ownership,
    Func<WalletId, AccessMode, Result<bool, Error>> checkExisting)
{
    var exists = checkExisting(ownership.WalletId, AccessMode.Signing);
    if (exists.IsFailure || exists.Value)
        return Error.Conflict("Conflict", "WALLET.EXISTS");

    // ... proceed
}

// In handler
var result = principal.LinkWallet(
    ownership,
    (walletId, mode) => _repo.ExistsAsync(walletId, mode, ct));
```

---

## Command Handler Template

```csharp
public sealed class LinkWalletHandler
    : IRequestHandler<LinkWalletCommand, Result<Unit, Error>>
{
    private readonly IAxonPrincipalRepository _repo;
    private readonly IUnitOfWork _uow;

    public async Task<Result<Unit, Error>> Handle(
        LinkWalletCommand cmd, CancellationToken ct)
    {
        // 1. Load aggregate
        var principal = await _repo.GetByIdAsync(cmd.PrincipalId, ct);
        if (principal is null)
            return Error.NotFound("Principal not found", "PRINCIPAL.NOT_FOUND");

        // 2. Create entity
        var ownership = WalletOwnership.Create(cmd.PrincipalId, cmd.WalletId);

        // 3. Execute domain command
        var result = principal.LinkWallet(
            ownership,
            (wId, mode) => _repo.ExistsAsync(wId, mode, ct));

        if (result.IsFailure) return result.Error;

        // 4. Persist (domain events auto-dispatched)
        await _uow.SaveChangesAsync(ct);

        return Result.Success<Unit, Error>(Unit.Value);
    }
}
```

---

## Domain Event Pattern

```csharp
// Definition
public sealed record WalletLinkedEvent(
    AxonUserId PrincipalId,
    WalletId WalletId) : DomainEvent;

// Raising (in aggregate)
RaiseDomainEvent(new WalletLinkedEvent(Id, walletId));

// Handling
public sealed class WalletLinkedHandler
    : INotificationHandler<DomainEventNotification<WalletLinkedEvent>>
{
    public async Task Handle(
        DomainEventNotification<WalletLinkedEvent> notification,
        CancellationToken ct)
    {
        var evt = notification.DomainEvent;
        // Side effects: logging, cache, integration events
        await _publisher.PublishAsync(new WalletLinkedIntegrationEvent(evt.WalletId), ct);
    }
}
```

---

## Key Rules (Critical for AI)

### ✅ DO
- Private collections with `AsReadOnly()`
- Command methods return `Result<T, Error>`
- No-op guards for idempotency
- Factory methods over constructors
- Function injection for cross-aggregate checks
- `RaiseDomainEvent()` after state changes

### ❌ DON'T
- Public setters on aggregates
- Throw exceptions for business logic
- Direct aggregate-to-aggregate references
- Primitive IDs (use StrongId)
- Anemic models (logic in services)

---

## Related Docs

- [Quick Reference](./00-QUICK-REFERENCE.md) - All patterns in one place
- [CQRS](./cqrs.md) - Command/Query handlers
- [Identity Module](../../modules/identity/01-domain-model.md) - Real examples

---

**Lines**: ~250 (67% reduction from 817)
**Focus**: Copy-paste templates for AI code generation