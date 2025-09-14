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

## Repository Contract Additions (Epic 3 Complete Implementation)

```csharp
// Epic 3: Wallet-first resolution methods
Task<AxonPrincipal?> FindByWalletIdAsync(
    WalletId walletId, CancellationToken ct = default);

Task<bool> IsCredentialTakenAsync(
    ProviderType providerType, string issuer, string subject, CancellationToken ct = default);

// Epic 2: Read-side credential resolution (compiled query for performance)
Task<AxonPrincipal?> FindByCredentialAsync(
    ProviderType providerType, string issuer, string subject, CancellationToken ct = default);

// ETag fingerprint generation (deterministic hash for conditional GET)
Task<string> GetPrincipalFingerprintAsync(
    AxonId principalId, CancellationToken ct = default);

// Batch wallet operations (prevents N+1 queries)
Task<IReadOnlyDictionary<(string chainId, Address address), WalletId>>
  EnsureManyByChainAndAddressAsync(IEnumerable<(string chainId, Address address)> items, CancellationToken ct = default);

// Find verified signing owners (for conflict detection) - Epic 2/3 enhanced
Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
    IEnumerable<WalletId> walletIds, CancellationToken ct = default);

// Last-seen updates (does not affect ETag)
Task<int> TouchLastSeenAsync(IEnumerable<WalletId> ids, DateTimeOffset seenAt, CancellationToken ct = default);

// Single query for complete principal snapshot
Task<AxonPrincipal?> GetByIdWithActiveOwnershipsAsync(
    AxonId principalId, CancellationToken ct = default);
```

**Epic 2 Implementation Notes:**

* **All methods use compiled EF Core queries** for optimal performance
* **Batch operations** prevent N+1 query patterns  
* **ETag fingerprint** combines `principal.updated_at` + verified ownership timestamps + chain default timestamps
* **`TouchLastSeenAsync` specifically designed NOT to affect ETag** (excludes `last_seen` from fingerprint calculation)
* **Single round-trip** for complete data retrieval in `GetByIdWithActiveOwnershipsAsync`

---
