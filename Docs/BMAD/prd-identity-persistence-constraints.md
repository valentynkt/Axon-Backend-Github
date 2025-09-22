# Product Requirements Document: Identity Persistence & Constraints System (KISS • 20/80)

**Document Version**: 1.1-final
**Created**: 2025-01-20
**Last Updated**: 2025-01-20
**Scope**: Dynamic + Wallet Authentication Only (No OIDC, No Complex Features)

---

## 🎯 Executive Summary

### TL;DR
Ship a **KISS, 20/80** identity fabric for **Dynamic + Wallet** only. Database enforces what matters: unique Wallet per `(network_environment, chain, address)`, unique Credential per `(provider, issuer, subject)`, one live Ownership per `(principal, wallet)`, and ≤1 verified+signing owner per wallet. Credential-first → wallet-control resolution ensures users map to the **same AxonPrincipal** across apps. Mint short-lived Axon JWT (~15m) for both flows. **No refresh tokens, no nonce ledger, no OIDC, no overengineering.**

### ✅ 6 Critical KISS Alignments
1. **No OIDC**: Dynamic JWT + Wallet signatures only (removed all OIDC references)
2. **Single NetworkEnvironment Model**: Separate `network_environment` field for Wallets only + simple `chain` values (no double-encoding)
3. **Deterministic Tie-Break**: Ranked resolution (dynamic_verified > direct_signature > watch_only) → earliest principal
4. **Default Wallet Guard**: Only `verified+signing` wallets from same principal can be defaults
5. **Hardened JWT**: Enforce `iss`/`aud` validation, JWKS cache (10-30min), `kid` rotation
6. **Minimal Replay Protection**: 5-min expiry in signed payload + in-memory LRU (no DB ledger)

### Business Problem
Axon needs a minimal, vendor-agnostic identity core where any accepted proof (Dynamic JWT or wallet signature) deterministically resolves to one AxonPrincipal across all B2B clients and apps (web/mobile/dApp).

**Current State**: Multiple authentication paths can create duplicate identities, network environment isolation is incomplete, and wallet ownership constraints lack database-level enforcement.

### Solution Overview (KISS Approach)
Implement a **simplified** Identity Persistence & Constraints system focused on the 20% of features that deliver 80% of value.

**Core Components (Minimal Viable Set)**:
1. **Unified Principal Resolution** - Single identity across Dynamic + wallet authentication only
2. **Database-Level Constraints** - 5 critical invariants enforced at DB level
3. **Deterministic Resolution** - Simple 2-step algorithm (credential → wallet)
4. **Token Strategy** - Short-lived JWT (~15 minutes), no refresh complexity

### Business Value (20/80 Principle)
- **Simplicity First**: Minimal surface area = fewer bugs, easier maintenance
- **Database-First Security**: Critical invariants enforced at DB level, not just code
- **Future-Proof Foundation**: Simple contracts enable evolution without breaking changes
- **Fast Time-to-Market**: Ship core identity in days, not months

---

## 🏢 Business Context

### Why This Matters for Axon
Axon needs a rock-solid identity foundation that works TODAY. Start with Dynamic + Wallet authentication (covers 95% of Web3 use cases), enforce critical constraints at the database level, and keep the door open for future extensions without overbuilding.

**Current Reality**: Identity fragmentation risk threatens adoption. Security constraints are business-logic enforced rather than database-guaranteed.

### User Impact Examples
- **Web3 Developer**: "I authenticate with Dynamic in your web app and wallet in the CLI - am I the same user?"
- **Security Auditor**: "How do you guarantee network environment isolation at the database level?"
- **B2B Client**: "Can we trust that wallet ownership is exclusive?"

### Business Constraints (KISS Focus)
1. **Database-First Security**: Hard constraints at DB level, not application logic
2. **NetworkEnvironment Safety**: Absolute separation between mainnet/devnet/test
3. **Two Auth Methods Only**: Dynamic JWT + Wallet signatures (covers 95% of cases)
4. **No Overengineering**: No refresh tokens, no nonce ledger, no OIDC (for now)

---

## 🔗 Business-to-Technical Solution Mapping

### KISS Implementation: 20% Effort → 80% Value

| Business Problem | KISS Solution | Implementation |
|-------------------|---------------|----------------|
| **Identity Fragmentation** | 2-step resolution | Credential-first, wallet-fallback |
| "Dynamic vs Wallet = 2 users" | One AxonPrincipal | Deterministic matching algorithm |
| **Wallet Conflicts** | DB constraint | Partial-unique: ≤1 verified+signing owner |
| "Who owns this wallet?" | Exclusivity rule | Database enforces single ownership |
| **NetworkEnvironment Leaks** | Natural keys | Include network_environment in wallet unique indexes |
| "Mainnet/devnet collision" | Hard separation | `(network_env, chain, address)` uniqueness |
| **Token Complexity** | Simple JWT | 15-minute lifespan, no refresh |
| "Complex token management" | KISS approach | Re-exchange (Dynamic) or re-prompt (wallet) |

### Expected Outcomes (KISS Benefits)
- **Simplicity**: 80% of value with 20% of complexity
- **Security**: Database-enforced constraints that cannot be bypassed
- **Speed**: Ship in days, not weeks
- **Maintainability**: Simple enough for any developer to understand

---

## 🎯 Business Objectives

### Primary Business Goals

#### 1. **Enable Enterprise B2B Scaling** (Priority: P0)
**Objective**: Support vendor-agnostic identity integration for enterprise clients
**Success Criteria**:
- Single identity per user across Dynamic JWT and wallet authentication
- Support for Dynamic.xyz and wallet signature authentication only
- Zero identity conflicts during client onboarding
- Deterministic principal resolution with <100ms latency

**Business Value**: Unlocks enterprise B2B market with confidence in identity management

#### 2. **Achieve Database-Level Security** (Priority: P0)
**Objective**: Guarantee identity constraints at the database level, not just business logic
**Success Criteria**:
- NetworkEnvironment isolation enforced by database constraints
- Wallet exclusivity guaranteed by unique indexes
- Credential uniqueness enforced (provider-scoped, not network-scoped)
- Zero possibility of constraint bypass through API or direct access

**Business Value**: Security assurance required for enterprise customer confidence

#### 3. **Ensure Deterministic Identity Resolution** (Priority: P0)
**Objective**: Provide predictable, auditable identity resolution for compliance
**Success Criteria**:
- Documented resolution algorithm with deterministic outcomes
- Identity change audit trail (resolution path, auto-revokes, default changes)
- Conflict resolution with clear business rules
- <100ms principal resolution for authenticated users

**Business Value**: Regulatory compliance and enterprise integration confidence

### Secondary Business Goals

#### 4. **Maintain System Performance** (Priority: P1)
**Objective**: Implement constraints without degrading system performance
**Success Criteria**:
- Principal resolution latency remains <100ms P95
- Database constraint checks add <10ms to operations
- Bulk operations remain efficient with proper indexing
- Memory usage for constraint enforcement <5% increase

**Business Value**: Maintains competitive performance while adding enterprise features

---

## 👥 User Personas & Use Cases

### Primary Persona: Enterprise Integration Engineer "Taylor"
**Profile**: Integrating Axon identity with existing enterprise OIDC infrastructure
**Current Pain**: Uncertainty about identity conflicts between OIDC and wallet authentication
**Desired Outcome**: Guaranteed single identity per user with clear conflict resolution
**Business Impact**: Successful enterprise client onboarding and expansion

### Secondary Persona: Security Auditor "Morgan"
**Profile**: Evaluating Axon's identity security for enterprise compliance
**Current Pain**: Business-logic constraints don't provide sufficient security guarantees
**Desired Outcome**: Database-level proof of identity isolation and constraint enforcement
**Business Impact**: Security approval for enterprise deals

### Internal Persona: Identity Service Developer "Jordan"
**Profile**: Building features that depend on identity resolution
**Current Pain**: Uncertainty about edge cases in identity merging and conflicts
**Desired Outcome**: Deterministic APIs with predictable error handling
**Business Impact**: Faster feature development with confidence in identity system

---

## 🔧 Functional Requirements (KISS Version)

### Core Features (The Essential 20%)

#### F1: **Simple Principal Resolution (2 Steps)**
**Business Requirement**: One identity per user across Dynamic + Wallet auth
**Implementation**:
1. **Credential Match**: If `(provider, issuer, subject)` exists → use that principal
2. **Wallet Match**: Else check wallet ownership (prefer verified+signing)
3. **Create New**: Only if no matches found

**Why It Matters**: Users stay unified whether they use Dynamic or wallet auth

#### F2: **5 Hard Database Constraints with Guards**
**Business Requirement**: Database-enforced identity rules
**The 5 Constraints**:
1. Unique Wallet: `(network_environment, chain_id, address)` WHERE `is_deleted=false`
2. Unique Credential: `(provider, issuer, subject)` WHERE `is_deleted=false`
3. One Ownership: `(principal_id, wallet_id)` WHERE `is_deleted=false`
4. Exclusive Signing: `(wallet_id)` WHERE `status='verified' AND access_mode='signing'`
5. One Default: `(principal_id, network_environment, chain_id)` WHERE `is_deleted=false`

**Business Guards (Domain-Level Enforcement)**:
- **Default Wallet Guard**: `SetDefault(principal, wallet)` requires:
  - Same principal owns the wallet (`principal_id` match)
  - Wallet ownership has `status='verified' AND access_mode='signing'`
  - Returns `422 Unprocessable` if wallet is watch-only or pending
- **Monotonic Updates**: `last_seen_at` only updates if `new_timestamp > current_timestamp`

**Why It Matters**: Database + domain guards prevent all violations

#### F3: **Simple Token Strategy with Hardened Security**
**Business Requirement**: Secure authentication without complexity
**Implementation**:
- Issue 15-minute Axon JWT for both Dynamic and wallet auth
- **Dynamic JWT Validation Requirements**:
  - Validate `iss` matches: `app.dynamicauth.com/{environmentId}`
  - Map environmentId deterministically to our network_environment ('mainnet','devnet','test')
  - Reject tokens whose iss environmentId does not map to one of ('mainnet','devnet','test')
  - Validate `aud` matches expected value (document in config)
  - Cache JWKS for 10-30 minutes with `kid` rotation support
  - Clock skew tolerance: ±60 seconds max
- **Wallet Proof Replay Resistance (KISS)**:
  - Signed payload must include: `{issued_at, exp (≤5min), nonce, address, chain_id, network_environment, aud}`
  - In-memory LRU cache rejects duplicate `{message, signature}` pairs for 5-10 min
  - No database ledger or complex nonce tracking
  - Server canonicalizes message bytes (stable JSON template) and validates payload vs signature fields
  - Server validates `aud` against per-partner allowlist to prevent cross-app replay
- Dynamic: Silent re-exchange before expiry
- Wallet: Re-prompt on expiry with fresh nonce
- No refresh tokens, no session management

**Why It Matters**: Simple + secure = maintainable with replay protection

#### F4: **Minimal API Surface with Clear Errors**
**Business Requirement**: Two endpoints handle everything
**Endpoints**:
1. `POST /auth/exchange`: Validate proof → resolve principal → return JWT
   - **409 Conflict**: When competing verified+signing owner exists
   - **Auto-revoke**: Silent for pending/unverified conflicts
2. `GET /auth/me`: Return current identity state (Axon JWT only)
3. *(Optional)* `POST /auth/challenge`: Generate stateless sign-in message

**Why It Matters**: Smaller surface = fewer bugs = clear DX

### Non-Functional Requirements (KISS Targets)

#### Performance
- **Principal Resolution**: <100ms P95 (including DB + JWT)
- **Constraint Checks**: <10ms overhead
- **Codebase**: <500 lines for core logic

#### Security
- **Environment Isolation**: Database-enforced
- **Constraints**: Cannot be bypassed
- **Tokens**: 15-minute expiry, no refresh complexity

#### Simplicity
- **API Surface**: 2 endpoints
- **Auth Methods**: 2 (Dynamic + Wallet)
- **Database Rules**: 5 constraints
- **Time to Ship**: 7 days

---

## 📊 Success Metrics

### Before vs After Identity Management

| Metric | Current State | Target State | Improvement |
|--------|---------------|--------------|------------|
| **Identity Conflicts** | Possible via API bypass | Database-prevented | 100% elimination |
| **NetworkEnvironment Isolation** | Business-logic enforced | Database-guaranteed | Security assurance |
| **Resolution Determinism** | Varies by implementation | Documented algorithm | Predictable outcomes |
| **Enterprise Readiness** | Single-tenant focused | Multi-tenant ready | B2B market enablement |

### Key Success Indicators
1. **Security**: Zero identity constraint violations at database level
2. **Performance**: Principal resolution maintains <100ms P95 latency
3. **Compliance**: Identity change audit trail (resolution path, auto-revokes, default changes)
4. **Integration**: Successful enterprise client onboarding with identity confidence

---

## 🗓️ Implementation Roadmap (KISS Sprint)

### Phase 1: Database Constraints (2 days)
**Goal**: Add the 5 critical database constraints
**Tasks**:
- Add network_environment column to Wallet tables only
- Create 5 unique indexes with proper WHERE clauses
- Test constraint enforcement

**Deliverable**: Database prevents identity violations

### Phase 2: Resolution Algorithm (3 days)
**Goal**: Implement 2-step principal resolution with deterministic tie-break
**Tasks**:
- Credential-match logic (Dynamic JWT)
- Wallet-match with tie-break ranking (dynamic_verified > direct_signature > watch_only)
- Default wallet guard enforcement in domain commands
- New principal creation with proper environment tagging

**Deliverable**: Unified identity across auth methods

### Phase 3: JWT & API (2 days)
**Goal**: Simple token management
**Tasks**:
- 15-minute JWT generation
- `/auth/exchange` endpoint
- `/auth/me` endpoint

**Deliverable**: Complete auth flow

### Total Timeline: 7 days
**Prerequisites**: Migration rehearsal for environment backfill
**Why So Fast?**: KISS approach eliminates 70% of complexity

---

## 🚨 Risk Management

### Primary Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Performance degradation from constraints** | User experience | Optimize indexes, batch operations, performance testing |
| **Data migration complexity** | System stability | Phased migration with rollback plans |
| **Edge case resolution conflicts** | Identity accuracy | Comprehensive testing matrix, manual resolution procedures |
| **Enterprise integration complexity** | Client onboarding | Clear documentation, reference implementations |

### Rollback Strategy
Each phase includes database migration rollback scripts. Principal merge operations maintain complete audit trails for reversal if needed. Performance monitoring ensures early detection of constraint overhead.

---

## 🏁 Definition of Success

### Must Achieve
- ✅ Database-level network environment isolation with zero bypass possibilities
- ✅ Unified identity resolution across Dynamic, wallet, and OIDC authentication
- ✅ Deterministic principal resolution with <100ms latency
- ✅ Identity change audit trail for resolution paths and ownership changes

### Measures Success
- **Enterprise Confidence**: Security auditors approve database-level constraints
- **Developer Experience**: Clear, predictable identity APIs with excellent error messages
- **Business Enablement**: Successful B2B client onboarding with vendor-agnostic identity
- **System Reliability**: Zero identity conflicts and predictable performance

---

## 📋 Technical Implementation (KISS Code)

### The 5 Database Constraints
```sql
-- 1. Unique Wallet
CREATE UNIQUE INDEX ux_wallet ON wallet (network_environment, chain_id, address)
WHERE is_deleted = false;

-- 2. Unique Credential
CREATE UNIQUE INDEX ux_credential ON credential (provider, issuer, subject)
WHERE is_deleted = false;

-- 3. One Ownership per Pair
CREATE UNIQUE INDEX ux_ownership_pair ON wallet_ownership (principal_id, wallet_id)
WHERE is_deleted = false;

-- 4. Exclusive Signing
CREATE UNIQUE INDEX ux_exclusive ON wallet_ownership (wallet_id)
WHERE status = 'verified' AND access_mode = 'signing' AND is_deleted = false;

-- 5. One Default per Chain
CREATE UNIQUE INDEX ux_default ON principal_chain_default (principal_id, network_environment, chain_id)
WHERE is_deleted = false;
```

### 2-Step Resolution with Deterministic Tie-Break
```csharp
// Step 1: Credential match
var principal = await FindByCredential(provider, issuer, subject);
if (principal != null) return principal;

// Step 2: Wallet match with tie-break
var wallet = await FindWallet(network_env, chain_id, address);
if (wallet == null)
{
    // Check cross-environment attach rule first
    var crossEnvPrincipal = await FindPrincipalAcrossEnvironments(chain_id, address);
    if (crossEnvPrincipal != null)
    {
        // Create wallet for this network environment and link to existing principal
        await CreateWalletForNetworkEnvironment(crossEnvPrincipal.Id, network_env, chain_id, address);
        return crossEnvPrincipal;
    }

    // Upsert wallet first to prevent race conditions
    await UpsertWallet(network_env, chain_id, address); // INSERT ... ON CONFLICT DO NOTHING
    wallet = await FindWallet(network_env, chain_id, address);

    // Check if another request already linked ownership during race
    var raceOwnerships = await FindActiveOwnerships(wallet.Id);
    if (raceOwnerships.Any())
    {
        return ResolveFromOwnerships(raceOwnerships);
    }

    // Still no owner, safe to create principal
    return await CreatePrincipal();
}

var ownerships = await FindActiveOwnerships(wallet.Id);
if (ownerships.Any())
{
    // Apply deterministic tie-break ranking (applies ONLY if no verified+signing exists)
    return ownerships
        .OrderBy(o => GetAuthorityRank(o.VerificationSource)) // dynamic_verified > direct_signature_msg > etc.
        .ThenBy(o => o.Principal.CreatedAt) // Earliest principal wins
        .First().Principal;
}

// Step 3: Create new (should rarely happen after race protection)
return await CreatePrincipal();

private int GetAuthorityRank(VerificationSource source) => source switch
{
    VerificationSource.DynamicVerified => 1,
    VerificationSource.DirectSignatureMsg => 2,
    VerificationSource.DirectSignatureTx => 3,
    VerificationSource.WatchOnly => 4,
    _ => 99
};
```

### JWT & Wallet Proof Strategy
```csharp
// Issue 15-minute Axon JWT
var jwt = new JwtSecurityToken(
    issuer: "axon",
    audience: "axon-api", // Must validate this
    claims: new[] {
        new Claim("sub", $"axon:{principalId}"),
        new Claim("amr", authMethod), // "dynamic" or "wallet"
        new Claim("jti", Guid.NewGuid().ToString()) // For replay protection
    },
    expires: DateTime.UtcNow.AddMinutes(15)
);

// Wallet proof validation (KISS replay protection)
var signedPayload = new
{
    issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), // Max 5 min
    nonce = Guid.NewGuid().ToString(),
    address = walletAddress,
    chain_id = chain_id,
    network_environment = network_environment,
    aud = partnerIdentifier // Bind to relying party
};

// Check in-memory LRU cache
if (_replayCache.Contains($"{message}:{signature}"))
    return Error.Unauthorized("Signature already used");

// Add to cache with 10-min expiry
_replayCache.Add($"{message}:{signature}", TimeSpan.FromMinutes(10));
```

---

## 📝 Summary

This KISS PRD delivers a **simple, battle-ready** identity core: **Dynamic + Wallet** only, **DB-enforced** invariants, **deterministic** resolution, and **short-lived** Axon JWTs. It guarantees that users traverse providers and apps without fragmenting identity—while keeping the system easy to implement, test, and evolve.

**Next Step**: Ship Phase 1 (database constraints) in 2 days. Complete system live in 7 days.