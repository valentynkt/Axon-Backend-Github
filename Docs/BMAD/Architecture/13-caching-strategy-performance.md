# 13. Caching Strategy & Performance

## Production Caching (Epics 1-3) ✅
* **JWKS:** Managed by `ConfigurationManager`; keys cached with provider-aligned TTL and proactive refresh
* **ETag for `/auth/me`:** Compare `If-None-Match` vs DB fingerprint; return **304** if unchanged
* **Cache-Control:** `private, max-age=0, must-revalidate` (frontend always validates with ETag)

## Epic 4: Progressive Cache Hierarchy (Pending Implementation) 🔄
```
Request Scope (0ms) → Memory Cache (<1ms) → Database (20ms)
HttpContext.Items → IMemoryCache → FindByDynamicUserIdAsync
```

**Performance Targets:**
- Identity Resolution (cached): 50ms → <1ms (50x improvement)
- Same-request calls: 50ms each → 0ms (HttpContext.Items)
- Cache hit rate: 0% → >95% after warmup
- Database queries: 100% → <5% (cache-first strategy)

---
