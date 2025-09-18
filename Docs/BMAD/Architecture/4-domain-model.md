# 4. Domain Model

## AxonPrincipal (Aggregate Root)

**Purpose:** Canonical identity (human or service). Enforces invariants for credential uniqueness, wallet ownership, and chain defaults.

**Attributes**

* `Id: AxonId` (ULID, char(26))
* `Type: PrincipalType` (`Human` | `Service`)
* `RiskTier: RiskTier` (internal enum; wire = `low|medium|high`)
* `Credentials: IdentityCredential[]` (unique by provider, issuer, subject)
* `Ownerships: WalletOwnership[]` (access mode, status)
* `ChainDefaults: Map<chainId, WalletId>` (must be verified + signing)

> **MVP Privacy:** No contact identifiers (no email, no phone, no hashes) are stored.

## Wallet (Aggregate)

**Purpose:** Globally unique on-chain wallet (`chain`,`address`). Independent catalog entry until linked.

**Attributes**

* `Id: WalletId` (ULID, char(26))
* `ChainId: string` (e.g., `"solana"`)
* `Address: Address` (normalized, validated)
* `FirstSeenAt / LastSeenAt: DateTimeOffset`

## Invariants

1. **Global Wallet Uniqueness:** `(chainId, address)` unique in `wallet`.
2. **Single Verified Signing Owner:** At most one `Principal` has **verified & signing** ownership for a wallet (partial-unique index).
3. **No Silent Reassignments:** Attempts to link a wallet already owned (verified & signing) by another principal → **409 Conflict**.
4. **Idempotent Exchange:** Reprocessing the same credential bundle does not duplicate or contradict prior state.
5. **Defaults Verified-First:** Chain default must reference a **verified** `signing` ownership.

---
