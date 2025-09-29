# Identity Module

**User authentication, authorization, and wallet management.**

---

## Purpose

The Identity module manages user principals, wallet-based authentication, and multi-chain identity resolution. It integrates with Dynamic.xyz for JWT validation and provides the foundation for all authenticated operations in Axon.

---

## Key Responsibilities

- **Principal Management**: Create and manage AxonPrincipal aggregates (humans and services)
- **Authentication**: JWT validation (Dynamic.xyz) and wallet signature verification
- **Wallet Ownership**: Link/verify wallets with cryptographic proofs
- **Multi-Chain Support**: Per-principal default wallets for each blockchain
- **Identity Resolution**: Deterministic principal resolution from credentials

---

## Module Dependencies

### External Dependencies
- **Dynamic.xyz**: JWT validation via JWKS, wallet claim normalization
- **Ed25519 Cryptography**: Solana signature verification (NSec.Cryptography)

### Internal Dependencies
- **None**: Identity is a foundational module with no internal module dependencies

---

## Source Code Navigation

### Domain Layer (`src/Modules/Identity/Domain/`)
```
Aggregates/
  AxonPrincipal/
    AxonPrincipal.cs                 // Aggregate root (partial class)
    AxonPrincipal.Commands.cs        // Command methods (50+ business operations)
    AxonPrincipal.Queries.cs         // Query methods
  Wallet/
    Wallet.cs                        // Wallet verification aggregate

Entities/
  IdentityCredential.cs              // OAuth/JWT credential (owned entity)
  WalletOwnership.cs                 // Wallet ownership proof (owned entity)
  PrincipalChainDefault.cs           // Per-chain default wallet (owned entity)

ValueObjects/
  Address.cs                         // Blockchain address (Vogen)
  ChainId.cs                         // Chain identifier (Vogen)
  ProviderType.cs                    // Auth provider type (Vogen)
  ProofType.cs                       // Cryptographic proof type (Vogen)

Enums/
  PrincipalType.cs                   // Human | Service
  RiskTier.cs                        // Low | Medium | High
  AccessMode.cs                      // Signing | WatchOnly
  OwnershipStatus.cs                 // Pending | Verified | Revoked
  VerificationSource.cs              // DynamicAttested | ManualVerified | SiwsVerified

Events/
  PrincipalChangedEvent.cs           // Risk tier, chain default changes
  CredentialChangedEvent.cs          // Credential lifecycle
  OwnershipChangedEvent.cs           // Wallet link/verify/revoke
  WalletChangedEvent.cs              // Wallet-level changes

Errors/
  IdentityDomainErrors.cs            // Domain error codes
  WalletDomainErrors.cs              // Wallet error codes
```

### Application Layer (`src/Modules/Identity/Application/`)
```
Commands/
  ExchangeCredential/                // Sync Dynamic JWT → Axon principal
    ExchangeCredentialCommand.cs
    ExchangeCredentialHandler.cs
  GenerateChallenge/                 // Create wallet signature challenge
    GenerateChallengeCommand.cs
    GenerateChallengeHandler.cs
  VerifyWalletSignature/             // Verify Solana Ed25519 signatures
    VerifyWalletSignatureCommand.cs
    VerifyWalletSignatureHandler.cs
  RefreshToken/                      // Token refresh flow
    RefreshTokenCommand.cs
    RefreshTokenHandler.cs

Queries/
  GetMyPrincipal/                    // Current user snapshot with ETag
    GetMyPrincipalQuery.cs
    GetMyPrincipalHandler.cs

DTOs/
  Exchange/                          // Exchange command DTOs
  Responses/                         // Response DTOs

Services/
  PrincipalResolutionService.cs      // Deterministic identity resolution
```

### Infrastructure Layer (`src/Modules/Identity/Infrastructure/`)
```
Persistence/
  Configurations/
    AxonPrincipalConfiguration.cs    // EF Core owned entity configuration
  Repositories/
    AxonPrincipalWriteRepository.cs  // Aggregate persistence
    AxonPrincipalReadRepository.cs   // Read model queries
  DbContexts/
    IdentityWriteDbContext.cs        // Write model context
    IdentityReadDbContext.cs         // Read model context
  Migrations/                        // EF Core migrations

Services/
  Ed25519SignatureVerifier.cs        // Solana signature verification
  WalletVerificationService.cs       // Multi-chain verification orchestration
  PrincipalResolutionService.cs      // Identity resolution logic

ExternalServices/
  DynamicXyz/
    DynamicApiClient.cs              // Dynamic.xyz API client
  DynamicAuthService.cs              // JWT validation and claim parsing
  JwksService.cs                     // JSON Web Key Set validation
```

### API Layer (`src/Api/Endpoints/V1/Auth/`)
```
Commands/
  ExchangeEndpoint.cs                // POST /api/v1/auth/exchange
  ChallengeEndpoint.cs               // POST /api/v1/auth/challenge
  VerifySignatureEndpoint.cs         // POST /api/v1/auth/verify
  RefreshEndpoint.cs                 // POST /api/v1/auth/refresh

Queries/
  MeEndpoint.cs                      // GET /api/v1/auth/me
```

---

## Public Contracts

### Commands (CQRS)
- `ExchangeCredentialCommand` - Idempotent principal sync from JWT
- `GenerateChallengeCommand` - Wallet challenge generation
- `VerifyWalletSignatureCommand` - Wallet signature verification
- `RefreshTokenCommand` - Token refresh

### Queries (CQRS)
- `GetMyPrincipalQuery` - Current principal with wallets and defaults (ETag support)

### Domain Events
- `PrincipalChangedEvent` - Principal state mutation
- `CredentialChangedEvent` - Credential added/updated/revoked
- `OwnershipChangedEvent` - Wallet linked/unlinked/verified
- `WalletChangedEvent` - Wallet-level changes

---

## Documentation

### Core Documentation
- **[Domain Model](./01-domain-model.md)** ⭐ - Aggregates, entities, invariants (comprehensive)
- **[Authentication Flows](./03-authentication.md)** 🔥 - JWT + wallet auth flows (detailed guide)
- **[API Contracts](./05-api-contracts.md)** - REST endpoints, request/response schemas
- **[Database Schema](./06-database-schema.md)** - EF Core owned entity patterns, concurrency

### Quick Reference
- **Domain Model**: AxonPrincipal aggregate with owned entities (Credential, WalletOwnership, PrincipalChainDefault)
- **Key Pattern**: EF Core `OwnsMany` with composite keys, single concurrency token at aggregate root
- **Authentication**: Dynamic JWT + Solana wallet signatures (Ed25519)
- **Business Rules**: Wallet signing exclusivity, verified-first chain defaults, service risk constraints

---

## Testing

**Test Location**: `tests/Modules/Identity/`

### Test Coverage (113+ test files)
```
Domain/
  Aggregates/
    AxonPrincipal/                   // Aggregate business logic tests
      AxonPrincipal.Commands.Tests.cs  // 50+ command method tests
      AxonPrincipal.Queries.Tests.cs   // Query method tests
  Entities/                          // Owned entity tests
  ValueObjects/                      // Value object validation tests

Application/
  Commands/
    ExchangeCredential/              // Exchange handler integration tests
    VerifyWalletSignature/           // Signature verification tests
    GenerateChallenge/               // Challenge generation tests
  Queries/
    GetMyPrincipal/                  // Query handler + caching tests

Infrastructure/
  Persistence/                       // EF Core, concurrency, database invariants
  Services/
    Ed25519SignatureVerifier.Tests.cs  // Cryptographic verification tests
    DynamicAuthService.Tests.cs        // JWT validation tests
```

### Key Test Patterns
- **Domain Tests**: Aggregate invariants, business rules, state transitions
- **Integration Tests**: Command/query handlers with in-memory database
- **Persistence Tests**: EF Core owned entity patterns, concurrency control
- **Service Tests**: Ed25519 verification, Dynamic.xyz integration

---

## Architecture Highlights

### Owned Entity Pattern (EF Core OwnsMany)
- **Composite Keys**: `(PrincipalId, Id)` for all owned entities
- **Single Concurrency Token**: `xmin` (PostgreSQL) on aggregate root only
- **Cascading Operations**: Owned entities follow aggregate lifecycle
- **No Independent DbSet**: Accessed only through aggregate root

### Business Rules
1. **Wallet Signing Exclusivity**: Only one principal can have verified+signing ownership per wallet
2. **Chain Default Eligibility**: Only verified+signing wallets can be chain defaults
3. **Service Risk Constraint**: Service principals must have `RiskTier.Low`
4. **Ownership Uniqueness**: One principal-wallet-accessMode tuple

### Concurrency Strategy
- **Optimistic Concurrency**: PostgreSQL `xmin` system column
- **Aggregate Boundary**: Single concurrency check for aggregate + owned entities
- **No Per-Entity Versions**: Owned entities protected by aggregate's concurrency token

---

## Integration Points

### Dynamic.xyz
- **JWT Validation**: JWKS-based token validation
- **Wallet Claims**: Parse wallet addresses, chains, access modes from JWT
- **Idempotent Sync**: Exchange endpoint upserts principal + credentials + wallets

### Wallet Verification
- **Solana Ed25519**: NSec.Cryptography for signature verification
- **Challenge-Response**: MAC-protected challenges with replay prevention
- **Multi-Chain Support**: Extensible to EVM chains (future)

---

## Quick Start

### Read First
1. [Domain Model](./01-domain-model.md) - Understand AxonPrincipal aggregate
2. [Authentication Flows](./03-authentication.md) - Learn JWT + wallet auth patterns
3. [API Contracts](./05-api-contracts.md) - Explore REST endpoints

### Common Operations
```csharp
// Create principal with Dynamic credential
var principal = AxonPrincipal.CreateWithDynamicCredential(
    ProviderType.Dynamic, issuer, subject);

// Link wallet ownership
var ownership = WalletOwnership.Create(
    principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified);
principal.LinkWalletOwnership(ownership, checkConflictFunc);

// Set chain default
principal.SetChainDefault("solana-mainnet", walletId);

// Update risk tier
principal.UpdateRiskTier(RiskTier.Medium);
```

---

## Module Metrics

- **Domain Entities**: 4 (1 aggregate root + 3 owned entities)
- **Value Objects**: 4 (Address, ChainId, ProviderType, ProofType)
- **Commands**: 4 (Exchange, Challenge, Verify, Refresh)
- **Queries**: 1 (GetMyPrincipal)
- **API Endpoints**: 5 (Exchange, Me, Challenge, Verify, Refresh)
- **Test Files**: 113+
- **Domain Events**: 4

---

## Additional Resources

- **Tech Stack**: `Docs/ENGINEERING/guides/architecture/tech-stack.md`
- **Coding Standards**: `Docs/ENGINEERING/guides/codebase/coding-standards.md`
- **CQRS Pattern**: `Docs/ENGINEERING/guides/patterns/cqrs.md`
- **Domain Modeling**: `Docs/ENGINEERING/guides/patterns/domain-modeling.md`