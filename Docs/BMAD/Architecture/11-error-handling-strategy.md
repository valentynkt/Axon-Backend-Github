# 11. Error Handling Strategy

* **Result Pattern:** Domain workflows return `Result<Success, Error>`; API maps to HTTP codes.
* **Mapping**

  * Validation/auth/JWT → **400/401**
  * Invariant violations (e.g., conflicting ownership) → **409**
  * Other business rule failures → **422**
  * Rate limit → **429** with `Retry-After`, `X-RateLimit-*`
  * Unexpected → **500**
* **Privacy for 409:** Response MUST NOT include any principal identifiers other than the caller’s. Use `ApiError.details = { chainId, address }` only.
* **Logging:** Include correlation ID (trace id), principal id (when resolved), provider and wallet identifiers **without PII**.

---
