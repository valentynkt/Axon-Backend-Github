# Axon Identity — High-Level Flow Architecture (Client → SDK → API)

## 1) Purpose & Scope

A single, business-level source of truth for how clients authenticate and stay in sync with Axon using:

* **Dynamic Auth (preferred)** — client holds a Dynamic **JWT**
* **Manual Wallet Sign-In** — client obtains a short-lived **Axon Access Token** after a wallet signature

The document covers token flow, endpoints, call order, responsibilities, error semantics, and key invariants—optimized for 20/80 and KISS.

---

## 2) Core Principles (Design Guardrails)

* **One header to rule them all:** `Authorization: Bearer <token>` (Dynamic JWT **or** Axon Access Token)
* **Two endpoints only:**

    * `POST /api/v1/auth/exchange` — idempotent upsert/sync
    * `GET  /api/v1/auth/me` — current user snapshot (ETag)
* **No server sessions/cookies.** The bearer drives auth; clients handle refresh/challenge.
* **Idempotency:** Safe to retry `exchange`.
* **Caching:** `me` supports ETag for conditional GET.

---

## 3) Actors, Tokens, and “Source of Truth”

* **Client App** — UI and local state
* **Axon SDK (thin)** — adds bearer, orchestrates `exchange`/`me`, handles ETag
* **API** — validates bearer, runs exchange upsert, returns normalized identity
* **Tokens**

    * **Dynamic JWT** — validated against JWKS
    * **Axon Access Token** — short-lived token issued after wallet signature; validated by Axon
* **Authoritative user state** lives in Axon (principal, credentials, wallet ownerships, per-chain defaults)

---

## 4) Endpoints (Contracts & Semantics)

### A) `POST /api/v1/auth/exchange`

**Purpose:** Normalize/verify the bearer, upsert principal + credentials, link wallets, apply defaults, and return a **sync summary**.

* **Auth:** `AllowAnonymous` (header token treated as input)
* **Headers:** `Authorization: Bearer <Dynamic JWT | Axon Access Token>`
* **Body:** none (future flags optional)
* **Response (200):**

  ```json
  {
    "axonId": "string",
    "created": true,
    "walletsProcessed": 3,
    "walletsLinked": 2,
    "defaultsApplied": 1,
    "skipped": 0,
    "conflicts": 0
  }
  ```
* **HTTP Codes:** `200, 400, 401, 409, 422, 429, 500`
* **Business behavior:**

    * Upsert principal & credential for the provider
    * Parse wallet claims → validate → link as **Ownership(verified/pending/watch-only)**
    * Apply **one default per chain** if missing
    * Deduplicate; no duplicate wallet links for a principal
    * Metrics summarize the run

### B) `GET /api/v1/auth/me`

**Purpose:** Return the **canonical, merged** view of the current user.

* **Auth:** Required; middleware validates header (Dynamic JWT or Axon token)
* **Headers:**

    * `Authorization: Bearer <token>`
    * `If-None-Match: "<etag>"` (optional)
* **Response (200):**

  ```json
  {
    "profile": { "axonId": "string", "riskTier": "balanced" },
    "wallets": [
      { "chain":"solana","address":"...","state":"verified","access":"signing","isDefault":true }
    ],
    "etag": "\"W/abc123\""
  }
  ```
* **304 Not Modified** when ETag matches

---

## 5) Canonical Flows (Call Order)

### Flow A — First-Time Sign-In (Dynamic)

1. Client signs in with Dynamic → gets **Dynamic JWT**
2. SDK → `POST /auth/exchange` (Bearer = Dynamic JWT)
3. SDK → `GET /auth/me` → cache `etag`

### Flow B — Returning User (Dynamic)

1. SDK → `GET /auth/me` with `If-None-Match`
2. If stale/changed: `POST /auth/exchange` → `GET /auth/me`

### Flow C — Manual Wallet Sign-In (No Dynamic)

1. Client signs a challenge (Solana Action/Blink or SIWS-style)
2. Backend validates signature → issues **Axon Access Token**
3. SDK → `POST /auth/exchange` (Bearer = Axon token)
4. SDK → `GET /auth/me`

> If the signed wallet already belongs to a principal created via Dynamic, **the same principal** is returned (identity convergence).

### Flow D — Wallet Changes via Dynamic

1. Dynamic emits wallet connected/disconnected
2. SDK → `POST /auth/exchange` → `GET /auth/me`

### Flow E — Cold Boot (minimal)

If token exists: `POST /auth/exchange` → `GET /auth/me`
If no token: show logged-out UI.

---

## 6) Authentication & Validation (Middleware Posture)

* **Single header path:** always `Authorization: Bearer <token>`
* **Validator multiplexer (concept):**

    * If token looks like Dynamic: validate via JWKS → derive identity
    * If token is Axon Access Token: validate via Axon’s signing/issuer rules
* **No cookies, no server sessions.** All calls self-contained and stateless.

---

## 7) Identity Model (Business Invariants)

* **Principal:** one per user (the anchor ID = `axonId`)
* **Credentials:** one or more (e.g., `provider=dynamic`, `provider=siws`)
* **Wallet Ownerships:**

    * Unique **(principal, wallet)** pair
    * **State:** `verified` (trust for signing), `pending` (awaiting proof), `revoked` (removed)
    * **Access:** `signing` or `watch_only`
* **Per-Chain Defaults:** at most **one default per chain** per principal
* **Convergence rule:** Same wallet proven via any method maps to the same principal

---

## 8) Idempotency, Caching & Sync

* **Exchange** is idempotent (safe to retry); dedupes work; upserts only changes
* **Me** supports **ETag**; clients send `If-None-Match` to get fast 304s
* SDK should **exchange once** on boot when a token is present, then **me**

---

## 9) Error Semantics (Client-Facing)

* **400** — malformed header/JWT, size too big
* **401** — invalid/expired token; re-auth needed
* **409** — wallet ownership conflict (wallet owned by another principal)
* **422** — business rule violation (e.g., invalid default assignment)
* **429** — rate limit; honor `Retry-After`
* **500** — unexpected/external failure; retry with backoff

**SDK expectations:** map 401 → re-auth; 409 → open “resolve conflict” flow; 429 → backoff using headers; others → retry or surface error.

---

## 10) Security Posture (Essentials)

* **Dynamic JWT** validated via **JWKS** (issuer, alg, exp, sig)
* **Axon Access Token** short-lived; signed and audience-scoped
* **No long-term storage** of tokens in insecure persistence
* **Replay resistance:** rely on token expiry; consider jti checks where available
* **Principle of least privilege:** `me` returns only what UI needs

---

## 11) Observability & Ops

* **Structured logs** with `AxonId` scope once known
* **Metrics** per exchange (created, linked, defaults, conflicts)
* **Tracing** from SDK to API via correlation headers (optional)

---

## 12) Versioning & Change Safety

* **Stable paths**: `/api/v1/auth/exchange`, `/api/v1/auth/me`
* **Response evolution**: additive fields only; no breaking changes
* **Auth header** and semantics remain stable

---

## 13) SDK Responsibilities (Thin, Predictable)

* Attach bearer to all calls
* Orchestrate **exchange → me** on boot and on wallet changes
* Maintain `etag` and use conditional `me`
* Backoff/retry using rate-limit headers
* Surface conflicts and re-auth prompts

---

## 14) What We Explicitly Don’t Do

* No `X-Dynamic-Token` header (ever)
* No cookies/sessions
* No bespoke refresh logic for Dynamic (provider handles it)
* No JSON “bucket” fields in domain contracts exposed via API

---

## 15) Ready-to-Use Flow Cheatsheet

| Scenario                  | Calls (in order)                                    | Bearer          |
| ------------------------- | --------------------------------------------------- | --------------- |
| First login (Dynamic)     | `exchange` → `me`                                   | Dynamic JWT     |
| Returning (Dynamic)       | `me` (If-None-Match) → (if stale) `exchange` → `me` | Dynamic JWT     |
| Manual wallet sign-in     | Sign challenge → Axon token → `exchange` → `me`     | Axon token      |
| Wallets changed (Dynamic) | `exchange` → `me`                                   | Dynamic JWT     |
| Cold boot                 | `exchange` → `me`                                   | Any valid token |

---

This is the single high-level blueprint: one header, two endpoints, clear call order, idempotent sync, ETag-based reads, and identity convergence across Dynamic and manual sign-ins—simple, robust, and future-proof.
