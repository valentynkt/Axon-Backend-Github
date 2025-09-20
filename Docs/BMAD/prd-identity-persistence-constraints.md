# Product Requirements Document: Identity Persistence & Constraints System (KISS • 20/80)

**Document Version**: 1.1-final
**Created**: 2025-01-20
**Last Updated**: 2025-01-20
**Scope**: Dynamic + Wallet Authentication Only (No OIDC, No Complex Features)

---

## 🎯 Executive Summary

### TL;DR
Ship a **KISS, 20/80** identity fabric for **Dynamic + Wallet** only. Database enforces what matters: unique Wallet per `(environment, chain, address)`, unique Credential per `(environment, provider, issuer, subject)`, one live Ownership per `(principal, wallet)`, and ≤1 verified+signing owner per wallet. Credential-first → wallet-control resolution ensures users map to the **same AxonPrincipal** across apps. Mint short-lived Axon JWT (~15m) for both flows. **No refresh tokens, no nonce ledger, no OIDC, no overengineering.**

### Business Problem
Axon needs a minimal, vendor-agnostic identity core where any accepted proof (Dynamic JWT or wallet signature) deterministically resolves to one AxonPrincipal across all B2B clients and apps (web/mobile/dApp).

**Current State**: Multiple authentication paths can create duplicate identities, environment isolation is incomplete, and wallet ownership constraints lack database-level enforcement.

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
- **Security Auditor**: "How do you guarantee environment isolation at the database level?"
- **B2B Client**: "Can we trust that wallet ownership is exclusive?"

### Business Constraints (KISS Focus)
1. **Database-First Security**: Hard constraints at DB level, not application logic
2. **Environment Safety**: Absolute separation between mainnet/devnet/test
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
| **Environment Leaks** | Natural keys | Include environment in all unique indexes |
| "Mainnet/devnet collision" | Hard separation | `(env, chain, address)` uniqueness |
| **Token Complexity** | Simple JWT | 15-minute lifespan, no refresh |
| "Complex token management" | KISS approach | Re-exchange (Dynamic) or re-prompt (wallet) |

### Expected Outcomes
- **Identity Unity**: One AxonPrincipal per real user regardless of auth method
- **Security Assurance**: Database-level guarantees prevent constraint violations
- **Enterprise Compliance**: Full audit trail and deterministic identity resolution
- **Developer Experience**: Clear error messages and predictable behavior

---

## 🎯 Business Objectives

### Primary Business Goals

#### 1. **Enable Enterprise B2B Scaling** (Priority: P0)
**Objective**: Support vendor-agnostic identity integration for enterprise clients
**Success Criteria**:
- Single identity per user across all authentication methods
- Support for any OIDC provider, wallet system, or custom authentication
- Zero identity conflicts during client onboarding
- Deterministic principal resolution with <100ms latency

**Business Value**: Unlocks enterprise B2B market with confidence in identity management

#### 2. **Achieve Database-Level Security** (Priority: P0)
**Objective**: Guarantee identity constraints at the database level, not just business logic
**Success Criteria**:
- Environment isolation enforced by database constraints
- Wallet exclusivity guaranteed by unique indexes
- Credential uniqueness enforced across all environments
- Zero possibility of constraint bypass through API or direct access

**Business Value**: Security assurance required for enterprise customer confidence

#### 3. **Ensure Deterministic Identity Resolution** (Priority: P0)
**Objective**: Provide predictable, auditable identity resolution for compliance
**Success Criteria**:
- Documented resolution algorithm with deterministic outcomes
- Complete audit trail of identity merge operations
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

## 🔧 Functional Requirements

### Core Features

#### F1: **Unified Principal Resolution Algorithm**
**Business Requirement**: Single identity per user across all authentication methods
**Functional Specification**:
- **Credential-First Matching**: Check for existing AxonPrincipal with matching credentials
- **Wallet-Based Fallback**: If no credential match, check for principals with verified+signing wallet ownership
- **Merge Strategy**: When multiple candidates exist, merge using deterministic rules
- **New Principal Creation**: Only when no existing identity can be matched

**Business Value**: Eliminates identity fragmentation while supporting all authentication methods

#### F2: **Database-Level Environment Isolation**
**Business Requirement**: Absolute separation between environments for multi-tenant security
**Functional Specification**:
- All unique constraints scoped by environment identifier
- Environment context automatically added to all identity queries
- Database-level prevention of cross-environment data access
- Partition-like behavior using environment prefix in constraints

**Business Value**: Security assurance required for enterprise B2B confidence

#### F3: **Wallet Exclusivity Enforcement**
**Business Requirement**: One verified+signing owner per wallet per environment
**Functional Specification**:
- Database unique constraint on (environment, wallet_id, verified+signing status)
- Automatic ownership transfer with audit trail
- Watch-only ownership exceptions (multiple allowed)
- Clear error messages for ownership conflicts

**Business Value**: Prevents wallet ownership disputes and provides clear business rules

#### F4: **Deterministic Conflict Resolution**
**Business Requirement**: Predictable outcomes for complex identity scenarios
**Functional Specification**:
- Documented resolution algorithm with decision trees
- Merge policies for credentials, wallets, and profile data
- Audit trail for all merge operations
- Rollback capabilities for resolution errors

**Business Value**: Compliance-ready identity management with full auditability

### Non-Functional Requirements

#### Performance
- **Principal Resolution**: <100ms P95 latency for authenticated users
- **Constraint Validation**: <10ms additional overhead for database constraints
- **Bulk Operations**: Efficient handling of batch identity operations
- **Memory Usage**: <5% increase for constraint enforcement logic

#### Security
- **Environment Isolation**: 100% database-enforced separation
- **Constraint Integrity**: Zero bypass possibilities for uniqueness constraints
- **Audit Trail**: Complete logging of identity operations and merges
- **Data Protection**: Secure handling of PII in identity resolution

#### Reliability
- **Transaction Safety**: ACID guarantees for all identity operations
- **Constraint Enforcement**: Database-level validation with clear error messages
- **Recovery**: Rollback capabilities for failed merge operations
- **Monitoring**: Comprehensive metrics for identity system health

---

## 📊 Success Metrics

### Before vs After Identity Management

| Metric | Current State | Target State | Improvement |
|--------|---------------|--------------|------------|
| **Identity Conflicts** | Possible via API bypass | Database-prevented | 100% elimination |
| **Environment Isolation** | Business-logic enforced | Database-guaranteed | Security assurance |
| **Resolution Determinism** | Varies by implementation | Documented algorithm | Predictable outcomes |
| **Enterprise Readiness** | Single-tenant focused | Multi-tenant ready | B2B market enablement |

### Key Success Indicators
1. **Security**: Zero identity constraint violations at database level
2. **Performance**: Principal resolution maintains <100ms P95 latency
3. **Compliance**: Complete audit trail for all identity operations
4. **Integration**: Successful enterprise client onboarding with identity confidence

---

## 🗓️ Implementation Roadmap

### Phase 1: Database Constraints Foundation (5 days)
**Goal**: Implement database-level constraint enforcement
**Implementation**:
- Environment-scoped unique constraints for credentials and wallets
- Migration strategy for existing data
- Constraint violation error handling

**Business Value**: Security foundation for enterprise confidence

### Phase 2: Principal Resolution Algorithm (7 days)
**Goal**: Implement deterministic identity resolution
**Implementation**:
- Credential-first, wallet-fallback resolution logic
- Merge strategies for conflicting principals
- Audit trail for resolution decisions

**Business Value**: Unified identity across authentication methods

### Phase 3: Conflict Resolution & Testing (5 days)
**Goal**: Handle edge cases and comprehensive testing
**Implementation**:
- Complex conflict resolution scenarios
- Performance optimization for constraint checking
- Integration testing with all authentication methods

**Business Value**: Enterprise-ready identity system with confidence

### Total Timeline: 17 days
**Progressive Value**: Each phase builds enterprise readiness while maintaining system stability

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
- ✅ Database-level environment isolation with zero bypass possibilities
- ✅ Unified identity resolution across Dynamic, wallet, and OIDC authentication
- ✅ Deterministic principal resolution with <100ms latency
- ✅ Complete audit trail for identity operations and merges

### Measures Success
- **Enterprise Confidence**: Security auditors approve database-level constraints
- **Developer Experience**: Clear, predictable identity APIs with excellent error messages
- **Business Enablement**: Successful B2B client onboarding with vendor-agnostic identity
- **System Reliability**: Zero identity conflicts and predictable performance

---

## 📋 Technical Implementation Details

### Database Schema Changes

#### Environment-Scoped Constraints
```sql
-- Credential uniqueness per environment
CREATE UNIQUE INDEX ux_credential_environment_provider_issuer_subject
ON identity.identity_credential (environment, provider, issuer, subject);

-- Wallet exclusivity per environment
CREATE UNIQUE INDEX ux_wallet_ownership_environment_verified_signing
ON identity.wallet_ownership (environment, wallet_id)
WHERE access_mode = 'Signing' AND status = 'Verified';
```

#### Principal Resolution Data Model
```csharp
public sealed class PrincipalResolutionAudit
{
    public Guid Id { get; set; }
    public AxonUserId TargetPrincipalId { get; set; }
    public List<AxonUserId> MergedPrincipalIds { get; set; }
    public string ResolutionMethod { get; set; } // "credential_match", "wallet_match", "new_principal"
    public ResolutionContext Context { get; set; }
    public DateTime ResolvedAt { get; set; }
}
```

### Principal Resolution Algorithm

#### Resolution Flow
1. **Extract Identity Claims**: From JWT, wallet signature, or OIDC token
2. **Credential Matching**: Query for existing principals with matching (provider, issuer, subject)
3. **Wallet Matching**: If no credential match, query principals with verified+signing wallet ownership
4. **Conflict Resolution**: Handle multiple matches using merge policies
5. **Principal Creation**: Create new principal only if no matches found

#### Merge Policies
- **Credentials**: Additive - combine all unique credentials
- **Wallets**: Ownership transfer with audit trail
- **Profile Data**: Most recent non-null values preferred
- **Chain Defaults**: Preserve verified+signing wallet preferences

---

## 📝 Summary

This PRD establishes the foundation for enterprise-grade identity management that ensures unified identity across all authentication methods while providing database-level security guarantees. The system enables Axon's transition to B2B enterprise clients with confidence in vendor-agnostic identity integration.

**Next Step**: Begin Phase 1 implementation of database constraints foundation to establish the security foundation required for enterprise confidence.