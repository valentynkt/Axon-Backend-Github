# Identity Domain Model

**Aggregates, entities, value objects, and domain invariants.**

---

## Aggregate Root: AxonPrincipal

**Location**: `src/Modules/Identity/Domain/Aggregates/AxonPrincipal/`

### Responsibilities
- Manages principal identity (human/service)
- Owns credentials, wallet ownerships, and chain defaults
- Enforces ownership exclusivity and risk tier constraints
- Coordinates wallet verification and chain default selection

### Key Properties
```csharp
AxonUserId Id                                          // Strong-typed aggregate ID
PrincipalType Type                                     // Human | Service
RiskTier RiskTier                                      // Low | Medium | High
IReadOnlyCollection<IdentityCredential> Credentials
IReadOnlyCollection<WalletOwnership> WalletOwnerships
IReadOnlyCollection<PrincipalChainDefault> PrincipalChainDefaults
```

### Domain Invariants
1. **Service Risk Constraint**: Service principals must have `RiskTier.Low`
2. **Ownership Uniqueness**: One principal-wallet-accessMode tuple (enforced via composite key)
3. **Signing Exclusivity**: One verified+signing ownership per wallet (enforced via partial unique index)
4. **Chain Default Uniqueness**: One default wallet per chain per principal
5. **Default Eligibility**: Only verified+signing wallets can be chain defaults
6. **Max Wallets**: 10 wallet ownerships per principal

### Factory Methods
```csharp
CreateHuman(AxonUserId? id = null)                    // Human principal
CreateService(AxonUserId? id = null)                  // Service principal  
CreateWithDynamicCredential(...)                      // Human + Dynamic JWT credential
```

### Command Methods
- `UpdateRiskTier(RiskTier)` - Update risk tier with service constraint
- `LinkWalletOwnership(...)` - Add wallet with exclusivity enforcement
- `AddCredential(...)` - Add OAuth/JWT credential with uniqueness check
- `VerifyWalletOwnership(...)` - Create/update ownership to verified
- `SetChainDefault(chainId, walletId)` - Set default wallet for chain
- `ApplyChainDefaultsBatch(...)` - Bulk default application (optimized)
- `UpdateWalletOwnershipStatus(...)` - Update ownership status + auto-clear defaults
- `RevokePendingOwnershipsForWallet(...)` - Auto-revoke conflicts

---

## Owned Entities

### IdentityCredential
**Location**: `src/Modules/Identity/Domain/Entities/IdentityCredential.cs`

OAuth/JWT credential linking principal to external identity provider.

**Properties**:
```csharp
IdentityCredentialId Id                    // Owned entity ID
AxonUserId PrincipalId                     // FK to aggregate root
string Provider                            // "dynamic", "siws", etc.
string Issuer                              // JWT issuer URL
string Subject                             // Provider's user ID
DateTime LastSeenAt                        // Last authentication timestamp
```

**Composite Key**: `(PrincipalId, Id)`  
**Unique Constraint**: `(Provider, Issuer, Subject)` - one credential per provider identity

---

### WalletOwnership
**Location**: `src/Modules/Identity/Domain/Entities/WalletOwnership.cs`

Links principal to wallet with cryptographic verification proof.

**Properties**:
```csharp
WalletOwnershipId Id                       // Owned entity ID
AxonUserId PrincipalId                     // FK to aggregate root
WalletId WalletId                          // FK to Wallet aggregate
AccessMode AccessMode                      // Signing | WatchOnly
OwnershipStatus Status                     // Pending | Verified | Revoked
VerificationSource VerificationSource      // DynamicAttested | ManualVerified | SiwsVerified
DateTime? VerifiedAt
DateTime? RevokedAt
string? RevokeReason
```

**Composite Key**: `(PrincipalId, Id)`  
**Unique Constraints**:
- `(PrincipalId, WalletId)` - one ownership per principal-wallet pair
- `(WalletId, AccessMode, Status)` with filter `AccessMode='Signing' AND Status='Verified'` - signing exclusivity

**State Transitions**:
- Pending → Verified ✅
- Pending → Revoked ✅
- Verified → Revoked ✅
- Revoked → Verified ✅ (re-verification allowed)

**Computed Properties**:
- `IsVerifiedSigning` - Status=Verified AND AccessMode=Signing
- `CanBeDefault` - Alias for IsVerifiedSigning
- `IsActive` - Status != Revoked

---

### PrincipalChainDefault
**Location**: `src/Modules/Identity/Domain/Entities/PrincipalChainDefault.cs`

Maps principal's default wallet per blockchain.

**Properties**:
```csharp
Guid Id                                    // Owned entity ID (Version 7 GUID)
AxonUserId PrincipalId                     // FK to aggregate root
string ChainId                             // Compound format: "solana-mainnet", "ethereum-mainnet"
WalletId WalletId                          // Default wallet for this chain
```

**Composite Key**: `(PrincipalId, Id)`  
**Unique Constraint**: `(PrincipalId, ChainId)` - one default per chain per principal

---

## Value Objects

**Location**: `src/Modules/Identity/Domain/ValueObjects/`

### Address
Normalized blockchain address with multi-chain validation.
```csharp
Address.Create(string)                     // Result-based factory
Address.From(string)                       // Throwing factory

// Validation
- Solana: Base58, 32-44 chars
- EVM: 0x + 40 hex chars
- Generic: 10-200 chars, no malicious patterns

// Features
ToShortDisplay(prefixLen, suffixLen)       // "0x1234...5678"
IsValidForChain(chainId)                   // Chain-specific format check
```

### ProviderType
Identity provider enumeration (Vogen value object).
```csharp
Dynamic, Manual, Siws                      // Primary types
Google, Github, Discord, Twitter           // Future OAuth providers

SupportsWalletAuth()                       // dynamic/siws/manual
SupportsOAuth()                            // google/github/discord/twitter
```

### ProofType
Cryptographic proof type for wallet verification.
```csharp
Signature, Message, Transaction            // Supported proof types
```

### ChainId
Blockchain network identifier with compound format support.
```csharp
Supported: solana, ethereum, polygon, arbitrum, optimism, base, avalanche, binance
Compound: "solana-mainnet", "ethereum-sepolia", "polygon-mumbai", etc.
```

---

## Enums

**Location**: `src/Modules/Identity/Domain/Enums/`

```csharp
PrincipalType { Human, Service }
RiskTier { Low, Medium, High }
AccessMode { Signing, WatchOnly }
OwnershipStatus { Pending, Verified, Revoked }
VerificationSource { DynamicAttested, ManualVerified, SiwsVerified }
```

---

## Domain Events

**Location**: `src/Modules/Identity/Domain/Events/`

```csharp
PrincipalChangedEvent                      // Risk tier, chain default changes
CredentialChangedEvent                     // Credential added/updated/revoked
OwnershipChangedEvent                      // Ownership linked/verified/revoked
WalletChangedEvent                         // Wallet-level changes
```

**Key Event Patterns**:
- `verified_signing_added` - Triggers cross-aggregate auto-revocation
- `status_updated` - Ownership state transition
- `ChainDefault.{chainId}` - Default wallet changed for chain

---

## Business Rules Summary

### Wallet Ownership Exclusivity
- **One Signing Owner**: Only one principal can have verified+signing access to a wallet
- **Pending Auto-Revocation**: When principal A verifies signing ownership, all pending ownerships for other principals are revoked
- **Watch-Only Allowed**: Multiple principals can have watch-only access to same wallet

### Chain Defaults
- **Verified-First**: Only verified+signing wallets eligible as defaults
- **Auto-Clear on Revoke**: Revoking ownership clears all chain defaults for that wallet
- **Idempotent**: Setting same wallet as default twice is no-op

### Risk Tiers
- **Service Constraint**: Service principals locked to `RiskTier.Low`
- **Human Flexibility**: Human principals can have any risk tier
- **Wire Mapping**: `low/conservative`, `medium/balanced`, `high/aggressive`

---

## Code References

**Domain**: `src/Modules/Identity/Domain/`
- `Aggregates/AxonPrincipal/AxonPrincipal.cs` - Aggregate root (partial class)
- `Aggregates/AxonPrincipal/AxonPrincipal.Commands.cs` - Command methods
- `Aggregates/AxonPrincipal/AxonPrincipal.Queries.cs` - Query methods
- `Entities/` - Owned entities (IdentityCredential, WalletOwnership, PrincipalChainDefault)
- `ValueObjects/` - Vogen value objects (Address, ChainId, ProviderType, ProofType)
- `Enums/` - Domain enumerations
- `Events/` - Domain events
- `Errors/IdentityDomainErrors.cs` - Domain error codes