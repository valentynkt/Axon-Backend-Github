# Axon AI Identity & Memory — Brownfield Enhancement PRD (Final)

**Date:** 2025-09-09
**Module:** Identity (Principal, Credentials, Wallets)
**Owner:** Product (BMAD PM)
**Architecture Reference:** “Axon AI Identity & Memory — Backend Architecture Document (Final), v1.0, 2025-09-09 (Winston)”

---

## 0. Executive Summary (TL;DR)

Deliver a **minimal, stateless identity layer** that unifies a user’s multiple credentials and wallets into a canonical **Principal**, exposes exactly **two API endpoints** (`POST /auth/exchange`, `GET /auth/me`), enforces **global wallet uniqueness** with **no silent reassignment**, and provides a deterministic **ETag** snapshot for fast client rehydration. The system is designed for **simplicity, security, and future-proofing**, aligning 1:1 with the backend architecture (C#/.NET, FastEndpoints, EF Core, Postgres, Azure, OTel).
**MVP persists only “minimal memory”: per-chain default wallet and a global risk posture — no contact identifiers (e.g., email hashes) are stored.**

**Key invariants:**

* Global wallet uniqueness; one **verified & signing** owner per wallet.
* **No silent reassignments** (conflicts surface as HTTP **409**).
* **Verified-first defaults** (defaults must reference verified & signing wallets).
* **Idempotent exchange** (safe retries).
* **ETag determinism** (changes only on risk tier change, verified & signing ownership change, or default change).

---

## 1. Scope & Non-Goals

**In scope (MVP):**

* Canonical **Principal** model unifying credentials and wallets.
* Two endpoints only: `POST /auth/exchange` (idempotent identity resolution) and `GET /auth/me` (snapshot + ETag).
* Enforced invariants: global wallet uniqueness; **one verified & signing owner** per wallet; **no silent reassignments**.
* Minimal “memory”: per-chain default wallet; global risk posture.
* Observability (OTel) and rate limiting (10 rpm/IP on `exchange`).
* **Code-first** EF Core: entities, fluent configs, migrations (no standalone DB schema doc).

**Out of scope (MVP):**

* Server-issued tokens, sessions, cookies.
* Free-form profile metadata (names, locales, labels, etc.).
* Admin tooling for ownership revocation / dispute back-office.
* Additional endpoints beyond `/auth/exchange` and `/auth/me`.
* End-to-end rollout gating story (removed per direction).

---

## 2. Goals & Success Metrics

**Product goals:**

* Provide a single, stable **Principal** across credentials and wallets.
* Guarantee **wallet ownership safety**: prevent hijacks via global uniqueness + explicit `409` conflicts.
* Keep the API **minimal and stateless**, while supporting future providers/chains without endpoint changes.

**User/API success metrics:**

* A new user can `exchange` to create/resolve Principal; wallets linked; defaults/risk set once.
* `exchange` is idempotent (repeat produces no dupes); `me` returns ETag; unchanged → **304**.
* Cross-credential linking works; conflicts surface as **409** without reassignments.

**KPIs (instrumented):**

* Endpoint latency & uptime; `exchange_success/failure` rate; `401` and `409` rates; `etag_hits/misses` ratio; rate limiter events.

---

## 3. Non-Negotiable Invariants

* **Global wallet uniqueness** by `(chainId, address)`.
* **One verified & signing owner** per wallet (partial-unique enforced via EF Core fluent config → generated index).
* **No silent reassignments**: conflicting link attempts return **409** with privacy-safe details; never reassign.
* **Verified-first defaults**: chain default must reference a **verified & signing** wallet.
* **Idempotent exchange**: replays produce stable state and counts.

---

## 4. Requirements (Locked v0.4)

### 4.1 Functional Requirements (FR)

* **FR1 — Exchange & Upsert:** `POST /auth/exchange` validates provider JWT (JWKS), resolves/creates Principal, ensures wallets by `(chainId, address)`, links **verified & signing** ownerships, applies **verified-first** chain defaults **atomically** in one transaction.
* **FR2 — Idempotency:** Replaying an equivalent `exchange` produces no duplicates (Principal, wallets, ownerships, defaults) and stable counts in `ExchangeDynamicTokenResponse`.
* **FR3 — Global Uniqueness:** If a wallet already has a **verified & signing** owner for another Principal, return **409** (no reassignment, no side-effects).
* **FR4 — Cross-Credential Linking:** Credential uniqueness `(provider, issuer, subject)` guarantees repeat recognition and linking to the same Principal when proofs match.
* **FR5 — Wallet States:** Ownership status supports `pending | verified | revoked`; only **verified & signing** may be a chain default. `pending→verified` occurs on proof via `exchange`; `revoked`/downgrades are **post-MVP/admin**.
* **FR6 — Default Changes:** Defaults can be set/changed **only** to a **verified & signing** wallet of the **same** Principal, via an explicit `exchange` request field.
* **FR7 — Me Snapshot + Caching:** `GET /auth/me` returns `CurrentUserResult` (principal id, risk tier; wallets with `chainId/address/accessMode/isVerified`; `chainDefaults`) and **ETag**; `If-None-Match` short-circuits with **304** if unchanged. **404 MAY** be returned if caller has not completed a successful `exchange` yet.
* **FR8 — Error Semantics + Privacy:** `400/401` (JWT/validation), `409` (ownership conflict), `422` (domain rule), `429` (rate limit), `500` (unexpected). **409 must not expose the other Principal’s id**; details include only `{ chainId, address }`.
* **FR9 — Rate Limiting:** `/auth/exchange` limited to **10 rpm/IP**, returning `Retry-After`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
* **FR10 — Observability & Auditing:** OTel traces/metrics/logs + structured audit events for identity-affecting ops (who/what/when/why; no secrets).
* **FR11 — Normalization:** Normalize `chainId` and address before persistence; optionally update `last_seen` without affecting ETag.
* **FR12 — Minimal Data + Risk Tier:** Persist only Principal, Credentials (minimal), Wallets, Ownerships, ChainDefaults, RiskTier. **No email hash or profile fields in MVP.** **Wire enum**: `low | medium | high`; product terms map `conservative→low`, `balanced→medium`, `aggressive→high`.
* **FR13 — ETag Determinism:** ETag changes **only** when: (a) risk tier changes, (b) a **verified & signing** ownership is added/removed, or (c) a chain default changes. Pure reads, `last_seen` updates, **pending-only ownership changes**, and **no-op writes** (e.g., setting the same default or the same risk tier) **must not** change ETag.

### 4.2 Non-Functional Requirements (NFR)

* **NFR1 — Statelessness:** No server tokens/cookies; each call validates provider JWT (`iss`, `aud` (if set), `sub`, exp, signature, skew).
* **NFR2 — Performance:** `/auth/me` optimized for ETag short-circuit; `exchange` optimized for idempotent retries and batch wallet “ensure” to avoid N+1.
* **NFR3 — Reliability:** Transactional writes; partial-unique constraint ensures single verified & signing owner; safe to retry `exchange`.
* **NFR4 — Security:** JWKS via `ConfigurationManager`; HTTPS/HSTS; security headers; secrets in Azure Key Vault.

  * **JWKS caching:** Keys cached with provider-aligned TTL and proactive refresh to tolerate rotations/outages.
  * **CORS:** Restricted to Axon frontend origins; partner origins added via configuration.
* **NFR5 — Privacy/Minimalism:** Minimal data only; immutable audit trail; **no contact identifiers (including email hashes)**; no free-form profiles or ownership labels.
* **NFR6 — Observability:** OTel metrics for `exchange_success/failure`, `wallet_conflicts`, `etag_hits/misses`; validator→DB spans; structured logs with correlation.
* **NFR7 — Extensibility:** Provider-agnostic validator; adding **SIWS** or new chains **does not** change `/auth/*`.
* **NFR8 — Operations:** Rate limiter configured; Swagger + Scalar enabled; forward-only migrations.
* **NFR9 — Read Model Compatibility:** Read models may include `watchOnly`; **MVP** does not create watch-only via `exchange`.

### 4.3 Compatibility Requirements (CR)

* **CR1 — API Stability:** Identity remains encapsulated behind `/auth/*`; no breaking changes to other Axon public APIs.
* **CR2 — Schema Safety:** Additive/migration-safe changes; `updated_at` + EF concurrency tokens.
* **CR3 — UI Contract:** Conflicts are explicit via **409**; backend never reassigns silently.
* **CR4 — Integrations:** Dynamic now; **SIWS post-MVP** via adapter; Azure deployment aligned with Axon-Backend conventions.

---

## 5. Architecture Summary (for PRD readers)

* **Module:** Identity inside Axon-Backend modular monolith.
* **Style/Patterns:** Clean Architecture; DDD aggregates (Principal, Wallet); CQRS (write=`exchange`, read=`me`).
* **Auth:** Provider-issued JWT only; `JwtBearerHandler` + `ConfigurationManager` for JWKS & rotation; in-process cache.
* **Persistence:** EF Core code-first to Postgres 16; unique constraints for credentials and wallets; **partial unique** for one verified & signing owner.
* **Caching:** ETag (DB-derived fingerprint) on `/auth/me` with `If-None-Match` support.
* **Observability:** OTel traces/logs/metrics → OTLP collector; dashboards for latency and 401/409/429.
* **Hosting:** Azure App Service / Container Apps; Key Vault for secrets.

---

## 6. API Contracts (MVP, minimal)

> The service is **stateless**. Authentication via `Authorization: Bearer <provider-JWT>` on every request. No server-issued tokens or cookies.

### 6.1 `POST /auth/exchange`

**Purpose:** Validate provider JWT, resolve/create Principal, ensure/link **verified & signing** ownerships, enforce invariants, apply defaults; return operation counts.

**Headers:**

* `Authorization: Bearer <JWT>`

**Body (optional — preferences):**

```json
{
  "desiredDefaults": {
    "solana": { "chainId": "solana", "address": "<base58 address>" }
  },
  "riskTier": "low"
}
```

**Notes:**

* Body is **optional**. By default, defaults follow **verified-first**. Provide `desiredDefaults` to explicitly change to another **verified & signing** wallet owned by the same Principal.
* `riskTier` is optional; wire enum `low|medium|high` (product mapping documented below).
* **No-op semantics:** If `desiredDefaults` equals current defaults and/or `riskTier` equals current value, the operation **MUST** avoid writes that would change `updated_at` or ETag.

**Response 200 — ExchangeDynamicTokenResponse:**

```json
{
  "axonId": "01JJ8Z3W1W0Q9QYJYF3EG2H20X",
  "created": true,
  "walletsProcessed": 1,
  "walletsLinked": 1,
  "defaultsApplied": 1,
  "skipped": 0,
  "conflicts": 0
}
```

**Error responses:**

* `400/401` invalid/malformed/expired JWT.
* `409` wallet ownership conflict →

```json
{ "code": "WALLET_OWNERSHIP_CONFLICT", "message": "wallet already owned", "details": { "chainId": "solana", "address": "..." } }
```

* `422` domain rule violation (non-conflict).
* `429` rate limit with `Retry-After`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` headers.

### 6.2 `GET /auth/me`

**Purpose:** Return current Principal snapshot with ETag for conditional GET.

**Headers:**

* `Authorization: Bearer <JWT>`
* Optional `If-None-Match: "<etag>"`

**Response 200 (with `ETag` header):**

```json
{
  "profile": { "axonId": "01JJ8Z3W1W0Q9QYJYF3EG2H20X", "riskTier": "medium" },
  "wallets": [
    { "walletId": "01JJ8Z3X...", "chainId": "solana", "address": "...", "accessMode": "signing", "isVerified": true }
  ],
  "chainDefaults": { "solana": "01JJ8Z3X..." }
}
```

**Response 304:** no body when `If-None-Match` matches server fingerprint.
**Response 404:** MAY be returned if the caller has not completed a successful `exchange` yet (no Principal exists).
**Cache-Control:** `private, max-age=0, must-revalidate`.

---

## 7. Data Model (Conceptual)

* **Principal (Aggregate Root):** ULID id; type (Human/Service); **no contact identifiers persisted in MVP**; risk tier (`low|medium|high`); collections: Credentials, WalletOwnerships, PrincipalChainDefaults.
* **Credential:** Unique by `(provider, issuer, subject)`; belongs to Principal.
* **Wallet:** Globally unique `(chainId, address)`; catalog entry until linked.
* **WalletOwnership:** `(principalId, walletId)` unique; fields: `accessMode (signing|watchOnly)`, `status (pending|verified|revoked)`; **partial unique** index ensures one **verified & signing** owner per wallet.
* **PrincipalChainDefault:** Composite `(principalId, chainId)` → walletId; must point to **verified & signing** wallet.

---

## 8. Observability & Rate Limiting

* **Metrics:** `exchange_success/failure`, `wallet_conflicts`, `etag_hits/misses`, latency histograms per endpoint, rate limiter counters.
* **Tracing:** Spans across validator → DB; correlation IDs flow end-to-end.
* **Logging:** Structured JSON; include requestId/traceId; **never log secrets/PII; no contact identifiers are persisted or logged.**
* **Rate limiting:** `/auth/exchange` at **10 rpm/IP**; surfaced via headers.

---

## 9. Risks & Mitigations

* **Wallet uniqueness contention:** Use batch ensure + compiled queries; future read replicas for `/auth/me` if needed.
* **Provider dependency (JWKS):** `ConfigurationManager` handles key rotation; cache with provider-aligned TTL & proactive refresh; return clear 401s on failures.
* **Conflict UX burden:** Frontend owns dispute flows; backend stays explicit (`409`), auditable.
* **Enum drift:** Central mapping for product terms ↔ wire values; unit tests.
* **Operational misconfig:** Startup health checks for issuer/audience, JWKS endpoint, DB connectivity; IaC defaults.

---

## 10. Epic & Stories (Final)

### Epic — Identity & Memory (Principal, Credentials, Wallets) — Brownfield Enhancement

**Goal:** Deliver the minimal, stateless Identity & Memory module with enforced invariants, two endpoints, ETag caching, observability, and rate limiting.
**Integration Requirements:** Axon-Backend module; C#/.NET 10; FastEndpoints; EF Core (code-first); Postgres; Azure; OTel; rate limiting; Swagger/Scalar.

#### Story 1.1 — EF Core Model, Configs & Initial Migration (Code-First)

**As** a backend engineer, **I want** code-first entity models and fluent configurations (plus initial migration), **so that** invariants are enforced by the generated schema with no separate DB schema doc.

**Acceptance Criteria**

* Entities/VOs: Principal, Credential, Wallet, WalletOwnership, PrincipalChainDefault; ULID ids; enums (risk `low|medium|high`, access `signing|watchOnly`, status `pending|verified|revoked`).
* Fluent configs enforce:

  * Credential unique `(provider, issuer, subject)`.
  * Wallet unique `(chain_id, address)`.
  * WalletOwnership unique `(principal_id, wallet_id)` **and** **partial unique** for one **verified & signing** owner per wallet.
  * PrincipalChainDefault composite key `(principal_id, chain_id)` with FK to a **verified & signing** wallet.
  * `updated_at` concurrency tokens on write tables.
* ETag fingerprint implemented as compiled query combining principal/ownership/default timestamps; returns hex string.
* Initial migration generated and applied locally (dev/test) via EF migrations.

**Integration Verification**

* IV1: Namespacing confined to Identity; no collisions.
* IV2: Constraint tests (Testcontainers) validate uniqueness + partial-unique.
* IV3: Compiled queries avoid N+1 on hot paths.

---

#### Story 1.2 — JWT Validation & Rate Limiting Skeleton

**As** a platform engineer, **I want** JWT validation (JWKS rotation) and rate-limited API skeleton, **so that** calls are authenticated and `/auth/exchange` is protected from abuse.

**Acceptance Criteria**

* `JwtBearerHandler` + `ConfigurationManager` wired; issuer/audience, signature, expiry, skew enforced; JWKS cache with sensible TTL.
* ASP.NET Core RateLimiter: `/auth/exchange` at **10 rpm/IP** returning `Retry-After`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
* Placeholder endpoints secured: **401** without token; **200** on mock handler with valid token.

**Integration Verification**

* IV1: Global headers/security unaffected for existing modules.
* IV2: Auth failures logged safely; no secrets.
* IV3: Limiter overhead negligible at low QPS.

---

#### Story 1.3 — Domain Aggregates & Invariants (Unit-Level)

**As** a domain engineer, **I want** aggregates and VOs enforcing invariants, **so that** business rules are explicit and testable without infra.

**Acceptance Criteria**

* Aggregates enforce: one verified & signing owner; verified-first defaults; idempotent link/update semantics; risk tier mapping (product ↔ wire).
* **No-op guard:** Reapplying the same `riskTier` or the same default **MUST NOT** persist changes (no `updated_at` bump; ETag unchanged).
* Unit tests cover linking, idempotency, default assignment/change, conflict detection, and no-op behavior.

**Integration Verification**

* IV1: No EF types leak into domain/app layers.
* IV2: Result pattern for expected failures (no exceptions for control flow).
* IV3: Branch coverage ≥80% on invariants.

---

#### Story 1.4 — `/auth/exchange` Command Handler (Create/Resolve & Link)

**As** an application engineer, **I want** a command that validates, normalizes, batch-ensures wallets, links **verified & signing** ownerships, applies defaults, and persists atomically, **so that** `exchange` is safe to retry and enforces invariants.

**Acceptance Criteria**

* `ExchangeCredentialCommand` implements sequence with a **single transaction**.
* Batch `EnsureManyByChainAndAddressAsync` prevents N+1; optional `TouchLastSeenAsync` does not affect ETag.
* Returns `ExchangeDynamicTokenResponse` with stable counts.
* **409** on conflicting verified owner; `ApiError { code, message, details:{ chainId,address } }`; never exposes other principal id.

**Integration Verification**

* IV1: Endpoint isolated under `/auth` and does not impact other APIs.
* IV2: 409 path validated via partial-unique index in integration tests.
* IV3: p50/p95 within acceptable dev/test bounds.

---

#### Story 1.5 — `/auth/me` Query + Deterministic ETag

**As** a frontend-facing engineer, **I want** `GET /auth/me` to return a snapshot with a deterministic ETag and `If-None-Match` support, **so that** clients rehydrate cheaply.

**Acceptance Criteria**

* `GetMyPrincipalQuery` resolves by credential.
* DB fingerprint used as ETag; **304** when `If-None-Match` matches.
* **ETag invariants:** ETag changes only on (a) riskTier change, (b) **verified & signing** ownership add/remove, (c) chain default change.
* Pending-only ownership changes, `last_seen` touches, and no-op writes **DO NOT** change ETag.
* **404** MAY be returned if no Principal exists yet (pre-exchange).

**Integration Verification**

* IV1: ETag changes only on specified events.
* IV2: Cache-Control set to `private, max-age=0, must-revalidate`.
* IV3: No sensitive data leakage.

---

#### Story 1.6 — Error Mapping, Privacy, and API Docs

**As** a developer advocate, **I want** canonical error mapping and documented schemas, **so that** clients integrate quickly and safely.

**Acceptance Criteria**

* Map: `400/401/409/422/429/500` to `ApiError` envelopes.
* **409** excludes owner principal id; includes only `{ chainId, address }`.
* Swagger & Scalar serve OpenAPI with `bearerAuth` and examples.

**Integration Verification**

* IV1: API discovery unaffected for other modules.
* IV2: Contract tests confirm error envelopes & enums.
* IV3: Security headers preserved.

---

#### Story 1.7 — Observability & Audit Events

**As** an SRE-minded engineer, **I want** OTel tracing/metrics/logging and structured audit events, **so that** we can operate and investigate the service.

**Acceptance Criteria**

* Spans across validator → DB; correlation IDs flow end-to-end.
* Metrics: `exchange_success/failure`, `wallet_conflicts`, `etag_hits/misses`, latency distributions.
* Audit logs for identity-affecting operations (who/what/when/why) without secrets.

**Integration Verification**

* IV1: OTLP export aligns with the existing collector.
* IV2: Dashboards created/updated (latency, 401/409/429, DB).
* IV3: Log volume within budget at expected QPS.

---

## 11. Definition of Done (Epic)

* Stories **1.1–1.7** pass ACs and IVs.
* Invariants enforced at both domain and persistence layers (via code-first configs).
* `/auth/exchange` and `/auth/me` fully functional, rate-limited, and documented; `/auth/me` 200→304 ETag flow validated by unit/integration tests.
* OTel metrics/traces/logs and structured audit events live; dashboards/alerts created.
* No regressions in Axon-Backend services.

---

## 12. Appendices

### 12.1 Risk Posture Mapping (Product ↔ Wire)

* Product terms: `conservative | balanced | aggressive`.
* **Wire enum:** `low | medium | high`.
* Mapping: `conservative→low`, `balanced→medium`, `aggressive→high`.

### 12.2 Error Code Glossary

* `400/401`: Malformed/invalid/expired JWT.
* `409`: Wallet ownership conflict (no reassignment). Details include only `(chainId, address)`.
* `422`: Domain rule violation (non-conflict).
* `429`: Too many requests (rate limit); includes retry headers.
* `500`: Unexpected error.

### 12.3 ETag Fingerprint (Concept)

* Deterministic hash over: `principal.updated_at` + **max of active verified & signing** `wallet_ownership.updated_at` + max of `principal_chain_default.updated_at`.
* Pure reads, `last_seen` touches, **pending-only ownership changes**, and **no-op writes** **must not** change the ETag.

### 12.4 Test Strategy (Unit/Integration)

* **Unit:** Domain invariants; handler idempotency; error mapping; risk tier mapping; **no-op write guards**.
* **Integration:** EF + Postgres (Testcontainers); unique + partial-unique index behavior; JWKS validation; `/auth/me` ETag 200→304; rate limiter headers; **pending-only change does not alter ETag**.

---

**End of PRD**
