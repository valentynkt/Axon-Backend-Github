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

- Single verified owner per wallet globally
- Maximum 10 wallets per principal
- Unique credentials per provider/issuer/subject
- One default wallet per chain per principal

## Integration

The Identity module integrates with:
- Wallet BC (read-only via repository)
- Dynamic.xyz authentication provider
- SIWS (Sign-In With Solana) flows