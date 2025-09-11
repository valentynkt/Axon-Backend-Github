# 13. Caching & ETags

* **JWKS:** Managed by `ConfigurationManager`; keys cached with provider-aligned TTL and proactive refresh.
* **ETag for `/auth/me`:** Compare `If-None-Match` vs DB fingerprint; return **304** if unchanged.
* **Cache-Control:** `private, max-age=0, must-revalidate` (frontend always validates with ETag).

---
