# Identity Workflow - Module-Specific Enhancement

**Tier**: 3 (Module-Specialized)
**Status**: ✅ Complete
**Completion Date**: 2025-09-30
**Files**: 3 (workflow.yaml, instructions.md, checklist.md)
**Total Lines**: 1,529

---

## Purpose

Identity module enhancement for **story-implementation** workflow. Adds Identity-specific expertise for:
- **Authentication**: JWT validation (JWKS, Dynamic.xyz), wallet signatures (Ed25519)
- **Wallet Management**: Ownership verification, multi-chain support, Ed25519 signatures
- **Principal Resolution**: Deterministic identity mapping, credential uniqueness
- **Credential Management**: OAuth/JWT, multi-provider support

---

## When to Use

This workflow is **automatically invoked** by `story-orchestrator` when:
```
Story Type: Feature
Module: Identity
```

**Routing**:
```
story-orchestrator detects "module: Identity"
  → routes to identity-workflow
  → identity-workflow extends story-implementation
  → adds Identity-specific context at 4 enhancement points
```

---

## What It Does

### Extends story-implementation with Identity Expertise

**Enhancement Point 1**: Load 7 Identity docs + classify subdomain
- 5 Identity module docs (INDEX, domain-model, authentication, api-contracts, database-schema)
- 1 Identity library doc (dynamic_auth)
- 1 Integration doc
- Classify into 1 of 4 subdomains (authentication, wallet, principal, credential)

**Enhancement Point 2**: Identity-specific pre-flight validation
- **Archaeologist**: Search AxonPrincipal aggregate (50+ command methods), owned entities, services
- **Library Sage**: Validate Dynamic.xyz, NSec.Cryptography, Microsoft.IdentityModel.Tokens
- **Doc Oracle**: Validate 6 domain invariants, owned entity patterns, EF Core configuration

**Enhancement Point 3**: Identity-specific implementation guidance
- AxonPrincipal modification patterns (Commands.cs, Queries.cs)
- Owned entity patterns (composite keys, partial indexes)
- Service patterns (Ed25519SignatureVerifier, JwksService, PrincipalResolutionService)
- CQRS handler patterns (ExchangeCredential, VerifyWalletSignature, etc.)

**Enhancement Point 4**: Identity-comprehensive testing
- Domain tests (aggregate, invariants, owned entities)
- Application tests (handlers, services, idempotency)
- Infrastructure tests (EF Core, concurrency, cryptography)
- E2E tests (authentication flows, wallet flows)
- 8 Identity-specific test scenarios

---

## Key Features

### 🔐 Domain Invariants (6 Critical Rules)

1. **Service Risk Constraint**: Service principals must have `RiskTier.Low`
2. **Ownership Uniqueness**: One principal-wallet-accessMode tuple
3. **Signing Exclusivity** (CRITICAL): One verified+signing ownership per wallet **globally**
4. **Chain Default Uniqueness**: One default wallet per chain per principal
5. **Default Eligibility**: Only verified+signing wallets can be chain defaults
6. **Max Wallets**: 10 wallet ownerships per principal

All 6 invariants are:
- ✅ Documented in workflow.yaml
- ✅ Enforced in domain code
- ✅ Validated in checklist.md
- ✅ Tested in comprehensive test suite

### 🏗️ Codebase Patterns

**AxonPrincipal Aggregate**:
- 3 partial class files (main .cs, Commands.cs, Queries.cs)
- 50+ command methods (reuse patterns identified by Archaeologist)
- Result<T, Error> return type for all operations
- Domain events raised on state changes

**Owned Entities** (EF Core OwnsMany):
- IdentityCredential, WalletOwnership, PrincipalChainDefault
- Composite keys: (PrincipalId, Id)
- Partial unique indexes (signing exclusivity)
- Single concurrency token on aggregate root (xmin)
- No independent DbSet

**Services**:
- Ed25519SignatureVerifier (Solana signatures)
- JwksService (JWT validation)
- DynamicAuthService (Dynamic.xyz integration)
- PrincipalResolutionService (deterministic identity mapping)

### 📚 Library Stack

1. **Dynamic.xyz SDK** - JWT validation, wallet claims
2. **NSec.Cryptography** - Ed25519 signatures (Solana)
3. **Microsoft.IdentityModel.Tokens** - JWKS handling
4. **SimpleBase** - Base58 encoding (Solana addresses)

---

## File Structure

```
identity-workflow/
├── workflow.yaml           # 308 lines - Configuration, metadata, Identity context
├── instructions.md         # 723 lines - 10-step enhancement instructions
├── checklist.md            # 498 lines - Comprehensive validation checklist
└── README.md              # This file
```

---

## Usage

### Option 1: Via Story Orchestrator (Recommended)

```bash
# Story file has "Module: Identity"
@axon-story-orchestrator implement-story story-NNN.md

# story-orchestrator automatically routes to identity-workflow
# identity-workflow extends story-implementation with Identity context
```

### Option 2: Direct Invocation (Advanced)

```bash
# Manually invoke identity-workflow
@bmad-builder workflow identity-workflow

# Provide story file when prompted
```

---

## Expected Inputs

### Story File (Required)
```markdown
# Story: Add multi-factor authentication

**Story ID**: AXON-123
**Module**: Identity  # ← Triggers identity-workflow
**Type**: Feature
...
```

### Tech Spec (Optional)
- BMM tech-spec reference (if from planning phase)
- Enhances story understanding

---

## Expected Outputs

### Code Artifacts
- **Domain**: AxonPrincipal command methods, owned entity modifications, domain events
- **Application**: CQRS handlers (commands/queries), services
- **Infrastructure**: Service implementations, EF Core configurations, migrations
- **API**: FastEndpoints (authentication endpoints)

### Test Artifacts
- **Domain Tests**: Aggregate tests, invariant tests, owned entity tests
- **Application Tests**: Handler integration tests, service tests, idempotency tests
- **Infrastructure Tests**: EF Core tests, concurrency tests, cryptography tests
- **E2E Tests**: Authentication flow tests, wallet flow tests

### Documentation Artifacts
- **Decision Log**: YAML format (Docs/PROCESS/active-stories/{story-id}/decisions.yaml)
- **Implementation Log**: Story understanding, pre-flight, implementation plan
- **Updated Docs**: Identity module docs synchronized (zero drift)

---

## Quality Gates

### Inherited from story-implementation
- ✅ Pattern compliance ≥ 95%
- ✅ Test coverage ≥ 90%
- ✅ AC coverage = 100%
- ✅ Build success = 100%
- ✅ Doc sync = Zero drift

### Identity-Specific
- ✅ Domain invariants preserved = 100% (6/6)
- ✅ Owned entity patterns correct = 100%
- ✅ EF Core configuration correct = 100%
- ✅ Identity test scenarios complete = 100% (8/8)

---

## Critical Validations

### 🚨 Signing Exclusivity (Most Critical)

```yaml
Invariant: One verified+signing ownership per wallet globally
Enforcement:
  - Code: LinkWalletOwnership() validates via checkConflictFunc
  - Database: Partial unique index (WalletId) WHERE status=Verified AND accessMode=Signing
  - Tests: Global uniqueness test across all principals
Critical Check: Verify index exists in migration ✓
```

### 🚨 Owned Entity Access

```yaml
Pattern: EF Core OwnsMany with composite keys
Rules:
  - No independent DbSet<T>
  - Access only via AxonPrincipal aggregate
  - Composite key: (PrincipalId, Id)
  - Single concurrency token (xmin on aggregate root)
Critical Check: No code bypasses aggregate ✓
```

### 🚨 Result<T> Pattern

```yaml
Pattern: No exceptions in domain layer
Rules:
  - All AxonPrincipal methods return Result<T, Error>
  - All service methods return Result<T, Error>
  - All handlers return Result<TResponse, Error>
  - Error types: IdentityDomainErrors, WalletDomainErrors, AuthErrors
Critical Check: No exceptions thrown in domain ✓
```

---

## Subdomain Classification

Stories are classified into 1 of 4 Identity subdomains:

### 1. Authentication Subdomain
**Keywords**: JWT, token, JWKS, Dynamic.xyz, authentication, login
**Services**: JwksService, DynamicAuthService, DynamicClaimNormalizer
**Tests**: JWT validation, JWKS refresh, Dynamic.xyz integration

### 2. Wallet Management Subdomain
**Keywords**: wallet, signature, Ed25519, Solana, verification, challenge
**Services**: Ed25519SignatureVerifier, WalletVerificationService, AddressNormalizationService
**Tests**: Ed25519 signatures, challenge-response, wallet ownership

### 3. Principal Resolution Subdomain
**Keywords**: principal, identity, resolution, deterministic, mapping
**Services**: PrincipalResolutionService
**Tests**: Resolution determinism, credential uniqueness

### 4. Credential Management Subdomain
**Keywords**: credential, OAuth, provider, multi-provider, sync
**Entities**: IdentityCredential (owned entity)
**Tests**: Credential uniqueness, provider management, idempotent sync

---

## Success Metrics

### Implementation Efficiency
- **Discovery-First**: Archaeologist finds 50+ existing AxonPrincipal methods to pattern-match
- **Library-First**: Library Sage validates 4 libraries (Dynamic.xyz, NSec, IdentityModel, SimpleBase)
- **Reuse Score**: High (many existing patterns to follow)

### Code Quality
- **Pattern Compliance**: 95%+ (Result<T>, StrongId<T>, CQRS, Events, Owned Entities)
- **Test Coverage**: 90%+ (Domain, Application, Infrastructure, E2E)
- **Invariant Preservation**: 100% (6/6 invariants enforced and tested)

### Documentation Quality
- **Doc Loading**: 7 Identity docs (comprehensive module knowledge)
- **Doc Sync**: Zero drift (code changes → doc updates)
- **Decision Capture**: YAML learning log

---

## Troubleshooting

### Issue: Signing exclusivity index missing
**Symptom**: Tests fail on wallet ownership linking
**Cause**: Partial unique index not in migration
**Solution**: Check Infrastructure/Persistence/Configurations/WalletOwnershipConfiguration.cs and migration

### Issue: Owned entity accessed directly
**Symptom**: EF Core error or concurrency issues
**Cause**: Code trying to access DbSet<IdentityCredential> directly
**Solution**: Access owned entities only via AxonPrincipal aggregate

### Issue: Domain invariant violated
**Symptom**: Business rule bypassed
**Cause**: New code doesn't validate invariants
**Solution**: Review checklist.md "Domain Invariants" section, ensure validation before mutation

### Issue: Test coverage < 90%
**Symptom**: Quality gate fails
**Cause**: Missing test scenarios
**Solution**: Review checklist.md "Identity Test Coverage" section, add missing tests

---

## Related Workflows

- **story-orchestrator**: Master router (invokes identity-workflow)
- **story-implementation**: Base workflow (extended by identity-workflow)
- **story-refactoring**: Refactoring workflow (can also be enhanced with Identity context)
- **story-bugfix**: Bugfix workflow (can also be enhanced with Identity context)

---

## Documentation References

### Identity Module Docs
- `Docs/ENGINEERING/modules/identity/00-INDEX.md` - Module overview
- `Docs/ENGINEERING/modules/identity/01-domain-model.md` - Aggregate, owned entities, invariants
- `Docs/ENGINEERING/modules/identity/03-authentication.md` - JWT + wallet auth flows
- `Docs/ENGINEERING/modules/identity/05-api-contracts.md` - REST endpoints
- `Docs/ENGINEERING/modules/identity/06-database-schema.md` - EF Core patterns

### Library Docs
- `Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md` - Dynamic.xyz SDK

### Architecture Docs
- `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` - Core patterns (80% coverage)
- `Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md` - ADR catalog

---

## Maintenance

### When to Update This Workflow

1. **New Identity subdomain**: Add to `identity_subdomains` in workflow.yaml
2. **New domain invariant**: Add to `identity_domain_invariants` in workflow.yaml + checklist.md
3. **New service**: Add to `identity_code_patterns` in workflow.yaml
4. **New library**: Add to `identity_library_docs` in workflow.yaml + checklist.md
5. **Pattern changes**: Update instructions.md and checklist.md

### Version History

- **v1.0** (2025-09-30): Initial creation - deeply code-grounded Identity workflow
  - 7 Identity docs loaded
  - 4 subdomains classified
  - 6 domain invariants validated
  - 50+ AxonPrincipal methods pattern-matched
  - Comprehensive test generation (Domain, Application, Infrastructure, E2E)

---

**Created**: 2025-09-30
**Last Updated**: 2025-09-30
**Maintained By**: Axon Module / BMad Builder
**Status**: ✅ Production-Ready