# Identity Domain Module

## Overview

The Identity module implements the core domain logic for principal management in the Axon platform. It provides wallet-first identity resolution with support for Dynamic.xyz and SIWS authentication flows.

## Architecture

- **Aggregate Root**: AxonPrincipal
- **Owned Entities**: PrincipalProfile (1:1)
- **Child Entities**: IdentityCredential (1:many), WalletOwnership (1:many)
- **External References**: Wallet BC via repository pattern

## Key Features

- Global identity across partners
- Wallet-first resolution
- No auto-merges in MVP
- Soft delete pattern
- Cryptographic proof of possession
- Per-chain default wallet management

## Domain Rules

### Core Invariants

#### Wallet Rules (W1-W7)
- **W1**: Global uniqueness - Each (Chain, CanonicalAddress) pair exists exactly once in the system
- **W2**: Immutability - Chain and Address cannot be modified after wallet creation
- **W3**: Canonical form - Addresses are normalized to canonical format for the chain
- **W4**: Time monotonicity - `LastSeenAt >= FirstSeenAt` always
- **W5**: Metadata size limits - Wallet metadata cannot exceed `WalletMeta.MaxSizeBytes`
- **W6**: Tag policy - Only allowed tags can be applied to wallets
- **W7**: Soft delete guard - Operations only allowed on active (non-deleted) wallets

#### Principal Rules (P1-P5)
- **P1**: **Single verified owner per wallet globally** - Only one principal can have verified-signing access to a wallet
- **P2**: Maximum 10 wallets per principal (`MaxWalletsPerPrincipalRule`)
- **P3**: Unique credentials per provider/issuer/subject combination per principal
- **P4**: One default wallet per chain per principal
- **P5**: Operations only allowed on active (non-deleted) principals

### Cross-Principal Ownership Enforcement
**Critical**: The global "single verified owner" invariant is enforced by the application/domain service layer, not within the aggregate. The aggregate only enforces in-aggregate uniqueness via `WalletMustNotBeOwnedByPrincipalRule`.

**Implementation Location**: Application layer handlers that orchestrate wallet linking must:
1. Check that no other principal has a verified-signing ownership for the target wallet
2. Raise `WalletOwnershipConflictSkippedEvent` when conflicts are detected and resolution strategy is to skip
3. Only proceed with linking/verification if no global conflict exists

### WalletOwnership Semantics
- **"Active" ownership** = `!IsDeleted && State.IsVerified` 
- **"Verified-signing" ownership** = `IsActive && AccessMode == Signing`
- Only verified-signing ownerships can be set as chain defaults
- Only one verified-signing ownership allowed globally per wallet (cross-principal invariant)

### Time Provider Usage
**Query Methods**: `Wallet.Queries.cs` methods (`Age`, `TimeSinceLastSeen`, `IsRecentlyActive`) use `DateTimeOffset.UtcNow` directly instead of TimeProvider for simplicity. This is an intentional trade-off:
- **Pros**: Cleaner API, no additional parameter passing required
- **Cons**: Less testable (non-deterministic in unit tests)
- **Decision**: Keep as-is for convenience queries; use explicit TimeProvider for state-changing operations only

## Database Constraints (Infrastructure Layer)

The following uniqueness constraints should be implemented in the persistence layer to enforce domain invariants under concurrency:

### Required Unique Constraints

1. **Wallet Identity Uniqueness**
   ```sql
   UNIQUE (Chain, CanonicalAddress)
   ```
   - Enforces W1: Global wallet uniqueness across the entire system
   - Prevents duplicate wallet registration

2. **Identity Credential Uniqueness** 
   ```sql
   UNIQUE (PrincipalId, ProviderType, Issuer, Subject)
   WHERE IsDeleted = false
   ```
   - Enforces unique credentials per provider/issuer/subject per principal
   - Partial index excludes soft-deleted credentials from uniqueness check

3. **Single Verified Signing Owner**
   ```sql
   UNIQUE (WalletId) 
   WHERE IsDeleted = false 
     AND State = 'Verified' 
     AND AccessMode = 'Signing'
   ```
   - Enforces the critical global "single verified owner" invariant
   - Prevents data integrity issues under concurrent wallet linking/verification
   - Partial index only applies to active verified-signing ownerships

### Implementation Notes
- Use partial unique indexes for soft-deleted entities to maintain uniqueness among active records
- Database constraints serve as the final enforcement layer for concurrency scenarios
- Application layer should still perform checks for better user experience and error messages

## Event Handling

### Key Domain Events
- `DefaultWalletChangedEvent` - Raised when chain default is set/cleared (supports null `NewWalletId` for clearing)
- `WalletOwnershipLinkedEvent` - Raised when wallet is linked to a principal
- `WalletOwnershipVerifiedEvent` - Raised when ownership proof is verified
- `WalletOwnershipUnlinkedEvent` - Raised when wallet is unlinked from a principal
- `WalletOwnershipConflictSkippedEvent` - Raised when linking is skipped due to global ownership conflicts

### Event Payload Standards
All domain events use primitive types (strings, GUIDs) rather than value objects for better interoperability and stable serialization contracts across bounded context boundaries.

## Integration

The Identity module integrates with:
- Wallet BC (read-only via repository)
- Dynamic.xyz authentication provider
- SIWS (Sign-In With Solana) flows