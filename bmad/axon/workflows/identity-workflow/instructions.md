# Identity Workflow - Module Enhancement Instructions

<workflow>

<critical>Governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>Loaded config: {project-root}/bmad/axon/workflows/identity-workflow/workflow.yaml</critical>
<critical>EXTENDS story-implementation - inject at 4 strategic points, do NOT duplicate</critical>

## Overview

Enhances **story-implementation** with Identity module expertise for authentication, wallet management, principal resolution, and credential management in brownfield .NET + Clean Architecture + DDD + CQRS.

**Invocation**: `story-orchestrator` when `module = "Identity"`

**Core Patterns**: AxonPrincipal aggregate (50+ commands), owned entities (3), domain invariants (6), EF Core OwnsMany, composite keys, partial unique indexes.

---

<step n="0" goal="Initialize Identity context">
<action>Load {installed_path}/workflow.yaml</action>
<action>Confirm module = "Identity"</action>
<action>Set Identity context flag for all agents</action>
</step>

---

## ENHANCEMENT POINT 1: IDENTITY DOC LOADING

<step n="1" goal="Load Identity documentation (7 files)">
<action>Load all 7 Identity docs referenced in workflow.yaml:

**Module Docs** (5 files - identity_module_docs in workflow.yaml):
- 00-INDEX.md, 01-domain-model.md, 03-authentication.md, 05-api-contracts.md, 06-database-schema.md

**Library Docs** (1 file - identity_library_docs in workflow.yaml):
- dynamic_auth/IMPLEMENTATION_GUIDE.md

**Integration Docs** (1 file - identity_integration_docs in workflow.yaml):
- integrations/00-INDEX.md
</action>

<action>Parse workflow.yaml context:
- identity_subdomains (4)
- identity_domain_invariants (6)
- identity_code_patterns
- identity_challenges
</action>

<critical>All 7 docs must be loaded before proceeding</critical>
</step>

<step n="2" goal="Classify to Identity subdomain">
<action>Analyze story, match to ONE primary subdomain:
1. **Authentication** - JWT, JWKS, Dynamic.xyz
2. **Wallet Management** - Ed25519, Solana, signatures
3. **Principal Resolution** - Deterministic identity mapping
4. **Credential Management** - OAuth, multi-provider
</action>

<action>Set subdomain context from workflow.yaml:
- {{identity_subdomain}} (primary)
- {{identity_services}} (key services)
- {{identity_patterns}} (patterns)
</action>

<output section="subdomain_classification">
Subdomain: {{identity_subdomain}}
Services: {{identity_services}}
Patterns: {{identity_patterns}}
</output>
</step>

---

## ENHANCEMENT POINT 2: IDENTITY PRE-FLIGHT VALIDATION

<step n="3" goal="Identity codebase discovery (@axon-archaeologist)">
<action>Invoke @axon-archaeologist:

**Search Strategy** (5 layers):
1. **Domain**: AxonPrincipal aggregate (3 partial classes, 50+ commands), owned entities (3), domain events (4)
2. **Application**: CQRS handlers (ExchangeCredential, GenerateChallenge, VerifyWalletSignature, RefreshToken, GetMyPrincipal), services (PrincipalResolutionService)
3. **Infrastructure**: Services (Ed25519SignatureVerifier, JwksService, DynamicAuthService, WalletVerificationService), EF Core configs (OwnsMany patterns)
4. **API**: FastEndpoints (Auth endpoints - Exchange, Me, Challenge, Verify, Refresh)
5. **Tests**: Test patterns (Domain: command tests, Application: handler tests, Infrastructure: EF Core/crypto tests, E2E: auth flows)

**Reuse Focus**: Find similar AxonPrincipal methods, existing services to extend, handler patterns, test patterns.
</action>

<output section="discovery_report">
**Reuse Recommendations**:
- REUSE: [Components as-is]
- EXTEND: [Components to extend]
- ADAPT: [Patterns to adapt]
- CREATE: [New components]
</output>
</step>

<step n="4" goal="Identity library validation (@axon-library-sage)">
<action>Invoke @axon-library-sage:

**Validate against Identity library stack** (4 libraries from workflow.yaml):
1. Dynamic.xyz SDK - JWT validation, JWKS, wallet claims
2. NSec.Cryptography - Ed25519 for Solana
3. Microsoft.IdentityModel.Tokens - JWKS validation
4. SimpleBase - Base58 encoding (Solana addresses)

**4-Factor Scoring**: Capability match, complexity reduction, maintenance burden, integration cost.
</action>

<output section="library_validation">
**Library Recommendations**:
- Library-First: [Which libraries]
- Extension Needed: [Which services]
- Manual: [Justification]
</output>
</step>

<step n="5" goal="Identity pattern validation (@axon-doc-oracle)">
<action>Invoke @axon-doc-oracle:

**Validate 6 dimensions + Identity-specific**:
1. **Result<T, Error>**: All AxonPrincipal commands, services, handlers
2. **StrongId<T>**: AxonUserId, owned entity IDs, Vogen value objects
3. **CQRS**: BaseIdentityCommandHandler, BaseIdentityIdempotentCommandHandler, BaseIdentityQueryHandler
4. **Domain Events**: PrincipalChangedEvent, CredentialChangedEvent, OwnershipChangedEvent, WalletChangedEvent
5. **Owned Entities** (Identity-specific): Composite keys (PrincipalId, Id), OwnsMany, no independent DbSet, single xmin token, partial unique indexes
6. **Domain Invariants** (Identity-specific): 6 invariants from workflow.yaml (Service Risk, Ownership Uniqueness, **Signing Exclusivity**, Chain Default Uniqueness, Default Eligibility, Max Wallets)

**ADR Compliance**: ADR-001 (Modular Monolith), ADR-002 (CQRS), ADR-003 (Result), ADR-004 (Strong IDs), ADR-005 (PostgreSQL + xmin)
</action>

<output section="pattern_validation">
**Compliance Score**: [95-100%]
**Invariants**: [6/6 preserved]
**Violations**: [None / List]
</output>
</step>

---

## ✅ CHECKPOINT 2: IDENTITY PRE-FLIGHT APPROVAL

<step n="6" goal="Approve Identity pre-flight">
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: IDENTITY PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Subdomain**: {{identity_subdomain}}
**Reuse Score**: [High/Medium/Low]
**Libraries**: [List]
**Compliance**: [95-100%]
**Invariants**: [6/6]

Approve proceeding to implementation?
[c] Continue | [e] Edit | [a] Abort
</ask>
</step>

---

## ENHANCEMENT POINT 3: IDENTITY IMPLEMENTATION GUIDANCE

<step n="7" goal="Identity-specific implementation guidance">
<action>Provide surgical implementation guidance based on subdomain:

**AxonPrincipal Modifications**:
- Which partial class? (AxonPrincipal.cs / Commands.cs / Queries.cs)
- Follow existing patterns from 50+ command methods
- Enforce invariants BEFORE mutation
- Raise domain events AFTER mutation
- Return Result<T, Error>

**Owned Entity Modifications**:
- Maintain composite keys: (PrincipalId, Id)
- Preserve partial unique indexes (signing exclusivity critical)
- EF Core: OwnsMany, no independent DbSet, single xmin token
- Migration required? (New fields, new owned entity, modified indexes)

**Service Implementation**:
- See workflow.yaml: identity_code_patterns for service patterns
- Return Result<T, Error>
- Constructor injection: ILogger, IOptions<T>, dependencies

**CQRS Handlers**:
- Commands: BaseIdentityCommandHandler or BaseIdentityIdempotentCommandHandler
- Queries: BaseIdentityQueryHandler
- Pattern: Load aggregate → Invoke method → SaveChanges

**API Endpoints**:
- FastEndpoints pattern: Endpoint<TRequest, TResponse>
- Route: POST/GET /api/v1/auth/{route}
- FluentValidation for request validation
- Map Result<T, Error> → HTTP status codes
</action>

<output section="implementation_plan">
**Bottom-Up Layering**:
1. Domain: [AxonPrincipal methods, owned entities, events]
2. Application: [Handlers, services]
3. Infrastructure: [Service implementations, EF Core config, migrations]
4. API: [FastEndpoints]
</output>
</step>

---

## ENHANCEMENT POINT 4: IDENTITY VALIDATION

<step n="8" goal="Identity comprehensive testing (@axon-quality-guardian)">
<action>Invoke @axon-quality-guardian:

**4-Layer Test Strategy**:
1. **Domain**: AxonPrincipal command tests (50+ patterns to follow), owned entity tests, value object tests, domain event tests
2. **Application**: Handler integration tests (in-memory DB), idempotency tests, Result<T, Error> paths
3. **Infrastructure**: EF Core tests (composite keys, xmin concurrency, partial indexes), service integration tests (Ed25519, JWKS, Dynamic.xyz)
4. **E2E**: Authentication flows (JWT → Exchange → Me with ETag), wallet flows (Challenge → Sign → Verify)

**Identity Test Scenarios** (from workflow.yaml success_metrics):
- JWT validation with JWKS
- Ed25519 signature verification (Solana base58)
- Principal resolution determinism
- Credential uniqueness
- Wallet signing exclusivity
- Chain default eligibility
- Auto-revocation logic
- Owned entity cascade operations
</action>

<output section="test_plan">
**Coverage Breakdown**: [Domain, Application, Infrastructure, E2E]
**AC Coverage**: 100%
**Invariant Coverage**: 6/6 (100%)
**Estimated Coverage**: 90%+
</output>
</step>

<step n="9" goal="Validate Identity domain invariants">
<action>Validate all 6 invariants from workflow.yaml:
1. **Service Risk Constraint**: AxonPrincipal.UpdateRiskTier()
2. **Ownership Uniqueness**: Composite key + unique constraint
3. **Signing Exclusivity** (CRITICAL): Partial unique index + LinkWalletOwnership() validation
4. **Chain Default Uniqueness**: SetChainDefault() enforcement
5. **Default Eligibility**: CanSetAsDefault() validation
6. **Max Wallets**: IsAtWalletLimit property (10 limit)

**Validation**: Code check + DB check + Test check for each invariant.
</action>

<output section="invariant_validation">
**Invariants Preserved**: 6/6 (100%) ✅
**Critical Invariant** (signing exclusivity): Validated ✓
</output>
</step>

---

## COMPLETION

<step n="10" goal="Identity summary">
<output section="completion_summary">
**Identity Workflow Complete** ✅

**Module**: Identity
**Subdomain**: {{identity_subdomain}}

**Deliverables**:
- ✓ 7 Identity docs loaded
- ✓ Subdomain classified
- ✓ Codebase discovery (reuse recommendations)
- ✓ Library validation (Dynamic.xyz, NSec, etc.)
- ✓ Pattern compliance (95%+)
- ✓ Domain invariants preserved (6/6)
- ✓ Comprehensive test suite
- ✓ Owned entity patterns validated

**Quality Metrics**:
- Test Coverage: 90%+
- AC Coverage: 100%
- Invariants: 100%
- Build: 100%
- Doc Sync: Zero drift
</output>

<critical>Append to base story-implementation summary</critical>
</step>

</workflow>