# Identity Workflow - Validation Checklist

**Purpose**: Validate Identity-specific enhancement to story-implementation workflow

**Module**: Identity
**Workflow Type**: Module-specific enhancement
**Inherits**: story-implementation base validation + Identity-specific checks

---

## 📋 Identity Documentation Loading

- [ ] All 7 Identity docs loaded in memory
  - [ ] modules/identity/00-INDEX.md (module overview, metrics)
  - [ ] modules/identity/01-domain-model.md (aggregate, owned entities, invariants)
  - [ ] modules/identity/03-authentication.md (JWT flows, wallet flows)
  - [ ] modules/identity/05-api-contracts.md (REST endpoints)
  - [ ] modules/identity/06-database-schema.md (EF Core patterns)
  - [ ] libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md (Dynamic.xyz)
  - [ ] integrations/00-INDEX.md (integration patterns)

---

## 🎯 Identity Subdomain Classification

- [ ] Story mapped to primary Identity subdomain
  - [ ] Authentication subdomain (JWT, JWKS, Dynamic.xyz)
  - [ ] Wallet Management subdomain (Ed25519, signatures, verification)
  - [ ] Principal Resolution subdomain (deterministic mapping)
  - [ ] Credential Management subdomain (OAuth/JWT, multi-provider)
- [ ] Relevant services identified for subdomain
- [ ] Relevant patterns identified for subdomain
- [ ] Test scenarios identified for subdomain

---

## 🔍 Identity Codebase Discovery

### AxonPrincipal Aggregate Discovery
- [ ] Searched AxonPrincipal.Commands.cs for similar methods (50+ existing)
- [ ] Searched AxonPrincipal.Queries.cs for query patterns
- [ ] Identified reusable command methods (REUSE)
- [ ] Identified extendable command methods (EXTEND)
- [ ] Identified new command methods needed (CREATE)

### Owned Entity Discovery
- [ ] Searched IdentityCredential.cs for existing fields/validations
- [ ] Searched WalletOwnership.cs for existing fields/validations
- [ ] Searched PrincipalChainDefault.cs for existing patterns
- [ ] Verified composite key patterns: (PrincipalId, Id)
- [ ] Verified partial unique indexes (signing exclusivity)

### Service Discovery
- [ ] Searched Ed25519SignatureVerifier for signature verification patterns
- [ ] Searched JwksService for JWT validation patterns
- [ ] Searched DynamicAuthService for claim parsing patterns
- [ ] Searched PrincipalResolutionService for deterministic resolution
- [ ] Identified services to extend vs create new

### CQRS Handler Discovery
- [ ] Searched existing command handlers for patterns
- [ ] Searched existing query handlers for ETag patterns
- [ ] Identified handler base classes to inherit from
- [ ] Identified idempotency patterns (for idempotent commands)

### Test Discovery
- [ ] Searched Domain tests for AxonPrincipal test patterns
- [ ] Searched Application tests for handler integration patterns
- [ ] Searched Infrastructure tests for EF Core patterns
- [ ] Searched E2E tests for authentication flow patterns

---

## 📚 Identity Library Validation

### Dynamic.xyz SDK
- [ ] Validated Dynamic.xyz library capabilities match requirements
- [ ] Reviewed DynamicAuthService usage patterns
- [ ] Reviewed DynamicClaimNormalizer usage patterns
- [ ] Verified JWKS caching patterns
- [ ] Confirmed JWT validation approach

### NSec.Cryptography (Ed25519)
- [ ] Validated NSec.Cryptography capabilities match requirements
- [ ] Reviewed Ed25519SignatureVerifier usage patterns
- [ ] Verified Solana base58 encoding/decoding patterns
- [ ] Confirmed signature verification approach

### Microsoft.IdentityModel.Tokens
- [ ] Validated Microsoft.IdentityModel.Tokens capabilities
- [ ] Reviewed JwksService JWKS handling patterns
- [ ] Verified token validation parameter patterns

### SimpleBase (Base58)
- [ ] Validated SimpleBase library for Solana address handling
- [ ] Verified Base58.Bitcoin.Decode/Encode usage patterns

### Library Decision (4-Factor Scoring)
- [ ] Capability match scored for each library
- [ ] Complexity reduction assessed
- [ ] Maintenance burden evaluated
- [ ] Integration cost estimated
- [ ] Library-first vs manual decision documented

---

## ✅ Identity Pattern Validation

### Result<T, Error> Pattern
- [ ] All AxonPrincipal command methods return Result<T, Error>
- [ ] All service methods return Result<T, Error>
- [ ] All handlers return Result<TResponse, Error>
- [ ] Error types used: IdentityDomainErrors, WalletDomainErrors, AuthErrors
- [ ] No exceptions thrown in domain layer

### StrongId<T> Pattern
- [ ] AxonUserId used for aggregate root ID
- [ ] IdentityCredentialId, WalletOwnershipId, PrincipalChainDefaultId used for owned entities
- [ ] Vogen value objects used: Address, ChainId, ProviderType, ProofType
- [ ] No primitive obsession (no string/Guid for IDs)

### CQRS Pattern
- [ ] Commands inherit from IdentityBaseCommand or IdentityIdempotentCommand
- [ ] Queries inherit from IdentityBaseQuery
- [ ] Handlers inherit from BaseIdentityCommandHandler or BaseIdentityQueryHandler
- [ ] Command/Query separation maintained
- [ ] No business logic in query handlers

### Domain Events Pattern
- [ ] Events raised on aggregate state changes
- [ ] PrincipalChangedEvent raised for risk tier changes
- [ ] CredentialChangedEvent raised for credential operations
- [ ] OwnershipChangedEvent raised for wallet operations
- [ ] Events published via MediatR pipeline
- [ ] No business logic in event handlers

### Owned Entity Pattern (Identity-Specific)
- [ ] Composite keys maintained: (PrincipalId, Id)
- [ ] OwnsMany configured in AxonPrincipalConfiguration
- [ ] No independent DbSet<T> for owned entities
- [ ] Single concurrency token on aggregate root (xmin)
- [ ] Partial unique indexes configured (signing exclusivity)
- [ ] Owned entities accessed only via aggregate root

---

## 🔒 Identity Domain Invariants Preservation

### Invariant 1: Service Risk Constraint
- [ ] Service principals must have RiskTier.Low
- [ ] Enforced in AxonPrincipal.UpdateRiskTier()
- [ ] Test validates rejection of High/Medium risk for services
- [ ] Code review: Invariant enforced before state mutation

### Invariant 2: Ownership Uniqueness
- [ ] One principal-wallet-accessMode tuple
- [ ] Enforced via composite key: (PrincipalId, Id)
- [ ] Unique constraint in database
- [ ] Test validates duplicate rejection
- [ ] Code review: Database schema enforces uniqueness

### Invariant 3: Signing Exclusivity (CRITICAL)
- [ ] One verified+signing ownership per wallet globally
- [ ] Enforced via checkConflictFunc in LinkWalletOwnership()
- [ ] Partial unique index: (WalletId) WHERE status=Verified AND accessMode=Signing
- [ ] Test validates global uniqueness across all principals
- [ ] Code review: Validation BEFORE wallet link, database enforces uniqueness
- [ ] **CRITICAL CHECK**: Index exists in migration

### Invariant 4: Chain Default Uniqueness
- [ ] One default wallet per chain per principal
- [ ] Enforced in SetChainDefault()
- [ ] Test validates duplicate chain defaults rejected
- [ ] Code review: Uniqueness checked before setting default

### Invariant 5: Default Eligibility
- [ ] Only verified+signing wallets can be chain defaults
- [ ] Enforced via CanSetAsDefault() query
- [ ] Test validates watch-only wallets cannot be defaults
- [ ] Code review: Eligibility checked before default assignment

### Invariant 6: Max Wallets
- [ ] 10 wallet ownerships per principal maximum
- [ ] Enforced via IsAtWalletLimit property
- [ ] Test validates 11th wallet rejected
- [ ] Code review: Limit checked in LinkWalletOwnership()

### Overall Invariant Assessment
- [ ] All 6 invariants preserved: 100% (6/6)
- [ ] No invariant violations detected
- [ ] Code enforces invariants before state mutation
- [ ] Database constraints align with business rules
- [ ] Test coverage validates all invariants

---

## 🧪 Identity Test Coverage

### Domain Tests (tests/Modules/Identity/Domain/)
- [ ] AxonPrincipal command method tests created/updated
- [ ] Test pattern: Arrange → Act → Assert Result + State + Events
- [ ] All 6 domain invariants tested
- [ ] Error paths tested (invalid inputs, invariant violations)
- [ ] Owned entity tests (IdentityCredential, WalletOwnership, PrincipalChainDefault)
- [ ] Value object validation tests (Address, ChainId, etc.)
- [ ] Domain event tests (events raised correctly)

### Application Tests (tests/Modules/Identity/Application/)
- [ ] Command handler integration tests with in-memory database
- [ ] Query handler tests (including ETag support for GetMyPrincipal)
- [ ] Idempotency tests for idempotent commands
- [ ] Result<T, Error> success and error paths tested
- [ ] Service unit tests with mocked dependencies

### Infrastructure Tests (tests/Modules/Identity/Infrastructure/)
- [ ] EF Core tests (owned entity persistence, composite keys)
- [ ] Concurrency control tests (xmin optimistic concurrency)
- [ ] Partial unique index tests (signing exclusivity)
- [ ] Cascade operation tests (owned entities follow aggregate)
- [ ] Service integration tests (Ed25519, JWKS, Dynamic.xyz)
- [ ] Repository tests (SaveChanges, query patterns)

### E2E Tests (tests/Modules/Identity/E2E/)
- [ ] Authentication flow tests (POST /api/v1/auth/exchange → GET /api/v1/auth/me)
- [ ] ETag conditional GET tests (If-None-Match → 304 Not Modified)
- [ ] Wallet signature flow tests (Challenge → Sign → Verify)
- [ ] Full integration tests across all layers

### Identity-Specific Test Scenarios (from workflow.yaml)
- [ ] JWT validation with JWKS
- [ ] Ed25519 signature verification (Solana base58)
- [ ] Principal resolution determinism
- [ ] Credential uniqueness enforcement
- [ ] Wallet signing exclusivity (CRITICAL test)
- [ ] Chain default eligibility
- [ ] Auto-revocation logic
- [ ] Owned entity cascade operations

### Test Coverage Metrics
- [ ] Overall test coverage ≥ 90%
- [ ] Acceptance criteria coverage = 100%
- [ ] Domain invariant test coverage = 100% (6/6)
- [ ] Critical paths covered (authentication flows, wallet flows)
- [ ] Edge cases covered (error scenarios, boundary conditions)

---

## 🏗️ Identity Code Structure

### Bottom-Up Layering (Axon Mandated)
- [ ] Domain layer implemented first
- [ ] Application layer implemented second
- [ ] Infrastructure layer implemented third
- [ ] API layer implemented last
- [ ] No layer skipping occurred

### Domain Layer (src/Modules/Identity/Domain/)
- [ ] AxonPrincipal aggregate modifications follow patterns (Commands.cs, Queries.cs, main .cs)
- [ ] Owned entity modifications maintain composite keys
- [ ] Domain events created/updated correctly
- [ ] Error types defined (IdentityDomainErrors, WalletDomainErrors)
- [ ] Value objects follow Vogen pattern

### Application Layer (src/Modules/Identity/Application/)
- [ ] Commands inherit from IdentityBaseCommand or IdentityIdempotentCommand
- [ ] Queries inherit from IdentityBaseQuery
- [ ] Handlers inherit from base handler classes
- [ ] Services implement interfaces from Contracts/
- [ ] DTOs created for request/response mapping

### Infrastructure Layer (src/Modules/Identity/Infrastructure/)
- [ ] Services implement Application/Contracts interfaces
- [ ] EF Core configurations updated (OwnsMany, composite keys, indexes)
- [ ] Repositories follow existing patterns
- [ ] Database migrations created if schema changes
- [ ] External service integrations (Dynamic.xyz, NSec) follow patterns

### API Layer (src/Api/Endpoints/V1/Auth/)
- [ ] FastEndpoints created following existing patterns
- [ ] Request/Response mapping implemented
- [ ] FluentValidation for request validation
- [ ] Authentication configured (AllowAnonymous vs Authenticated)
- [ ] ETag support implemented if needed (conditional GET)

---

## 🗄️ Identity Database & EF Core

### EF Core Configuration
- [ ] OwnsMany configured for owned entities
- [ ] Composite keys configured: (PrincipalId, Id)
- [ ] Partial unique indexes configured where needed
- [ ] Single concurrency token strategy (xmin on aggregate root)
- [ ] No independent DbSet for owned entities
- [ ] Navigation properties configured correctly

### Database Migration
- [ ] Migration created if schema changes
- [ ] Migration includes owned entity tables/columns
- [ ] Migration includes composite key constraints
- [ ] Migration includes unique indexes (including partial indexes)
- [ ] Migration includes foreign key constraints
- [ ] Migration tested (Up and Down)

### Concurrency Strategy
- [ ] xmin system column used for optimistic concurrency
- [ ] Concurrency token only on aggregate root (AxonPrincipal)
- [ ] Owned entities protected by aggregate's concurrency token
- [ ] Concurrency tests validate optimistic locking

---

## 📊 Identity Implementation Quality

### Pattern Compliance Score
- [ ] Result<T, Error> compliance: 100%
- [ ] StrongId<T> compliance: 100%
- [ ] CQRS compliance: 100%
- [ ] Domain Events compliance: 100%
- [ ] Owned Entity patterns: 100%
- [ ] Domain Invariants preserved: 100% (6/6)
- [ ] **Overall Pattern Compliance**: ≥ 95%

### Code Quality
- [ ] No code smells (long methods, god classes, etc.)
- [ ] Proper async/await usage
- [ ] Logging at appropriate levels (ILogger)
- [ ] Exception handling follows Result<T> pattern (no exceptions in domain)
- [ ] Comments only where business logic is complex
- [ ] XML documentation on public APIs

### Build Success
- [ ] dotnet build succeeds with zero errors
- [ ] dotnet build succeeds with zero warnings (warnings-as-errors enabled)
- [ ] No analyzer violations
- [ ] No nullable reference warnings

### Test Execution
- [ ] dotnet test succeeds for all Identity tests
- [ ] Domain tests pass: 100%
- [ ] Application tests pass: 100%
- [ ] Infrastructure tests pass: 100%
- [ ] E2E tests pass: 100%
- [ ] Test coverage ≥ 90%

---

## 📝 Documentation Sync (Zero Drift)

### Documentation Updates Required
- [ ] Identity module docs updated if behavior changed
- [ ] Authentication flow docs updated if JWT/wallet flows changed
- [ ] API contract docs updated if endpoints changed
- [ ] Database schema docs updated if schema changed
- [ ] Integration docs updated if Dynamic.xyz integration changed

### Documentation Validation
- [ ] No documentation drift detected (code matches docs)
- [ ] New features documented in appropriate docs
- [ ] Updated features have corresponding doc updates
- [ ] Decision log captured in YAML format

---

## ✅ Final Identity Validation

### Identity-Specific Completeness
- [ ] All 7 Identity docs were loaded and consulted
- [ ] Identity subdomain correctly classified
- [ ] Identity-specific patterns followed (owned entities, invariants)
- [ ] Identity-specific libraries used correctly (Dynamic.xyz, NSec, etc.)
- [ ] Identity-specific tests comprehensive (JWT, Ed25519, resolution, etc.)
- [ ] All 6 domain invariants preserved and tested

### Acceptance Criteria
- [ ] Every acceptance criterion has corresponding test(s)
- [ ] All acceptance criteria tests pass
- [ ] Acceptance criteria coverage = 100%

### Quality Gates (inherited from story-implementation)
- [ ] Pattern compliance ≥ 95%
- [ ] Test coverage ≥ 90%
- [ ] AC coverage = 100%
- [ ] Build success = 100%
- [ ] Doc sync = Zero drift

### Identity Quality Gates (additional)
- [ ] Domain invariants preserved = 100% (6/6)
- [ ] Owned entity patterns correct = 100%
- [ ] EF Core configuration correct = 100%
- [ ] Identity test scenarios complete = 100% (8/8)

---

## 🚨 Critical Identity Checks (MUST PASS)

### Critical Check 1: Signing Exclusivity Index
- [ ] **VERIFY**: Partial unique index exists in database migration
- [ ] **VERIFY**: Index definition: (WalletId) WHERE status=Verified AND accessMode=Signing
- [ ] **VERIFY**: Test validates global uniqueness (one wallet can't have two verified+signing ownerships)
- [ ] **CRITICAL**: This is the most important Identity invariant

### Critical Check 2: Owned Entity Access
- [ ] **VERIFY**: No code accesses owned entities via DbSet directly
- [ ] **VERIFY**: All owned entity access goes through AxonPrincipal aggregate
- [ ] **VERIFY**: EF Core configuration uses OwnsMany (not regular entity)

### Critical Check 3: Concurrency Token
- [ ] **VERIFY**: Only AxonPrincipal has concurrency token (xmin)
- [ ] **VERIFY**: Owned entities do NOT have independent concurrency tokens
- [ ] **VERIFY**: Tests validate concurrency conflicts caught at aggregate level

### Critical Check 4: Result<T> Pattern
- [ ] **VERIFY**: No exceptions thrown in domain layer (all use Result<T, Error>)
- [ ] **VERIFY**: All AxonPrincipal command methods return Result<T, Error>
- [ ] **VERIFY**: Tests validate error paths return Result.Failure (not exceptions)

---

## 🎯 Workflow Enhancement Success Criteria

### Identity Workflow Enhancement Complete
- [ ] All checklist items above completed
- [ ] Identity-specific validation passed
- [ ] Base story-implementation validation inherited and passed
- [ ] Ready for commit

### Commit Readiness
- [ ] All tests passing (Domain, Application, Infrastructure, E2E)
- [ ] Build succeeds with zero warnings
- [ ] All 6 domain invariants preserved
- [ ] Pattern compliance ≥ 95%
- [ ] Documentation synchronized (zero drift)
- [ ] Decision log captured

---

## 📋 Issues Found

**List any issues discovered during validation**:

1. Issue: [Description]
   - Severity: [Low/Medium/High/Critical]
   - Location: [File:Line]
   - Remediation: [Action needed]

2. Issue: [Description]
   - Severity: [Low/Medium/High/Critical]
   - Location: [File:Line]
   - Remediation: [Action needed]

---

**Validation Completed By**: _______________
**Date**: _______________
**Overall Status**: ✅ Pass / ⚠️ Pass with Issues / ❌ Fail