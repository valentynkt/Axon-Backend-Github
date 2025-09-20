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
**Decision**: Separate `environment` column + `chain_id='solana'` (no double-encoding)

**Implementation**:
- `chain_id`: Chain identifier only (e.g., 'solana', 'ethereum')
- `environment`: Network identifier (e.g., 'mainnet', 'devnet', 'test')
- Apply consistently to all keys, queries, indexes, and API contracts
- Never infer environment from headers or client - require explicit in payloads

### 2. Exclusivity Transaction Semantics
**Requirement**: Verification runs in single DB transaction
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

### 4. Default Wallet Guard
**Domain Enforcement**: Default requires same principal's `verified+signing` ownership on that `(environment, chain_id)`
- Reject watch-only as default (422 Unprocessable)
- Clear default when ownership revoked for that environment
- Defaults are per `(principal, environment, chain_id)` triple

### 5. Dynamic JWT Hardening
**Requirements**:
- Enforce `iss` validation: `app.dynamicauth.com/{environmentId}`
- Enforce `aud` validation against configured value
- Cache JWKS for 10-30 minutes
- Support `kid` rotation
- Clock skew tolerance: ±60 seconds max

### 6. Replay Protection Strategy (Multi-Instance)
**Options**:
- **Option A**: In-memory LRU per instance + sticky routing + very short TTL (2-5min)
- **Option B**: Shared Redis cache for `{message,signature}` hash (5-10min)

**Decision**: [TO BE DECIDED]

### 7. Migration Deduplication Rules
**When backfilling environment and normalizing chain_id**:
- Keep record with oldest `first_seen_at`
- Move all references to survivor
- Delete duplicates
- Generate deduplication report

### 8. Test Coverage Requirements
**Critical Scenarios** (must have explicit tests):
- Exclusivity race: concurrent verifications → one winner
- JWKS key rotation: `kid1` → `kid2`
- Challenge TTL and clock skew handling
- Default clearing on revoke
- Environment isolation verification

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
1. Implement environment model per decision #1 (separate column or encoded in chain_id)
2. Add 5 named partial unique indexes with CONCURRENTLY creation:
   - `ux_wallet_env_chain_addr`: (environment, chain, address) WHERE is_deleted=false
   - `ux_credential_env`: (environment, provider, issuer, subject) WHERE is_deleted=false
   - `ux_ownership_pair`: (principal_id, wallet_id) WHERE is_deleted=false
   - `ux_exclusive_signing`: (wallet_id) WHERE status='verified' AND access='signing'
   - `ux_chain_default`: (principal_id, environment, chain) WHERE is_deleted=false
3. Implement migration deduplication per decision #7 (keep oldest, report conflicts)
4. Validate all indexes are used in query plans (EXPLAIN ANALYZE)
5. Performance: constraint checks <10ms, bulk operations remain efficient
6. Create NOT VALID constraints first, then VALIDATE after backfill

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
2. `/auth/me` accepts Axon JWT only (not Dynamic JWT directly)
3. Delete nonce database tables if they exist
4. `/auth/challenge` remains stateless (no DB persistence)
5. Remove refresh token infrastructure completely
6. Update auth pipeline: Dynamic JWT → Axon JWT exchange only
7. All tests pass with updated auth flow

**Important Clarifications**:
- Dynamic JWT validation stays (it's not OIDC)
- We're removing generic OIDC support, not Dynamic.xyz support
- Axon JWT is the only token accepted by protected endpoints

### Story 5.3: Implement Deterministic Principal Resolution
**Priority**: P0 (Core Logic)
**Type**: Refactoring/Implementation
**PRD Requirements**: Unified Principal Resolution, Deterministic Tie-Break

**Acceptance Criteria**:
1. Implement 2-step resolution: credential-first → wallet-fallback → create-new
2. Define "active ownership" as `status != revoked` (per decision #3)
3. Implement tie-break ranking when multiple active owners:
   - `dynamic_verified` > `direct_signature_msg` > `direct_signature_tx` > `watch_only`
   - Then earliest principal (by created_at)
4. Add resolution path audit trail for compliance
5. Environment isolation enforced in all queries
6. Resolution performance <100ms P95
7. Remove all legacy `FindPrincipalBy*` methods

**Implementation Details**:
- Single `IPrincipalResolver` service replaces multiple methods
- Resolution path logged for every authentication
- Deterministic results - same input always yields same principal

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

**Transaction Semantics**:
- Use explicit DB transactions for verification
- Handle unique constraint violations with retry
- Silent auto-revoke for pending/unverified only

### Story 5.5: Hardened JWT Strategy & Replay Protection
**Priority**: P0 (Security)
**Type**: Refactoring/Security
**PRD Requirements**: Simple Token Strategy, Hardened Security

**Acceptance Criteria**:
1. Issue 15-minute Axon JWT for both Dynamic and wallet auth
2. Implement Dynamic JWT hardening (per decision #5):
   - Validate `iss`: `app.dynamicauth.com/{environmentId}`
   - Validate `aud` against configured value
   - Cache JWKS for 10-30 minutes
   - Support `kid` rotation
   - Clock skew tolerance ±60 seconds max
3. Implement replay protection (per decision #6):
   - For wallet signatures: 5-min max in payload
   - Choose: in-memory LRU + sticky routing OR Redis cache
   - Test multi-instance scenarios
4. Remove all refresh token code and endpoints
5. Pre-warm JWKS cache on service startup

**Security Hardening**:
- Log all validation failures with detail
- Monitor JWKS rotation events
- Alert on replay attempts

### Story 5.6: Comprehensive Testing & Validation
**Priority**: P1 (Quality)
**Type**: Testing/Refactoring
**PRD Requirements**: Test Coverage for Critical Scenarios

**Acceptance Criteria**:
1. Add critical scenario tests (per decision #8):
   - **Exclusivity race**: Two concurrent verifications → one winner
   - **JWKS rotation**: Transition from kid1 → kid2
   - **Challenge TTL**: Expiry and clock skew handling
   - **Default guard**: Reject watch-only as default
   - **Environment isolation**: No cross-environment leaks
2. Add constraint violation tests for all 5 database constraints
3. Add deterministic resolution tests with all tie-break scenarios
4. Remove obsolete tests (OIDC, refresh tokens, sessions)
5. Focus on Tier-A scenarios over coverage percentage

**Test Categories**:
- **Concurrency Tests**: Race conditions and deadlocks
- **Security Tests**: Replay attacks, token validation
- **Constraint Tests**: Database-level enforcement
- **Performance Tests**: Resolution latency, constraint overhead

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
- JWKS cache hit rates

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

CREATE UNIQUE INDEX CONCURRENTLY ux_exclusive_signing
  ON identity.wallet_ownership (wallet_id)
  WHERE status = 'verified' AND access_mode = 'signing' AND is_deleted = false;
```

### Resolution Algorithm Pseudocode
```csharp
// Step 1: Credential match
var principal = await FindByCredential(env, provider, issuer, subject);
if (principal != null) return principal;

// Step 2: Wallet match with tie-break
var ownerships = await FindActiveOwnerships(env, chain, address);
if (ownerships.Any())
{
    return ownerships
        .OrderBy(o => GetAuthorityRank(o.VerificationSource))
        .ThenBy(o => o.Principal.CreatedAt)
        .First().Principal;
}

// Step 3: Create new principal
return await CreatePrincipal();
```

## Definition of Done

### Must Complete (P0)
- [ ] All 5 database constraints enforced with named indexes
- [ ] Environment isolation implemented and tested
- [ ] Deterministic resolution with tie-breaking active
- [ ] Transaction-scoped exclusivity with retry logic
- [ ] Default wallet guard enforced at domain level
- [ ] Dynamic JWT hardening (iss/aud/JWKS) complete
- [ ] Replay protection strategy implemented
- [ ] Critical scenario tests passing

### Should Complete (P1)
- [ ] OIDC code removed (keeping Dynamic JWT)
- [ ] Refresh tokens eliminated
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