# 1. Introduction

This document captures the **production-ready** architecture of the Axon AI **Identity & Memory** service after **Epic 3 wallet-first resolution**. It is a **stateless** backend module, designed with **DDD**, **CQRS**, and **Clean Architecture**, that exposes two REST endpoints with production-hardened features:

* `POST /auth/exchange` — idempotently creates/updates a **Principal** using **wallet-first resolution**, links verified wallets, enforces global uniqueness, applies defaults. **Rate-limited** at 10 req/min per IP.
* `GET /auth/me` — returns a **snapshot** of the authenticated Principal with **conditional GET** support via **ETag** headers for optimal caching.

**Epic 3 Core Innovation:**
* **Wallet-First Identity Resolution**: Prevents duplicate principals by checking wallet ownership before credential-based lookup
* **Cross-Authentication Method Unity**: Users with same wallets get same identity regardless of login method (Google, email, Dynamic, etc.)
* **Enhanced Principal Resolution**: New `ResolveOrCreatePrincipalWalletFirst` method implements the wallet-first workflow

**Previous Epic Improvements (Epics 1-2):**
* **Eliminated dual patterns**: Single canonical command/query handlers (`ExchangeCredentialCommand`, `GetMyPrincipalQuery`)
* **Separated concerns**: `DynamicAuthService` with extracted `IJwksService` for JWKS caching and validation
* **Production observability**: Comprehensive metrics, correlation IDs, structured logging
* **Performance optimization**: ETag-based caching, rate limiting, compiled queries

The design avoids server-issued tokens/cookies, relies on provider-issued JWTs (e.g., Dynamic), and stores only the data required for automated functions. **No contact identifiers are persisted.**

---
