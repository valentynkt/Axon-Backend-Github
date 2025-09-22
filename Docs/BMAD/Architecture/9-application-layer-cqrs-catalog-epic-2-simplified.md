# 9. Application Layer — CQRS Catalog (Epic 2 Simplified)

**Epic 2 Eliminated Dual Patterns**: Removed `ExchangeTokenCommand`, `GetCurrentUserQuery` and all associated handlers, validators, DTOs. API endpoints route directly to single canonical handlers.

## Commands (Single Pattern)

**`ExchangeCredentialCommand`** *(Only Command - Dual Pattern Removed)*

* **Input:** Normalized `ExchangeUserData` (issuer, subject, provider, wallets, riskTier?) from separated `DynamicAuthService`.
* **Simplified Steps:**
  a) `FindByCredentialAsync` (compiled query) → new or existing Principal
  b) `EnsureManyByChainAndAddressAsync` (batch operation - no N+1)
  c) Link **verified** ownerships (domain invariants enforced)
  d) Apply **verified-first** chain defaults (**idempotent**)
  e) **No-op guards:** Skip writes when values unchanged (ETag preservation)
  f) Persist in **single transaction** with observability tracing
* **Output:** `ExchangeDynamicTokenResponse` (created?, counts)
* **Observability:** Trace correlation ID, metrics (exchange_success/failure, wallets_linked), structured logging
* **Errors → HTTP:** JWT validation → 400/401; wallet conflict → 409; rate limit → 429; domain rules → 422

## Queries (Single Pattern)

**`GetMyPrincipalQuery`** *(Only Query - Dual Pattern Removed)*

* **Input:** Claims (provider, issuer, subject), `If-None-Match` ETag (optional).
* **Simplified Steps:**
  a) `FindByCredentialAsync` (compiled read query) → Principal ID
  b) `GetPrincipalFingerprintAsync(id)` → current ETag hash
  c) **ETag Optimization:** Compare with `If-None-Match` → return **304** if unchanged
  d) If different → `GetByIdWithActiveOwnershipsAsync(id)` (single compiled query)
* **Output:** `CurrentUserResult` + `ETag` header OR **304 Not Modified**
* **Observability:** Metrics (etag_hits/misses), trace correlation, structured logging

## Key Repository Methods

**Core Operations (Implemented):**
```csharp
// Identity resolution (Epic 3)
Task<AxonPrincipal?> FindByCredentialAsync(ProviderType providerType, string issuer, string subject, CancellationToken ct = default);
Task<AxonPrincipal?> FindByWalletIdAsync(WalletId walletId, CancellationToken ct = default);

// Wallet operations (Performance optimized)
Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(IEnumerable<WalletId> walletIds, CancellationToken ct = default);
Task<IReadOnlyDictionary<(NetworkEnvironment networkEnvironment, string chainId, Address address), WalletId>> EnsureManyByChainAndAddressAsync(IEnumerable<(NetworkEnvironment networkEnvironment, string chainId, Address address)> items, CancellationToken ct = default);

// ETag & snapshots
Task<string> GetPrincipalFingerprintAsync(AxonUserId principalId, CancellationToken ct = default);
Task<AxonPrincipal?> GetByIdWithActiveOwnershipsAsync(AxonUserId principalId, CancellationToken ct = default);
```

**Epic 4 Additions (Pending):**
```csharp
// Smart caching support
Task<AxonPrincipal?> FindByDynamicUserIdAsync(string dynamicUserId, CancellationToken ct = default);
```

## Enhanced ICurrentUserService (Epic 4)

Epic 4 significantly enhances the `ICurrentUserService` interface defined in `src/BuildingBlocks/Core/Abstractions/Authentication/ICurrentUserService.cs` to support unified identity resolution across modules.

### Interface Enhancement

**Current Interface (Pre-Epic 4):**
```csharp
public interface ICurrentUserService
{
    string? UserId { get; }              // Dynamic JWT userId
    string? UserName { get; }            // Display name from JWT
    bool IsAuthenticated { get; }        // Authentication status

    string GetUserIdOrDefault(string systemUserId = "SYSTEM");
    string GetCurrentUserIdOrSystem();
}
```

**Epic 4 Enhanced Interface:**
```csharp
public interface ICurrentUserService
{
    // Existing properties (unchanged)
    string? UserId { get; }              // Dynamic JWT userId from claims
    string? UserName { get; }            // Display name from JWT
    bool IsAuthenticated { get; }        // Authentication status

    string GetUserIdOrDefault(string systemUserId = "SYSTEM");
    string GetCurrentUserIdOrSystem();

    // Epic 4 NEW: Async AxonUserId resolution with caching
    Task<AxonUserId?> GetAxonUserIdAsync(CancellationToken ct = default);

    // Epic 4 NEW: Sync cache-only lookup (no database fallback)
    bool TryGetAxonUserId(out AxonUserId axonUserId);
}
```

### Key Behavioral Changes

**Identity Resolution Strategy:**
1. **Dynamic UserId** (existing): JWT `sub` claim from authentication provider
2. **AxonUserId** (new): Internal canonical identity with smart caching

**Caching Behavior:**
- `GetAxonUserIdAsync()`: Progressive cache hierarchy (HttpContext.Items → IMemoryCache → Database)
- `TryGetAxonUserId()`: Cache-only lookup, no database queries
- All calls within same HTTP request return cached value (0ms latency)

### Implementation Location

**HttpContextUserService Enhancement:**
Located at `src/Modules/Identity/Infrastructure/Services/HttpContextUserService.cs`

The existing implementation provides JWT-based authentication. Epic 4 extends it with:
- Dependency injection of `IMemoryCache` and `IAxonPrincipalReadRepository`
- Progressive cache resolution implementing the interface enhancements
- Request-scoped caching via `HttpContext.Items`

### Chat Module Integration Impact

**Current Chat Handler Pattern:**
```csharp
// src/Modules/Chat/Application/Common/Commands/BaseChatCommandHandler.cs
protected UserId GetAuthenticatedUserId()
{
    var userIdString = _currentUserService.UserId!;  // Dynamic JWT userId
    return new UserId(Guid.Parse(userIdString));     // Converts to UserId type
}
```

**Epic 4 Enhanced Pattern:**
```csharp
// Updated pattern for unified identity
protected async Task<AxonUserId> GetAxonUserIdAsync(CancellationToken ct = default)
{
    var axonUserId = await _currentUserService.GetAxonUserIdAsync(ct);
    if (!axonUserId.HasValue)
        throw new UnauthorizedAccessException("AxonUserId not resolved");

    return axonUserId.Value;
}
```

### Backward Compatibility

**No Breaking Changes**: Epic 4 maintains full backward compatibility:
- All existing properties and methods unchanged
- New methods are additive
- Chat module can migrate handlers incrementally
- `DefaultCurrentUserService` in Chat remains functional during transition

### Error Handling Strategy

**Resolution Failure Scenarios:**
1. **No Authentication**: `GetAxonUserIdAsync()` returns `null`
2. **Cache Miss + DB Miss**: Returns `null` (user not yet exchanged)
3. **Database Unavailable**: Throws exception (circuit breaker pattern recommended)

**Graceful Degradation:**
- `TryGetAxonUserId()` returns `false` for any failure
- Chat handlers can fallback to Dynamic userId if needed
- Exchange endpoint always populates cache for subsequent requests

---
