# 1. Introduction

This document captures the **production-ready** architecture of the Axon AI **Identity & Memory** service after **Epic 2 stabilization**. It is a **stateless** backend module, designed with **DDD**, **CQRS**, and **Clean Architecture**, that exposes two REST endpoints with production-hardened features:

* `POST /auth/exchange` — idempotently creates/updates a **Principal**, links verified wallets, enforces global uniqueness, applies defaults. **Rate-limited** at 10 req/min per IP.
* `GET /auth/me` — returns a **snapshot** of the authenticated Principal with **conditional GET** support via **ETag** headers for optimal caching.

**Epic 2 Improvements:**
* **Eliminated dual patterns**: Single canonical command/query handlers (`ExchangeCredentialCommand`, `GetMyPrincipalQuery`)
* **Separated concerns**: `DynamicAuthService` with extracted `IJwksService` for JWKS caching and validation
* **Production observability**: Comprehensive metrics, correlation IDs, structured logging
* **Performance optimization**: ETag-based caching, rate limiting, compiled queries

The design avoids server-issued tokens/cookies, relies on provider-issued JWTs (e.g., Dynamic), and stores only the data required for automated functions. **No contact identifiers are persisted.**

---
