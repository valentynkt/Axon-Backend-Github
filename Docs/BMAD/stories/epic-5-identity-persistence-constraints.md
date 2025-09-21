# Epic 5: Identity Persistence & Constraints - PRD Implementation with Brownfield Refactoring

## Epic Overview

### Purpose
Implement the complete Identity Persistence & Constraints system from PRD while refactoring existing brownfield code. Focus on KISS approach (Dynamic + Wallet only) with database-enforced constraints, deterministic resolution, and hardened security - removing obsolete OIDC infrastructure and complex token management.

### Business Value
- **Enterprise B2B Readiness**: Vendor-agnostic identity with guaranteed uniqueness
- **Database-First Security**: Critical constraints enforced at DB level, impossible to bypass
- **Deterministic Resolution**: Predictable identity mapping across all authentication methods
- **Simplified Maintenance**: 20% complexity for 80% value - easier to understand and maintain
- **Compliance Ready**: Audit trails and provable constraint enforcement

## PRD Requirements Mapping

### Core Requirements from PRD
1. **Unified Principal Resolution**: Single identity across Dynamic JWT and wallet authentication
2. **5 Hard Database Constraints**: Environment-scoped uniqueness with database enforcement
3. **Deterministic Tie-Break**: Ranked resolution for identity conflicts
4. **Simple Token Strategy**: 15-minute Axon JWT with hardened validation
5. **Minimal Replay Protection**: KISS approach without complex ledgers

## Critical Implementation Decisions (Must Lock Before Starting)

### 1. Environment Model Decision ✅ LOCKED
**Decision**: Separate `environment` column + `chain_id='solana'`

**Implementation**:
- `chain_id`: Chain identifier only (e.g., 'solana', 'ethereum')
- `environment`: Network identifier (e.g., 'mainnet', 'devnet', 'test')
- Apply consistently to all keys, queries, indexes, and API contracts
- Never infer environment from headers or client - require explicit in payloads

### 2. Exclusivity Transaction Semantics
**Requirement**: Verification runs in single DB transaction
- Lock wallet row first using `SELECT * FROM wallet WHERE id=:wallet_id FOR UPDATE`
- Set candidate to `verified+signing`
- Auto-revoke other ownerships on same wallet
- On unique index race, retry once
- Return **409** only when competing verified+signing exists
- Silent auto-revoke for pending conflicts only

### 3. Deterministic Resolution Rules
**Definition**: Active = `status != revoked`
**Tie-Break Order** (applies ONLY if no verified+signing exists):
1. `dynamic_attested` (highest authority - from Dynamic.xyz)
2. `direct_signature_msg` (wallet signed message)
3. `direct_signature_tx` (on-chain transaction)
4. `watch_only` (lowest authority)
5. Then earliest principal (by created_at)

**Important**: Candidates come from Wallet found by `(environment, chain_id, address)` triple
**Cross-environment attach requires verified+signing on the other environment; watch-only never triggers attach**

### 4. Default Wallet Guard
**Domain Enforcement**: Default requires same principal's `verified+signing` ownership on that `(environment, chain_id)`
- Reject watch-only as default (422 Unprocessable)
- Clear default when ownership revoked for that environment
- Defaults are per `(principal, environment, chain_id)` triple

### 5. Dynamic JWT Hardening
**Requirements**:
- Enforce `iss` validation: `app.dynamicauth.com/{environmentId}`
- Map Dynamic environmentId to our environment deterministically
- Enforce `aud` validation against configured value
- **Enforce both `exp` and `nbf` with ±60s skew**
- Cache JWKS for 10-30 minutes with background refresh
- Support `kid` rotation seamlessly
- Clock skew tolerance: ±60 seconds max
- Reject unknown environmentIds with clear error

### 6. Replay Protection Strategy (Multi-Instance) ✅ LOCKED
**Decision**: Redis cache for `{message,signature}` hash (preferred)

**Implementation**:
- Store hash of `api_key | environment | chain_id | address | signature | message` in Redis with 5-10min TTL
- Signed message MUST embed `environment`, `chain_id`, `address`
- Alternative (if Redis unavailable): In-memory LRU + sticky routing + 2-5min TTL
- Log all replay attempts for security monitoring

### 7. Migration Deduplication Rules
**When splitting encoded values and backfilling**:
- Split any `chain_id='solana:mainnet'` → `chain_id='solana'`, `environment='mainnet'`
- Keep record with oldest `first_seen_at` as survivor
- Re-point all FKs (ownerships, defaults, credentials) to survivor
- Delete duplicate records
- Generate deterministic CSV/JSON report with affected records
- Run in batches with progress logging
- **Pre-migration cross-environment conflict scan**: Before migration, scan for same (chain_id, address) with verified+signing owned by different principals across environments; generate conflict report for manual resolution before proceeding

### 8. Test Coverage Requirements
**Critical Scenarios** (must have explicit tests):
- **Environment Isolation**: Same address on mainnet vs devnet = different Wallet rows
- **Per-Environment Exclusivity**: Two verifies on same env wallet → one winner; different env → both succeed
- **Resolution Determinism**: Identical proof with different environment → different principals
- **Default Guard**: Default on mainnet doesn't affect devnet
- **JWKS Rotation**: Seamless transition from kid1 → kid2
- **Challenge TTL**: Expiry and clock skew handling
- **Query Plans**: EXPLAIN shows index scans for triple-key lookups

### 9. Address Normalization Rules ✅ LOCKED
**Requirement**: Chain-specific normalization before all operations
**Implementation**:
- **Solana**:
  - Validate base58 encoding
  - Length check (32 bytes decoded, 44 chars typical)
  - Trim whitespace
  - Store exactly as canonical base58 string
- **EVM** (when added):
  - Validate 0x prefix + 40 hex chars
  - Convert to lowercase (or EIP-55 checksum consistently)
  - Fixed length validation
- Apply normalization at API edge before ANY database operation
- Apply normalization before computing `(environment, chain_id, address)` keys

### 10. Per-Partner Audience Configuration ✅ LOCKED
**Requirement**: Multi-tenant B2B audience binding
**Implementation**:
- Each API key/partner has audience allowlist
- Allowlists are environment-scoped
- Dynamic JWT `aud` must match partner's allowlist
- Wallet proof `aud` must match partner's identifier
- Configuration stored in partner settings table
- Cache partner config with 5-min TTL

## Current State Analysis (Brownfield)

### Existing Components to Refactor
1. **Missing Environment Isolation**: No environment field in Wallet/Credential entities
2. **Weak Constraints**: Business logic enforcement instead of database constraints
3. **Multiple Resolution Paths**: Inconsistent identity resolution logic
4. **No Exclusive Signing**: Multiple verified owners possible
5. **Missing Replay Protection**: No defense against signature replay

### Code to Remove Completely
1. **OIDC Infrastructure**: All OIDC provider code (keeping Dynamic JWT only)
2. **Refresh Tokens**: Token refresh logic and storage
3. **Session Management**: Complex session tracking
4. **Nonce Ledger**: Database-based nonce tracking (if exists)

## Stories

### Story 5.1: Database Schema Refactoring & Constraint Implementation
**Priority**: P0 (Foundation)
**Type**: Refactoring/Database
**PRD Requirements**: 5 Hard Database Constraints, Environment Isolation

**Acceptance Criteria**:
1. Implement environment model per decision #1 (separate environment column + chain_id='solana')
2. Add 5 named partial unique indexes with CONCURRENTLY creation:
   - `ux_wallet_env_chain_addr`: (environment, chain_id, address) WHERE is_deleted=false
   - `ux_credential_env`: (environment, provider, issuer, subject) WHERE is_deleted=false
   - `ux_ownership_pair`: (principal_id, wallet_id) WHERE is_deleted=false
   - `ux_exclusive_signing`: (wallet_id) WHERE status='verified' AND access_mode='signing'
   - `ux_chain_default`: (principal_id, environment, chain_id) WHERE is_deleted=false
   - `idx_ownership_wallet_active`: (wallet_id) WHERE is_deleted=false
   - `idx_ownership_principal_active`: (principal_id) WHERE is_deleted=false
   - **`idx_wallet_chain_addr_active`: (chain_id, address) WHERE is_deleted=false** (for cross-env lookups)
3. Implement migration deduplication per decision #7 (keep oldest, report conflicts)
4. Validate all indexes are used in query plans (EXPLAIN ANALYZE)
5. Performance: constraint checks <10ms, bulk operations remain efficient
6. Use CREATE UNIQUE INDEX CONCURRENTLY for uniques, ALTER TABLE ADD CONSTRAINT NOT VALID → VALIDATE CONSTRAINT for CHECK constraints
7. EXPLAIN shows index scans when is_deleted=false is in the WHERE clause
8. **All write operations and hot read queries MUST include `is_deleted=false` in WHERE clause** where relevant to ensure partial index usage
9. **All hot queries include `is_deleted=false` so partial indexes match; CI gate runs EXPLAIN and fails on seq scans**

**Technical Details**:
- Use Postgres CONCURRENTLY for zero-downtime index creation
- Implement two-phase constraint validation (NOT VALID → VALIDATE)
- Generate migration rollback scripts
- Log all deduplication actions for audit

### Story 5.2: Remove OIDC & Simplify Auth Infrastructure
**Priority**: P0 (Cleanup)
**Type**: Code Removal/Refactoring
**PRD Requirements**: KISS approach - Dynamic + Wallet only

**Acceptance Criteria**:
1. Remove all OIDC provider code while KEEPING Dynamic JWT validation
2. `/auth/me` accepts **Axon JWT only** (not Dynamic JWT directly)
3. `/auth/exchange` (wallet path) **requires `environment` in payload AND signed message**
4. `/auth/challenge` returns canonical message with `environment`, `chain_id`, `address` embedded
5. Delete nonce database tables if they exist (remain stateless)
6. Remove refresh token infrastructure completely
7. Map Dynamic environmentId to our environment deterministically
8. Reject tokens whose iss environmentId does not map to one of ('mainnet','devnet','test')
9. All tests pass with updated auth flow

**API Contract Clarifications**:
- Wallet proof payload MUST include: `{environment, chain_id, address, signature, message}`
- **Signed message MUST embed**: `{environment, chain_id, address, issued_at, exp, nonce, aud}` where `exp = issued_at + 300s` (≤5 minutes)
- **SDKs MUST sign the canonical template v1** with strict field order: `{environment, chain_id, address, issued_at, exp, nonce, aud}`
- **Hard cap TTL enforcement**: Server rejects any wallet proof where `exp > issued_at + 300s`
- **Server rejects non-canonical encodings** (e.g., different field order, extra fields, missing fields)
- **Publish canonical template in SDK docs** and enforce byte-for-byte match
- **Server validates `aud` against partner/client allowlist** for the API key to prevent cross-app replay
- **Optional `domain` field** for additional binding when applicable
- Canonicalize message bytes server-side (stable JSON or exact string template) and reject mismatched payload vs signature fields
- Reject if environment in payload doesn't match environment in signed message
- **Apply ±60s clock skew tolerance for `nbf`/`exp` validation**
- **Never log raw JWTs or signatures; log only stable hashes and identifiers**

### Story 5.3: Implement Deterministic Principal Resolution
**Priority**: P0 (Core Logic)
**Type**: Refactoring/Implementation
**PRD Requirements**: Unified Principal Resolution, Deterministic Tie-Break

**Acceptance Criteria**:
1. Implement 2-step resolution: credential-first → wallet-fallback → create-new
2. Define "active ownership" as `status != revoked`
3. **Wallet lookup MUST use triple key**: `(environment, chain_id, address)`
4. Tie-break applies **ONLY if no verified+signing exists**:
   - `dynamic_attested` > `direct_signature_msg` > `direct_signature_tx` > `watch_only`
   - Then earliest principal (by created_at)
5. Candidates come from Wallet found by triple, not global search
6. Add resolution path audit trail with environment context
7. Resolution performance <100ms P95
8. Remove all legacy `FindPrincipalBy*` methods
9. Log resolution_path={credential|wallet|created} and auto_revoke reason=conflict_lost with {environment, chain_id, address}
10. IdentityCredential.last_seen_at only updates if newer (monotonic), with a unit test
11. **Cross-environment attach decision tree**: Run credential-first resolution; if resolves to principal **A**, check cross-env verified+signing owner **B** for (chain_id, address): if **A == B** auto-link wallet for current env; if **A != B** return 409 Conflict; if credential-first fails, attach to **B** and create env-scoped wallet row; watch-only elsewhere never blocks attach
12. **Late principal creation with race protection**: Upsert wallet first (INSERT ... ON CONFLICT DO NOTHING), then re-read wallet and re-resolve ownerships. Only create principal if still no candidate
13. **Normalize addresses chain-specifically before all database operations** and key computations
14. **Solana addresses: validate base58, trim whitespace, check length** before storing
15. **Future EVM addresses: lowercase (or EIP-55), validate format** before storing

**Query Requirements**:
- NEVER query ownerships directly by address
- ALWAYS fetch Wallet by `(env, chain_id, address)` first
- Then traverse ownerships by `wallet_id`
- Log triple key in all resolution paths

### Story 5.4: Refactor Wallet Ownership with Transaction Guards
**Priority**: P0 (Domain Logic)
**Type**: Refactoring
**PRD Requirements**: Exclusive Signing, Default Wallet Guard

**Acceptance Criteria**:
1. Verification runs in single database transaction (per decision #2):
   - Set candidate to `verified+signing`
   - Auto-revoke others on same wallet
   - Retry once on unique index race
2. Return 409 only when competing verified+signing exists (not for pending)
3. Implement Default Wallet Guard:
   - Default requires same principal's verified+signing ownership
   - Return 422 for watch-only default attempts
   - Clear default when ownership revoked
4. Allow unlimited watch-only (non-blocking)
5. Add ownership change audit trail
6. **Verification transaction uses `SELECT ... FOR UPDATE`** on competing ownerships before updates
7. **Consistent lock ordering (wallet_id ASC)** prevents deadlocks
8. **Verification acquires a row lock on `wallet`** before updating any ownerships; no `SKIP LOCKED` in competing ownership load
9. **Consistent lock ordering (by `wallet_id` ASC)** across any multi-wallet operations

**Transaction Semantics**:
- **Use explicit DB transactions with row-level locking** for verification
- **Fetch competing ownerships using `SELECT ... FOR UPDATE`** within transaction
- **Lock order: always lock by wallet_id ASC** to prevent deadlocks
- **Partial-unique index remains ultimate guard** with single retry
- Handle unique constraint violations with retry
- Silent auto-revoke for pending/unverified only

### Story 5.5: Hardened JWT Strategy & Replay Protection
**Priority**: P0 (Security)
**Type**: Refactoring/Security
**PRD Requirements**: Simple Token Strategy, Hardened Security

**Acceptance Criteria**:
1. Issue 15-minute Axon JWT for both Dynamic and wallet auth
2. **Implement KMS/HSM-backed JWT signing**:
   - Use KMS/HSM for Axon JWT signing keys (not in-memory)
   - Include `kid` in JWT header for rotation tracking
   - Maintain old+new keys active during rotation (24-48h window)
   - Document key rotation playbook with emergency procedures
   - Pre-configure key rotation schedule (quarterly recommended)
3. Implement Dynamic JWT hardening:
   - Validate `iss`: `app.dynamicauth.com/{environmentId}`
   - Map environmentId → our environment deterministically
   - **Validate `aud` against per-partner allowlist (env-scoped)**
   - Cache JWKS for 10-30 minutes with background refresh
   - Support `kid` rotation seamlessly
   - Clock skew tolerance ±60 seconds max
4. **Hard cap wallet-proof TTL enforcement**:
   - Canonical message v1 MUST set `exp = issued_at + 300s` (≤5 minutes)
   - Enforce `nbf`/`exp` with ±60s skew tolerance
   - Add test to reject wallet proofs with >5m TTL
   - SDK documentation must specify 5-minute maximum
5. **Implement deterministic replay protection**:
   - Define SHA-256 over UTF-8 bytes: `api_key | environment | chain_id | address | signature | message`
   - Encode hash as base64url for Redis key & logs
   - Store in Redis with 5-10min TTL
   - Ensure cross-instance consistency via standardized hashing
   - Fallback: In-memory LRU if Redis unavailable
6. Remove all refresh token code and endpoints
7. Pre-warm JWKS cache on service startup
8. **Define and publish canonical message template v1** with exact field order and JSON formatting
9. **Server validates canonical byte encoding** before signature verification
10. **Wallet signatures include `aud` (partner/client identifier)** in canonical message
11. **Server enforces `aud` against per-API-key allowlist** to prevent cross-app replay
12. **Maintain per-API-key audience allowlist configuration** (environment-scoped)
13. **Log replay attempts with hashed identifiers only** (no raw bytes/tokens)

**Error Messages**:
- "Wallet already verified on {environment}/{chain_id}" for 409s
- "Cross-environment verified+signing conflict: manual resolution required" for cross-env conflicts
- "Unknown Dynamic environmentId: {id}" for unmapped environments
- "Wallet proof TTL exceeds 5-minute maximum" for TTL violations
- "Replay detected for signature on {environment}" for replays
- "Invalid audience '{aud}' for this API key" for cross-app replay attempts

### Story 5.6: Comprehensive Testing & Validation
**Priority**: P1 (Quality)
**Type**: Testing/Refactoring
**PRD Requirements**: Test Coverage for Critical Scenarios

**Acceptance Criteria**:
1. **Environment Isolation Tests**:
   - Same base58 address on mainnet vs devnet = different Wallet rows
   - No index conflicts between environments
   - Same (chain_id, address) across environments → same principal; wallets are env-scoped
2. **Per-Environment Exclusivity**:
   - Two verifies on same `(env, wallet)` → one winner
   - Two verifies on different env wallets → both succeed
3. **Query Performance**:
   - EXPLAIN ANALYZE shows index scans for `(env, chain_id, addr)`
   - EXPLAIN ANALYZE shows index scans for `(env, provider, issuer, subject)`
4. **Default Guard Tests**:
   - Default on mainnet doesn't affect devnet
   - Setting default requires verified+signing on that env
5. **Other Critical Tests**:
   - JWKS rotation: seamless kid1 → kid2 transition
   - Challenge TTL and clock skew handling
   - Replay detection across instances
6. **API Enhancement**:
   - /auth/me returns wallets grouped or tagged by {environment, chain_id} so integrators can render per-network state easily
7. **Query Plan Validation**:
   - Assert all hot paths use index scans via EXPLAIN ANALYZE in CI pipeline
   - CI gate fails if any critical query uses seq scan instead of index scan
8. **Address Normalization Tests**:
   - Address normalization prevents duplicates from format differences
   - Invalid address formats rejected at API edge
9. **Multi-Tenant B2B Security Tests**:
   - Partner A's Dynamic token rejected for Partner B's API key
   - Partner A's wallet signature rejected when `aud` = Partner B
   - Same wallet can be verified by different partners (different aud)
   - Cross-app replay prevented via audience binding
   - **nbf/exp skew acceptance within ±60s**
   - **Namespaced replay keys prevent cross-API-key collisions**
   - **Cross-env attach requires verified+signing (not watch-only)**
   - **EXPLAIN CI gate confirms index scans on all hot paths**

**Test Implementation**:
- Use Testcontainers for real Postgres constraints
- Use Redis test container for replay tests
- Mock Dynamic JWKS endpoint for rotation tests
- Parallel test execution for race conditions

## Rollout Strategy

### Database Migration Approach
1. **Index Creation**: Use CONCURRENTLY for zero-downtime
2. **Constraint Validation**: NOT VALID → VALIDATE pattern
3. **Data Migration**: Batch updates with progress logging
4. **Verification**: EXPLAIN ANALYZE for query plan validation

### Service Deployment
1. **Feature Flags**: Shadow-write new resolution for 24-48h
2. **JWKS Pre-warming**: Fetch keys on service startup
3. **Monitoring**: Track constraint violations and resolution paths
4. **Rollback Ready**: Keep rollback scripts tested and ready

### Performance Monitoring
- Query plan analysis for all identity queries
- Constraint overhead measurement (<10ms target)
- Resolution latency tracking (<100ms P95)
- **Track JWKS cache hit rate (target >95%)**
- **Monitor audience-mismatch count per partner (alert on spikes)**
- **Log format: Always include `{environment, chain_id, address}` + hashed token/sig IDs (never raw)**

## Technical Implementation Details

### Database Constraints SQL
```sql
-- Named indexes for clear error messages
CREATE UNIQUE INDEX CONCURRENTLY ux_wallet_env_chain_addr
  ON identity.wallet (environment, chain_id, address)
  WHERE is_deleted = false;

CREATE UNIQUE INDEX CONCURRENTLY ux_credential_env
  ON identity.credential (environment, provider, issuer, subject)
  WHERE is_deleted = false;

CREATE UNIQUE INDEX CONCURRENTLY ux_ownership_pair
  ON identity.wallet_ownership (principal_id, wallet_id)
  WHERE is_deleted = false;

CREATE UNIQUE INDEX CONCURRENTLY ux_exclusive_signing
  ON identity.wallet_ownership (wallet_id)
  WHERE status = 'verified' AND access_mode = 'signing' AND is_deleted = false;

CREATE UNIQUE INDEX CONCURRENTLY ux_chain_default
  ON identity.principal_chain_default (principal_id, environment, chain_id)
  WHERE is_deleted = false;

-- CHECK constraints for valid values
ALTER TABLE identity.wallet
  ADD CONSTRAINT check_wallet_environment
  CHECK (environment IN ('mainnet','devnet','test'));

ALTER TABLE identity.wallet
  ADD CONSTRAINT check_wallet_chain
  CHECK (chain_id IN ('solana','ethereum'));

-- CHECK constraint for credential environment
ALTER TABLE identity.credential
  ADD CONSTRAINT check_credential_environment
  CHECK (environment IN ('mainnet','devnet','test'));

-- Read-path optimization indexes
CREATE INDEX CONCURRENTLY idx_ownership_wallet_active
  ON identity.wallet_ownership (wallet_id)
  WHERE is_deleted = false;

CREATE INDEX CONCURRENTLY idx_ownership_principal_active
  ON identity.wallet_ownership (principal_id)
  WHERE is_deleted = false;

-- Cross-environment lookup index for FindVerifiedSigningOwnershipAcrossEnvironments
CREATE INDEX CONCURRENTLY idx_wallet_chain_addr_active
  ON identity.wallet (chain_id, address)
  WHERE is_deleted = false;
```

### Resolution Algorithm Pseudocode
```csharp
// Step 1: Credential match (already env-scoped)
var principalA = await FindByCredential(env, provider, issuer, subject);
if (principalA != null)
{
    // Credential resolved to principal A - check cross-env conflict
    var crossEnvOwnership = await FindVerifiedSigningOwnershipAcrossEnvironments(chainId, address);
    if (crossEnvOwnership != null)
    {
        var principalB = crossEnvOwnership.Principal;
        if (principalA.Id == principalB.Id)
        {
            // A == B: Same principal - auto-link wallet for current env
            await CreateWalletForEnvironment(principalA.Id, environment, chainId, address);
            return principalA;
        }
        else
        {
            // A != B: Different principals - 409 Conflict
            return Result.Error(ConflictError("Cross-environment verified+signing conflict: manual resolution required"));
        }
    }

    // No cross-env owner, proceed normally with credential-resolved principal
    return principalA;
}

// Step 2: Wallet match with tie-break (MUST use triple key)
var wallet = await FindWallet(environment, chainId, address);
if (wallet == null)
{
    // Credential-first failed - check for cross-env attach to existing verified owner
    var crossEnvOwnership = await FindVerifiedSigningOwnershipAcrossEnvironments(chainId, address);
    if (crossEnvOwnership != null)
    {
        // Attach to B (existing verified+signing owner) and create env-scoped wallet
        await CreateWalletForEnvironment(crossEnvOwnership.PrincipalId, environment, chainId, address);
        return crossEnvOwnership.Principal;
    }

    // Upsert wallet first to prevent race conditions
    await UpsertWallet(environment, chainId, address); // INSERT ... ON CONFLICT DO NOTHING
    wallet = await FindWallet(environment, chainId, address);

    // Check if another request already linked ownership during race
    var raceOwnerships = await FindActiveOwnerships(wallet.Id);
    if (raceOwnerships.Any())
    {
        // Another request beat us, use their principal
        return ResolveFromOwnerships(raceOwnerships);
    }

    // Still no owner, safe to create principal
    return await CreatePrincipal();
}

var ownerships = await FindActiveOwnerships(wallet.Id);
if (ownerships.Any(o => o.Status == "verified" && o.AccessMode == "signing"))
{
    // Verified+signing exists, use it
    return ownerships.First(o => o.Status == "verified").Principal;
}

// Apply tie-break ONLY when no verified+signing (applies ONLY if no verified+signing exists)
if (ownerships.Any())
{
    return ownerships
        .OrderBy(o => GetAuthorityRank(o.VerificationSource))
        .ThenBy(o => o.Principal.CreatedAt)
        .First().Principal;
}

// Step 3: Create new principal (should rarely happen after race protection)
return await CreatePrincipal();

private int GetAuthorityRank(string source) => source switch
{
    "dynamic_attested" => 1,
    "direct_signature_msg" => 2,
    "direct_signature_tx" => 3,
    "watch_only" => 4,
    _ => 99
};
```

### Verification Transaction Pseudocode (Row-Level Locking)
```csharp
// Wallet verification with explicit locking for exclusivity
public async Task<Result<Principal>> VerifyWalletOwnership(Guid walletId, Guid principalId)
{
    using (var tx = await BeginTransactionAsync())
    {
        try
        {
            // Step 1: Lock the wallet row first to prevent split views
            var wallet = await db.Wallets
                .Where(w => w.Id == walletId)
                .ForUpdate()
                .SingleAsync();

            // Step 2: Lock competing ownerships (consistent order prevents deadlocks)
            var existingOwnerships = await db.WalletOwnerships
                .Where(wo => wo.WalletId == walletId && !wo.IsDeleted)
                .OrderBy(wo => wo.Id) // Consistent lock order: wallet_id ASC
                .ForUpdate() // Row-level lock, no SKIP LOCKED for competing ownership load
                .ToListAsync();

            // Step 3: Check for existing verified+signing (business logic check)
            var verifiedSigning = existingOwnerships
                .FirstOrDefault(o => o.Status == "verified" && o.AccessMode == "signing");

            if (verifiedSigning != null && verifiedSigning.PrincipalId != principalId)
            {
                await tx.RollbackAsync();
                return Result.Error(ConflictError("Wallet already verified on {environment}/{chain_id}"));
            }

            // Step 4: Safe to proceed - set candidate to verified+signing
            var candidateOwnership = existingOwnerships
                .FirstOrDefault(o => o.PrincipalId == principalId);

            if (candidateOwnership != null)
            {
                candidateOwnership.Status = "verified";
                candidateOwnership.AccessMode = "signing";
                candidateOwnership.VerifiedAt = DateTime.UtcNow;
            }

            // Step 5: Auto-revoke others on same wallet (silent for pending only)
            foreach (var other in existingOwnerships.Where(o => o.Id != candidateOwnership?.Id))
            {
                if (other.Status == "pending" || other.Status == "unverified")
                {
                    other.Status = "revoked"; // Silent auto-revoke
                    other.RevokedAt = DateTime.UtcNow;
                    other.RevokeReason = "conflict_lost";
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Result.Success(candidateOwnership.Principal);
        }
        catch (UniqueConstraintViolationException)
        {
            // Retry once on constraint race
            await tx.RollbackAsync();
            return await VerifyWalletOwnership(walletId, principalId);
        }
    }
}
```

## Definition of Done

### Must Complete (P0)
- [ ] All 5 database constraints enforced with named indexes
- [ ] Environment isolation implemented and tested
- [ ] Deterministic resolution with tie-breaking active
- [ ] Transaction-scoped exclusivity with retry logic
- [ ] Default wallet guard enforced at domain level
- [ ] **KMS/HSM-backed JWT signing with kid rotation support**
- [ ] **Cross-environment verified-owner conflict gate (409 response)**
- [ ] **Hard cap wallet-proof TTL (≤5 minutes) enforcement**
- [ ] Dynamic JWT hardening (iss/aud/JWKS) complete
- [ ] **Deterministic replay protection (SHA-256 base64url hashing)**
- [ ] Critical scenario tests passing

### Should Complete (P1)
- [ ] OIDC code removed (keeping Dynamic JWT)
- [ ] Refresh tokens eliminated
- [ ] **Pre-migration cross-environment conflict scan & report**
- [ ] **Key rotation playbook documented**
- [ ] Migration deduplication report generated
- [ ] Performance targets met (<100ms resolution)
- [ ] Audit trail for all identity changes
- [ ] Documentation updated

### Success Metrics
- **Zero** constraint violations possible at DB level
- **100%** deterministic resolution outcomes
- **<100ms** P95 resolution latency
- **<10ms** constraint check overhead
- **Zero** identity fragmentation incidents