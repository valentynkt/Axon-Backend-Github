
**TL;DR:** Build a lean, high-signal test suite that proves the few things that matter: **Any Proof → One Principal**, **DB invariants can’t be violated**, **resolution is deterministic**, **JWT/crypto checks are right**, and **defaults behave**. Use a tight **test pyramid** (domain/property + DB-integration + a handful of E2E), deterministic fixtures, and concurrency probes for exclusivity. Below is a concise TDD plan you can copy into a separate doc.

---

# TDD — Identity Persistence & Constraints (KISS • 20/80)

## 1) Objective & Scope

* **Objective:** Prove, with minimal tests, that the implementation satisfies the business guarantees of the PRD (Dynamic + Wallet only).
* **Out of scope:** OIDC, device PoP/DPoP, nonce ledger, PDAs, realms/tenants.

## 2) Test Philosophy (20/80)

* **Test what can break the business:** DB constraints, exclusivity races, deterministic resolution, token/crypto validation, defaults.
* **Prefer invariants/property tests over exhaustiveness.**
* **Few, surgical E2E flows** to validate the seams (/auth/exchange, /auth/me).

## 3) Test Pyramid & Tooling

* **Domain/Property (≈50%)** — pure C# domain logic (no I/O).
* **DB-Integration (≈40%)** — real Postgres via Testcontainers; assert indexes/partial-unique constraints and transactional behavior.
* **E2E API (≈10%)** — minimal happy + edge flows over HTTP with fake JWKS and deterministic time.

*Harness hints:* Testcontainers-Postgres, WireMock (fake JWKS), deterministic clock provider, ed25519 test vectors.

---

## 4) Canonical Fixtures

* **Environments:** `mainnet`, `devnet`.
* **Chains:** `solana:mainnet`, `solana:devnet`.
* **Wallets:** `W1_main`, `W1_dev` (same base58 different env), `W2_main`.
* **Principals:** `P_A`, `P_B`.
* **Dynamic JWTs:** `dynA(kid1)`, `dynA_rotated(kid2)` (same subject, issuer).
* **Signed messages:** `sig_W1_msg_v1`, `sig_W1_msg_v2` (distinct TTLs).

---

## 5) High-Value Test Suites & Cases

### A) DB Invariants (Integration)

**Goal:** DB alone prevents identity violations.

1. **WALLET\_UNIQUE\_env\_chain\_address**

    * Insert `(mainnet, solana:mainnet, W1_main)` twice → 409/unique violation.
    * Insert `(devnet, solana:devnet, W1_dev)` succeeds (no collision).

2. **CREDENTIAL\_UNIQUE\_env\_provider\_issuer\_subject**

    * Same Dynamic `(env, provider=dynamic, issuer, subject)` upsert twice → one row, `last_seen_at` increases.

3. **OWNERSHIP\_PAIR\_UNIQUE**

    * Two inserts for `(P_A, Wallet(W1_main))` → second fails; only status/access mutation allowed.

4. **EXCLUSIVITY\_PARTIAL\_UNIQUE\_verified\_signing**

    * Create verified+signing for `(W1_main, P_A)`, then attempt verified+signing for `(W1_main, P_B)` → unique violation.

5. **DEFAULT\_UNIQUE\_per\_principal\_env\_chain**

    * Set default twice for same `(P_A, mainnet, solana)` → first replaced or violates unless using update path (assert chosen behavior).

6. **DEFAULT\_GUARD\_verified\_signing\_only**

    * Attempt to set default to watch-only or pending ownership → 422.

### B) Resolution Algorithm (Domain + Integration)

**Goal:** Deterministic “credential-first → wallet-fallback”.

7. **RESOLVE\_credential\_first**

    * Given Dynamic credential exists for `P_A`, exchanging with Dynamic JWT returns `P_A` regardless of wallet state.

8. **RESOLVE\_wallet\_verified\_wins**

    * `W1_main` has verified+signing for `P_A`; exchange with wallet sig returns `P_A`.

9. **RESOLVE\_single\_active\_owner**

    * `W1_main` has only watch-only for `P_A` (active) and none else; wallet sig resolves to `P_A`.

10. **RESOLVE\_ambiguity\_tie\_break**

* `W1_main` has active non-verified ownerships for `P_A` and `P_B`; apply authority ranking (msg > tx > watch-only) then earliest principal fallback; assert chosen principal.

11. **RESOLVE\_no\_match\_creates**

* No credential/wallet matches → new human principal is created; idempotent repeat yields same principal.

### C) State Transitions & Races (Integration + Concurrency)

**Goal:** Exclusivity under contention and auto-revocation.

12. **VERIFY\_exclusivity\_race\_two\_principals**

* In parallel, verify `W1_main` for `P_A` and `P_B` as signing. Exactly one commit; the other fails or is auto-revoked. Winner stable after single retry.

13. **AUTO\_REVOKE\_pending\_competitors**

* With multiple **pending** for same wallet, when `P_A` verifies, all others become `revoked (conflict_lost)`.

14. **REVERIFY\_revoked\_to\_verified**

* Ownership can move `revoked → verified` (no new row); exclusivity still holds.

### D) Defaults Behavior (Domain + Integration)

**Goal:** Auto-seed and strict clear on revoke.

15. **DEFAULT\_auto\_seed\_on\_first\_verified**

* First verified+signing on `(mainnet, solana)` auto-creates default if empty.

16. **DEFAULT\_clear\_on\_revoke**

* Revoking the defaulted ownership clears `PrincipalChainDefault` (strict policy).

### E) Token & Proof Validation (E2E light)

**Goal:** Minimal safety without overengineering.

17. **JWT\_iss\_aud\_enforced**

* Dynamic JWT with wrong `aud` or unknown `iss` → 401.

18. **JWT\_JWKS\_rotation**

* `kid1` valid; rotate to `kid2` (cache warm) → both validate; cache TTL honored.

19. **SIGN\_message\_TTL**

* Signed message with `exp ≤ now` or `issued_at` too old → 401.

20. **SIGN\_signature\_reuse\_guard (optional LRU)**

* Reuse identical `{message, signature}` within guard window → 401 (if you adopt the tiny in-memory LRU).

### F) Idempotency & Replays (Integration + E2E)

**Goal:** Safe repeats; no duplicates.

21. **EXCHANGE\_idempotent\_dynamic**

* Re-submit same Dynamic JWT: no new rows; timestamps updated.

22. **EXCHANGE\_idempotent\_wallet**

* Re-submit same valid wallet proof within TTL: no new rows; status unchanged.

### G) `/auth/me` Snapshot & Caching (E2E)

23. **ME\_snapshot\_contains\_defaults\_and\_wallets**

* After linking, `/auth/me` returns principal, wallets, and per-chain defaults; ETag works (304 on match).

---

## 6) Negative & Boundary Catalog (brief)

* Wrong chain/address formats rejected at normalization.
* Attempt to set default for a wallet not owned by principal → 422.
* Devnet wallet never collides with mainnet wallet (same base58).
* Attempt to create ownership beyond allowed max (if you keep the “max 10 wallets” rule) → 422.

---

## 7) Concurrency & Time Controls

* **Deterministic clock** for TTL tests (inject `IClock`).
* **Parallel verify** using two transactions + barriers to force race; assert single winner and auto-revokes.

---

## 8) Observability Assertions (light)

* For E2E tests 7/8/11/12/16: assert log markers for `resolution_path={credential|wallet|created}` and `auto_revoke reason=conflict_lost`.
* Not full audit trails—just enough to debug.

---

## 9) Performance Probes (sanity, not microbench)

* Single warm-path `/auth/exchange` (Dynamic + Wallet) runs **<100ms P95** with JWKS cache warm and indexed DB.
* Constraint insert failures occur **<20ms** (fast fail on unique/partial-unique).

---

## 10) Migration Tests (once, before rollout)

* **Backfill environment** for existing rows; ensure no duplicate keys post-migration (choose survivor deterministically).
* **ChainId normalization** maps legacy values to `solana:mainnet|devnet`; uniqueness holds.

---

## 11) CI Gates (fail-fast)

* **Tier-A (must pass):** A1–A6, B7–B11, C12–C14, D15–D16, E17–E19, F21–F22.
* **Optional:** E20 if LRU guard is implemented.
* **E2E smoke** runs on PR; full DB-integration runs nightly.

---

## 12) Exit Criteria (ready to ship)

* All Tier-A tests green.
* Two-run concurrency probe stable (no flaky exclusivity).
* E2E latency probe within target locally and once in staging.
* Migration rehearsal completed with zero post-migrate dupes.

---

This TDD gives you **maximum assurance with minimal tests**: it nails the invariants, the resolution determinism, the few race conditions that actually matter, and the simple token/proof safety checks—without adding the complexity you intentionally deferred.
