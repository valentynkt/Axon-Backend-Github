# 22. Next Steps & Implementation Focus

## 🎯 Epic 4: Smart User Context Caching (ACTIVE - Pending Implementation)

**Current Priority:** Implement progressive cache hierarchy for 50x identity resolution performance improvement.

**Implementation Roadmap:**
1. **AxonId → AxonUserId rename** (~23 files affected)
2. **Enhanced ICurrentUserService** with async AxonUserId resolution
3. **Smart caching in HttpContextUserService** (Request → Memory → Database)
4. **Chat module integration** replacing DefaultCurrentUserService stubs

**Target Outcomes:**
- Identity resolution: 50ms → <1ms (cached scenarios)
- Database load reduction: 100% → <5% queries
- Cache hit rate: >95% after warmup

## ✅ Completed Foundation (Epics 1-3)

**Epic 3** ✅ Wallet-first identity resolution prevents duplicate principals
**Epic 2** ✅ Production hardening: ETag caching, rate limiting, observability
**Epic 1** ✅ Core identity service with DDD + CQRS patterns

## 🔄 Future Enhancements

- **Enhanced wallet verification** with on-chain signature validation
- **Multi-chain wallet support** (Ethereum, Polygon, BSC)
- **Direct wallet authentication** (Solana Sign-In without OAuth)

---

## Appendix A — HTTP Error Codes (Canonical)

* `400` Malformed JWT/request
* `401` Invalid/expired JWT
* `409` Wallet ownership conflict (verified & signing already owned)
* `422` Business rule violation (non-conflict)
* `429` Too Many Requests (rate limit)
* `500` Unexpected

## Appendix B — Consistency Guarantees

* **Idempotency:** Unique/partial-unique constraints + upsert patterns + transaction boundaries.
* **Statelessness:** Every call validated by provider JWT; no server tokens.
* **Deterministic Caching:** ETag from DB fingerprint across principal + related rows (verified & signing ownerships + defaults).
* **Privacy:** MVP persists **no contact identifiers**; logs and audits exclude PII.
* **Wallet-First Resolution:** Epic 3 ensures single identity per wallet set, preventing authentication method fragmentation.

