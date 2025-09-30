# Identity Workflow - Module-Specific Enhancement Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/identity-workflow/workflow.yaml</critical>
<critical>This workflow EXTENDS story-implementation - it adds Identity-specific context to the base 4-phase workflow</critical>
<critical>Do NOT duplicate the base workflow - inject Identity enhancements at strategic points</critical>

## Overview

This workflow enhances story-implementation with **Identity module expertise**:
- **Authentication Mastery**: JWT validation (JWKS, Dynamic.xyz), wallet signatures (Ed25519)
- **Domain Mastery**: AxonPrincipal aggregate (50+ commands), owned entities (3), 6 domain invariants
- **Pattern Mastery**: EF Core OwnsMany, composite keys, partial unique indexes, deterministic resolution
- **Library Mastery**: Dynamic.xyz SDK, NSec.Cryptography, Microsoft.IdentityModel.Tokens

## Identity Module Context

This workflow is invoked by **story-orchestrator** when `module = "Identity"` is detected.

**Identity Subdomains**:
1. **Authentication** - JWT validation, JWKS, Dynamic.xyz integration
2. **Wallet Management** - Ed25519 signatures, ownership verification, multi-chain
3. **Principal Resolution** - Deterministic identity mapping, credential uniqueness
4. **Credential Management** - OAuth/JWT credentials, multi-provider support

**Key Codebase Components**:
- **Aggregate Root**: AxonPrincipal (50+ command methods in 3 partial class files)
- **Owned Entities**: IdentityCredential, WalletOwnership, PrincipalChainDefault
- **Services**: Ed25519SignatureVerifier, JwksService, PrincipalResolutionService, DynamicAuthService
- **CQRS Handlers**: ExchangeCredential, GenerateChallenge, VerifyWalletSignature, RefreshToken, GetMyPrincipal

---

<step n="0" goal="Initialize Identity workflow context">
<action>Load Identity workflow configuration: {installed_path}/workflow.yaml</action>
<action>Confirm this is an Identity module story (module = "Identity")</action>
<action>Set Identity context flag for all agents</action>

<critical>This step happens BEFORE story-implementation Step 0</critical>
</step>

---

## ENHANCEMENT POINT 1: IDENTITY-SPECIFIC DOC LOADING

<step n="1" goal="Load Identity module documentation (7 files)">
<action>Load all 7 Identity documentation files:

**Identity Module Docs (5 files)**:
1. {engineering_docs}/modules/identity/00-INDEX.md
   - Module overview, responsibilities, public contracts
   - Domain entities: 1 aggregate root + 3 owned entities
   - Value objects: 4 (Address, ChainId, ProviderType, ProofType)
   - Commands: 4, Queries: 1, Events: 4

2. {engineering_docs}/modules/identity/01-domain-model.md
   - AxonPrincipal aggregate (factory methods, command methods, query methods)
   - Owned entities: IdentityCredential, WalletOwnership, PrincipalChainDefault
   - 6 domain invariants (service risk, ownership uniqueness, signing exclusivity, etc.)
   - Composite keys, partial unique indexes

3. {engineering_docs}/modules/identity/03-authentication.md
   - Dynamic JWT authentication flow
   - Manual wallet signature flow (challenge-response)
   - JWT validation (JWKS)
   - Ed25519 signature verification (Solana)
   - Principal resolution (deterministic)

4. {engineering_docs}/modules/identity/05-api-contracts.md
   - POST /api/v1/auth/exchange (idempotent principal sync)
   - GET /api/v1/auth/me (ETag support)
   - POST /api/v1/auth/challenge
   - POST /api/v1/auth/verify
   - POST /api/v1/auth/refresh

5. {engineering_docs}/modules/identity/06-database-schema.md
   - EF Core owned entity configuration (OwnsMany)
   - Composite keys: (PrincipalId, Id)
   - Partial unique indexes (signing exclusivity)
   - Concurrency: Single xmin token on aggregate root

**Identity Library Docs (1 file)**:
6. {libraries_docs}/dynamic_auth/IMPLEMENTATION_GUIDE.md
   - Dynamic.xyz SDK usage
   - JWT validation patterns
   - Wallet claim parsing
   - JWKS caching

**Integration Docs (1 file)**:
7. {engineering_docs}/integrations/00-INDEX.md
   - Dynamic.xyz integration patterns
   - External service patterns
</action>

<action>Parse Identity-specific context from workflow.yaml:
  - 4 Identity subdomains (authentication, wallet_management, principal_resolution, credential_management)
  - 6 Identity challenges
  - 6 domain invariants
  - Key codebase patterns (aggregate_root, owned_entities, cqrs_handlers)
</action>

<critical>All 7 docs must be loaded in memory for Identity-aware decision-making</critical>
</step>

<step n="2" goal="Map story to Identity subdomain">
<action>Analyze story content and classify into Identity subdomain:

**Authentication Subdomain** - Keywords: JWT, token, JWKS, Dynamic.xyz, authentication, login
- Services: JwksService, DynamicAuthService, DynamicClaimNormalizer
- Patterns: JWKS caching, token validation, claim normalization
- Tests: JWT validation scenarios, JWKS refresh, Dynamic.xyz integration

**Wallet Management Subdomain** - Keywords: wallet, signature, Ed25519, Solana, verification, challenge
- Services: Ed25519SignatureVerifier, WalletVerificationService, AddressNormalizationService
- Patterns: Challenge-response, Solana base58, signature verification
- Tests: Ed25519 signature tests, challenge generation, wallet ownership

**Principal Resolution Subdomain** - Keywords: principal, identity, resolution, deterministic, mapping
- Services: PrincipalResolutionService
- Patterns: Deterministic ID generation, credential uniqueness
- Tests: Resolution determinism, credential uniqueness enforcement

**Credential Management Subdomain** - Keywords: credential, OAuth, provider, multi-provider, sync
- Entities: IdentityCredential (owned entity)
- Patterns: Idempotent credential sync, last-seen tracking
- Tests: Credential uniqueness, provider management
</action>

<action>Set subdomain context variables:
  - {{identity_subdomain}} (primary classification)
  - {{identity_services}} (relevant services to consider)
  - {{identity_patterns}} (patterns to apply)
  - {{identity_tests}} (test scenarios to generate)
</action>

<template-output section="identity_subdomain_mapping">
**Identity Subdomain Classification**

Story: {{story_title}}
Primary Subdomain: {{identity_subdomain}}

**Relevant Components**:
- Services: {{identity_services}}
- Patterns: {{identity_patterns}}
- Test Scenarios: {{identity_tests}}

**Domain Invariants to Validate**:
[List applicable invariants from the 6 defined in workflow.yaml]

**Codebase Impact Analysis**:
- AxonPrincipal modifications needed? [Yes/No - which methods?]
- Owned entity changes? [Which: IdentityCredential/WalletOwnership/PrincipalChainDefault?]
- New services? [List if creating new services]
- Service extensions? [Which existing services to extend?]
</template-output>

<critical>This classification guides all subsequent Identity-specific validation</critical>
</step>

---

## ENHANCEMENT POINT 2: IDENTITY-SPECIFIC PRE-FLIGHT VALIDATION

<step n="3" goal="Identity-enhanced codebase discovery (Archaeologist)">
<action>Invoke @axon-archaeologist with Identity context:

**Search Strategy** (5 layers - Identity-focused):

**Layer 1: Domain Layer** (src/Modules/Identity/Domain/)
- Search AxonPrincipal aggregate:
  * Partial classes: AxonPrincipal.cs, AxonPrincipal.Commands.cs, AxonPrincipal.Queries.cs
  * Find similar command methods (50+ existing - pattern match by intent)
  * Locate relevant query methods
- Search owned entities:
  * IdentityCredential.cs, WalletOwnership.cs, PrincipalChainDefault.cs
  * Identify existing fields, methods, validations
- Search domain events:
  * PrincipalChangedEvent, CredentialChangedEvent, OwnershipChangedEvent, WalletChangedEvent
  * Pattern: When to raise which events?

**Layer 2: Application Layer** (src/Modules/Identity/Application/)
- Search CQRS handlers:
  * Commands: ExchangeCredential, GenerateChallenge, VerifyWalletSignature, RefreshToken
  * Queries: GetMyPrincipal
  * Identify similar handler patterns
- Search services:
  * PrincipalResolutionService, StandardizedErrorMessageService
  * Check existing resolution logic, error message patterns

**Layer 3: Infrastructure Layer** (src/Modules/Identity/Infrastructure/)
- Search services:
  * Ed25519SignatureVerifier, JwksService, DynamicAuthService, WalletVerificationService
  * AddressNormalizationService, DynamicClaimNormalizer
  * Identify existing implementations to extend vs create new
- Search EF Core configurations:
  * Persistence/Configurations/ - owned entity configuration patterns
  * Verify OwnsMany usage, composite keys, partial indexes

**Layer 4: API Layer** (src/Api/Endpoints/V1/Auth/)
- Search existing endpoints:
  * ExchangeEndpoint, MeEndpoint, ChallengeEndpoint, VerifySignatureEndpoint, RefreshEndpoint
  * FastEndpoints patterns, request/response mapping

**Layer 5: Test Layer** (tests/Modules/Identity/)
- Search test patterns:
  * Domain tests: AxonPrincipal command tests (50+ existing patterns)
  * Application tests: Handler integration tests, caching tests
  * Infrastructure tests: EF Core tests, service tests, cryptography tests
  * E2E tests: Authentication flow tests
</action>

<template-output section="identity_discovery_report">
**Identity Codebase Discovery Report**

**Reusable Components Found**:
- AxonPrincipal Methods: [List similar command/query methods]
- Services: [Existing services that can be extended]
- Handlers: [Similar CQRS handler patterns]
- Tests: [Test patterns to replicate]

**Gaps Requiring New Code**:
- New command methods on AxonPrincipal? [List]
- New services? [List with rationale]
- New handlers? [List]
- Database migrations needed? [Yes/No - what changes?]

**Reuse Recommendations**:
- REUSE: [Components to use as-is]
- EXTEND: [Components to extend surgically]
- ADAPT: [Patterns to adapt]
- CREATE: [New components needed]

**Pattern Compliance Check**:
- Result<T, Error> pattern usage: ✓
- StrongId<T> usage: ✓
- Domain events: [Which events to raise?]
- Owned entity patterns: [Follows OwnsMany conventions?]
</template-output>
</step>

<step n="4" goal="Identity-enhanced library validation (Library Sage)">
<action>Invoke @axon-library-sage with Identity library context:

**Identity Library Stack** (validate against these):
1. **Dynamic.xyz SDK** (Docs/Libraries/dynamic_auth/)
   - JWT validation (JWKS endpoints)
   - Wallet claim parsing
   - DynamicAuthOptions configuration
   - Usage: DynamicAuthService, DynamicClaimNormalizer

2. **NSec.Cryptography** (Ed25519 for Solana)
   - Ed25519 signature algorithm
   - PublicKey/Signature parsing
   - Usage: Ed25519SignatureVerifier

3. **Microsoft.IdentityModel.Tokens** (JWKS validation)
   - JsonWebKeySet handling
   - Token validation parameters
   - Usage: JwksService

4. **SimpleBase** (Base58 encoding for Solana)
   - Base58.Bitcoin.Decode/Encode
   - Usage: Ed25519SignatureVerifier (address parsing)

**Validation Checks**:
- Does story require Dynamic.xyz API calls? → DynamicApiClient available
- Does story require signature verification? → Ed25519SignatureVerifier available
- Does story require JWKS validation? → JwksService available
- Does story require base58 encoding? → SimpleBase available
- Are there missing library capabilities? → Recommend library additions

**4-Factor Scoring** (Library vs Manual):
1. **Capability Match**: Does library handle this requirement?
2. **Complexity Reduction**: How much code does it save?
3. **Maintenance Burden**: Does it reduce long-term maintenance?
4. **Integration Cost**: How easy to integrate?
</action>

<template-output section="identity_library_report">
**Identity Library Validation Report**

**Library Recommendations**:
- Dynamic.xyz SDK: [Capability match - Yes/No/Partial]
- NSec.Cryptography: [Capability match - Yes/No/Partial]
- Microsoft.IdentityModel.Tokens: [Capability match - Yes/No/Partial]
- SimpleBase: [Capability match - Yes/No/Partial]

**Recommended Approach**:
- Library-First: [Which libraries to use]
- Extension Needed: [Which services to extend]
- Manual Implementation: [What must be manual - with justification]

**Library Usage Patterns** (from codebase):
[Show existing usage patterns to follow]

**Risk Assessment**:
- Breaking changes: [Library version compatibility]
- Integration complexity: [High/Medium/Low]
</template-output>
</step>

<step n="5" goal="Identity-enhanced pattern validation (Doc Oracle)">
<action>Invoke @axon-doc-oracle with Identity pattern context:

**Pattern Validation Dimensions** (6 dimensions + Identity-specific):

**1. Result<T, Error> Pattern**:
- All AxonPrincipal command methods return Result<T, Error>
- All service methods return Result<T, Error>
- All handlers return Result<TResponse, Error>
- Error types: IdentityDomainErrors, WalletDomainErrors, AuthErrors

**2. StrongId<T> Pattern**:
- AxonUserId (aggregate root ID)
- IdentityCredentialId, WalletOwnershipId, PrincipalChainDefaultId (owned entity IDs)
- Custom Vogen value objects: Address, ChainId, ProviderType, ProofType

**3. CQRS Pattern**:
- Commands: Inherit from IdentityBaseCommand or IdentityIdempotentCommand
- Queries: Inherit from IdentityBaseQuery
- Handlers: BaseIdentityCommandHandler, BaseIdentityIdempotentCommandHandler, BaseIdentityQueryHandler

**4. Domain Events Pattern**:
- Raise events on aggregate state changes: PrincipalChangedEvent, CredentialChangedEvent, etc.
- Events published via MediatR pipeline
- No business logic in event handlers (read model updates only)

**5. Owned Entity Pattern** (Identity-specific):
- Composite keys: (PrincipalId, Id)
- OwnsMany configuration in EF Core
- No independent DbSet
- Single concurrency token on aggregate root (xmin)
- Partial unique indexes (e.g., signing exclusivity)

**6. Domain Invariants** (Identity-specific):
- Service Risk Constraint
- Ownership Uniqueness
- Signing Exclusivity (critical - one verified+signing per wallet globally)
- Chain Default Uniqueness
- Default Eligibility
- Max Wallets (10 per principal)

**ADR Compliance**:
- ADR-001: Modular Monolith (Identity is a bounded context)
- ADR-002: CQRS + MediatR (commands/queries/handlers)
- ADR-003: Result Pattern (no exceptions in domain)
- ADR-004: Strong IDs (AxonUserId, etc.)
- ADR-005: PostgreSQL (EF Core owned entities, xmin concurrency)
</action>

<template-output section="identity_pattern_validation">
**Identity Pattern Validation Report**

**Compliance Score**: [95-100%]

**Pattern Checklist**:
- ✓ Result<T, Error> pattern usage
- ✓ StrongId<T> usage
- ✓ CQRS command/query separation
- ✓ Domain events on state changes
- ✓ Owned entity patterns (OwnsMany, composite keys)
- ✓ Domain invariants preserved (6/6)
- ✓ ADR compliance (5/5 applicable ADRs)

**Violations Detected**: [None / List violations with remediation]

**Identity-Specific Validations**:
- Owned entity composite keys correct? ✓
- Partial unique indexes for signing exclusivity? ✓
- Single concurrency token strategy? ✓
- Domain invariants enforced in code? ✓

**Risk Assessment**:
- Pattern violations: [None / List]
- Architectural drift: [None / List]
- Invariant violations: [None / List]
</template-output>
</step>

---

## ✅ CHECKPOINT 2: IDENTITY PRE-FLIGHT APPROVAL (3-5 min)

<step n="6" goal="Checkpoint 2: Approve Identity-enhanced pre-flight">
<action>Combine outputs from steps 3, 4, 5 into unified pre-flight report</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: IDENTITY PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Identity Subdomain**: {{identity_subdomain}}

**Discovery Summary**:
- Reusable components: [Count]
- New components needed: [Count]
- Reuse score: [High/Medium/Low]

**Library Summary**:
- Libraries to use: [List]
- Manual implementation: [Justification]

**Pattern Summary**:
- Compliance score: [95-100%]
- Domain invariants: [6/6 preserved]
- Violations: [None / List]

Review the Identity-enhanced pre-flight analysis above.

Do you approve proceeding to implementation?
- [c] Continue to Implementation Phase
- [e] Edit/clarify pre-flight findings
- [a] Abort workflow

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs clarification in the pre-flight analysis?</ask>
  <action>Re-run specific validation based on feedback</action>
  <goto step="6">Re-present pre-flight for approval</goto>
</action>

<action if="user_response == 'a'">
  <action>Abort workflow and document reason</action>
  <exit/>
</action>
</step>

---

## ENHANCEMENT POINT 3: IDENTITY-SPECIFIC IMPLEMENTATION GUIDANCE

<step n="7" goal="Provide Identity-specific code generation guidance">
<action>Based on subdomain classification and pre-flight analysis, provide surgical implementation guidance:

**If AxonPrincipal Modification Required**:
1. **Determine which partial class file**:
   - New factory method? → AxonPrincipal.cs
   - New command method? → AxonPrincipal.Commands.cs
   - New query method? → AxonPrincipal.Queries.cs

2. **Follow existing method patterns**:
   - Study similar methods (50+ existing command methods as reference)
   - Signature pattern: `public Result<T, Error> MethodName(params)`
   - Validate invariants BEFORE state mutation
   - Raise domain events AFTER successful mutation
   - Return Result.Success or Result.Failure

3. **Domain Invariant Enforcement**:
   - Service Risk Constraint: Check in UpdateRiskTier()
   - Ownership Uniqueness: Validate in LinkWalletOwnership()
   - Signing Exclusivity: Check via checkConflictFunc in LinkWalletOwnership()
   - Chain Default Uniqueness: Enforce in SetChainDefault()
   - Default Eligibility: Check via CanSetAsDefault()
   - Max Wallets: Validate via IsAtWalletLimit property

4. **Domain Event Raising**:
   - PrincipalChangedEvent: Risk tier changes, significant state changes
   - CredentialChangedEvent: Credential added/updated
   - OwnershipChangedEvent: Wallet linked/unlinked/verified
   - WalletChangedEvent: Wallet-level changes

**If Owned Entity Modification Required**:
1. **Maintain Composite Key Pattern**:
   - All owned entities: (PrincipalId, Id)
   - Never modify composite key pattern
   - Access only via aggregate root

2. **Preserve Partial Unique Indexes**:
   - WalletOwnership: Signing exclusivity index (verified+signing)
   - IdentityCredential: Unique (Provider, Issuer, Subject)
   - Check Infrastructure/Persistence/Configurations/ for existing indexes

3. **EF Core Configuration**:
   - Use OwnsMany() in AxonPrincipalConfiguration
   - Configure composite key: HasKey(e => new { e.PrincipalId, e.Id })
   - Add unique indexes where needed
   - No independent DbSet<T>

4. **Migration Required?**:
   - New fields on owned entities? → Yes, create migration
   - New owned entity type? → Yes, create migration + configuration
   - Modified unique indexes? → Yes, update migration

**If Service Implementation/Extension Required**:
1. **Follow existing service patterns**:
   - Constructor injection: ILogger, IOptions<T>, dependencies
   - Return Result<T, Error> from all public methods
   - Use async/await for I/O operations
   - Log at appropriate levels

2. **Authentication Subdomain Services**:
   - JwksService: JWKS caching, token validation
   - DynamicAuthService: Claim parsing, JWT validation
   - DynamicClaimNormalizer: Claim normalization logic

3. **Wallet Management Subdomain Services**:
   - Ed25519SignatureVerifier: Solana signature verification (base58, NSec.Cryptography)
   - WalletVerificationService: Multi-chain orchestration
   - AddressNormalizationService: Address format standardization

4. **Principal Resolution Subdomain Services**:
   - PrincipalResolutionService: Deterministic ID generation from credentials

**If CQRS Handler Required**:
1. **Command Handlers**:
   - Inherit: BaseIdentityCommandHandler or BaseIdentityIdempotentCommandHandler
   - Pattern: Load aggregate → Invoke command method → SaveChanges
   - Idempotent commands: Use IdempotencyKey for deduplication

2. **Query Handlers**:
   - Inherit: BaseIdentityQueryHandler
   - Read from IdentityReadDbContext (separate from write)
   - Use ETag for conditional GET (GetMyPrincipal pattern)

3. **Handler Testing**:
   - Integration tests with in-memory database
   - Test success path + all error paths
   - Test idempotency (for idempotent commands)

**If API Endpoint Required**:
1. **FastEndpoints Pattern**:
   - Inherit: Endpoint<TRequest, TResponse>
   - Configure: POST/GET /api/v1/auth/{route}
   - Use FluentValidation for request validation
   - Map Result<T, Error> to HTTP status codes

2. **Authentication**:
   - AllowAnonymous: ExchangeEndpoint (bearer is input)
   - Authenticated: All other endpoints (middleware validates bearer)

3. **ETag Support** (if applicable):
   - Generate ETag from principal state hash
   - Return 304 Not Modified if If-None-Match matches
   - Return 200 with new ETag if changed
</action>

<template-output section="identity_implementation_plan">
**Identity Implementation Plan**

**Bottom-Up Layering** (Axon mandated order):

**Layer 1: Domain** (src/Modules/Identity/Domain/)
Files to modify/create:
- [ ] AxonPrincipal.Commands.cs: [New method: MethodName()]
- [ ] WalletOwnership.cs: [New field: FieldName]
- [ ] Events/: [New event: EventName]

Domain Invariants to Enforce:
- [List applicable invariants with enforcement strategy]

**Layer 2: Application** (src/Modules/Identity/Application/)
Files to modify/create:
- [ ] Commands/NewCommand/: NewCommandHandler.cs, NewCommand.cs
- [ ] Queries/NewQuery/: NewQueryHandler.cs, NewQuery.cs
- [ ] Services/: [NewService.cs or extend existing]

**Layer 3: Infrastructure** (src/Modules/Identity/Infrastructure/)
Files to modify/create:
- [ ] Services/: [Implementation of INewService]
- [ ] Persistence/Configurations/: [EF Core configuration if schema changes]
- [ ] Migrations/: [New migration if database changes]

**Layer 4: API** (src/Api/Endpoints/V1/Auth/)
Files to modify/create:
- [ ] NewEndpoint.cs: [FastEndpoint implementation]

**Implementation Notes**:
- Owned entity patterns: [Specific considerations]
- Domain events: [Which to raise, when]
- Library usage: [Which libraries, how]
- Test strategy: [Which test types needed]
</template-output>

<critical>Implementation Surgeon uses this plan for surgical code generation</critical>
</step>

---

## ENHANCEMENT POINT 4: IDENTITY-SPECIFIC VALIDATION

<step n="8" goal="Generate Identity-comprehensive test suite">
<action>Invoke @axon-quality-guardian with Identity test context:

**Identity Test Generation Strategy** (Multi-layer):

**Domain Tests** (tests/Modules/Identity/Domain/):
1. **AxonPrincipal Tests**:
   - Command method tests (50+ existing patterns to follow)
   - Test pattern: Arrange aggregate → Act (invoke command) → Assert Result + State + Events
   - Test all invariants: Service risk, ownership uniqueness, signing exclusivity, etc.
   - Test error paths: Invalid inputs, invariant violations

2. **Owned Entity Tests**:
   - IdentityCredential: Uniqueness, validation
   - WalletOwnership: Status transitions, uniqueness
   - PrincipalChainDefault: Chain uniqueness

3. **Value Object Tests**:
   - Address: Validation, formatting
   - ChainId: Valid chains
   - ProviderType: Valid providers

4. **Domain Event Tests**:
   - Event raised on state changes?
   - Event payload correct?

**Application Tests** (tests/Modules/Identity/Application/):
1. **Command Handler Integration Tests**:
   - Use in-memory database (IdentityTestDbContext)
   - Test: Load → Execute → Assert database state
   - Test idempotency for idempotent commands
   - Test Result<T, Error> success and error paths

2. **Query Handler Tests**:
   - Test read model queries
   - Test ETag generation (GetMyPrincipal)
   - Test conditional GET (304 Not Modified)

3. **Service Unit Tests**:
   - Mock dependencies
   - Test Result<T, Error> paths
   - Test edge cases

**Infrastructure Tests** (tests/Modules/Identity/Infrastructure/):
1. **EF Core Tests**:
   - Owned entity persistence (composite keys)
   - Concurrency control (xmin optimistic concurrency)
   - Partial unique indexes (signing exclusivity)
   - Cascade operations (owned entities follow aggregate)

2. **Service Integration Tests**:
   - Ed25519SignatureVerifier: Real Solana signature verification
   - JwksService: JWKS endpoint mocking
   - DynamicAuthService: JWT validation with mocked JWKS

3. **Repository Tests**:
   - AxonPrincipalWriteRepository: SaveChanges, concurrency
   - AxonPrincipalReadRepository: Query patterns

**E2E Tests** (tests/Modules/Identity/E2E/):
1. **Authentication Flow Tests**:
   - POST /api/v1/auth/exchange → Idempotent principal sync
   - GET /api/v1/auth/me → Current user with ETag
   - Full flow: JWT → Exchange → Me (with If-None-Match)

2. **Wallet Signature Flow Tests**:
   - POST /api/v1/auth/challenge → Generate challenge
   - POST /api/v1/auth/verify → Verify Ed25519 signature
   - Full flow: Challenge → Sign → Verify → Exchange

**Identity-Specific Test Scenarios** (from workflow.yaml):
- ✓ JWT validation with JWKS
- ✓ Ed25519 signature verification (Solana base58)
- ✓ Principal resolution determinism
- ✓ Credential uniqueness enforcement
- ✓ Wallet signing exclusivity
- ✓ Chain default eligibility
- ✓ Auto-revocation logic
- ✓ Owned entity cascade operations
</action>

<template-output section="identity_test_plan">
**Identity Test Plan**

**Test Coverage Breakdown**:
- Domain Tests: [X tests] - Command methods, invariants, events
- Application Tests: [X tests] - Handlers, services
- Infrastructure Tests: [X tests] - EF Core, repositories, external services
- E2E Tests: [X tests] - Authentication flows, wallet flows

**Critical Test Scenarios**:
1. [Test Name]: [What it validates]
2. [Test Name]: [What it validates]
...

**Acceptance Criteria Coverage**:
- AC1: [Test file that validates this]
- AC2: [Test file that validates this]
...
Coverage: 100% (all ACs tested)

**Domain Invariant Testing**:
- Service Risk Constraint: [Test validates]
- Ownership Uniqueness: [Test validates]
- Signing Exclusivity: [Test validates]
- Chain Default Uniqueness: [Test validates]
- Default Eligibility: [Test validates]
- Max Wallets: [Test validates]
Coverage: 100% (6/6 invariants tested)

**Estimated Coverage**: 90%+
</template-output>
</step>

<step n="9" goal="Validate Identity domain invariants">
<action>Run comprehensive invariant validation:

**Invariant 1: Service Risk Constraint**
- Code check: AxonPrincipal.UpdateRiskTier() enforces type check
- Test check: Domain tests validate rejection of High/Medium risk for services
- Result: ✓ Pass / ✗ Fail

**Invariant 2: Ownership Uniqueness**
- Code check: Composite key (PrincipalId, Id) + unique constraint
- DB check: Database migration includes unique constraint
- Test check: Tests validate duplicate rejection
- Result: ✓ Pass / ✗ Fail

**Invariant 3: Signing Exclusivity** (CRITICAL)
- Code check: LinkWalletOwnership() validates via checkConflictFunc
- DB check: Partial unique index on (WalletId) WHERE status=Verified AND accessMode=Signing
- Test check: Tests validate one verified+signing per wallet globally
- Result: ✓ Pass / ✗ Fail

**Invariant 4: Chain Default Uniqueness**
- Code check: SetChainDefault() enforces one per chain per principal
- Test check: Tests validate duplicate chain defaults rejected
- Result: ✓ Pass / ✗ Fail

**Invariant 5: Default Eligibility**
- Code check: CanSetAsDefault() validates verified+signing requirement
- Test check: Tests validate watch-only wallets cannot be defaults
- Result: ✓ Pass / ✗ Fail

**Invariant 6: Max Wallets**
- Code check: IsAtWalletLimit property checks count >= 10
- Test check: Tests validate 11th wallet rejected
- Result: ✓ Pass / ✗ Fail
</action>

<template-output section="identity_invariant_validation">
**Identity Domain Invariant Validation**

**Validation Results**:
- ✓ Service Risk Constraint
- ✓ Ownership Uniqueness
- ✓ Signing Exclusivity (CRITICAL)
- ✓ Chain Default Uniqueness
- ✓ Default Eligibility
- ✓ Max Wallets

**Invariants Preserved**: 6/6 (100%) ✅

**Code Review**:
- All invariants enforced in domain code
- Database constraints align with business rules
- Test coverage validates all invariants

**Risk Assessment**:
- Invariant violations: None detected
- Critical invariant (signing exclusivity): Validated ✓
</template-output>
</step>

---

## COMPLETION: IDENTITY WORKFLOW ENHANCEMENT COMPLETE

<step n="10" goal="Generate Identity-specific implementation summary">
<template-output section="identity_completion_summary">
**Identity Workflow Enhancement Complete** ✅

**Story**: {{story_title}} ({{story_id}})
**Module**: Identity
**Subdomain**: {{identity_subdomain}}

**Identity-Specific Deliverables**:
- ✓ 7 Identity docs loaded and validated
- ✓ Subdomain classification performed
- ✓ Codebase discovery completed (reuse recommendations)
- ✓ Library validation performed (Dynamic.xyz, NSec, etc.)
- ✓ Pattern compliance validated (95%+ score)
- ✓ Domain invariants preserved (6/6)
- ✓ Identity-comprehensive test suite generated
- ✓ Owned entity patterns validated

**Pattern Compliance**:
- Result<T, Error>: ✓
- StrongId<T>: ✓
- CQRS: ✓
- Domain Events: ✓
- Owned Entities: ✓
- Domain Invariants: ✓ (6/6)

**Quality Metrics**:
- Test Coverage: 90%+
- AC Coverage: 100%
- Invariants Preserved: 100%
- Build Success: 100%
- Doc Sync: Zero drift

**Codebase Impact**:
- Domain: [Files modified/created]
- Application: [Files modified/created]
- Infrastructure: [Files modified/created]
- API: [Files modified/created]
- Tests: [Test files created]

**Next Steps**:
- Review identity-specific implementation
- Run build + tests
- Validate all 6 domain invariants
- Commit with Identity context in message
</template-output>

<critical>This summary is appended to the base story-implementation completion summary</critical>
</step>

</workflow>