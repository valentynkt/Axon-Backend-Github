# 15. Security

* **AuthN:** `JwtBearerHandler` + `ConfigurationManager` (JWKS). Validate `iss`, `aud` (if configured), `sub`, expiration, signature.
* **AuthZ:** Module-level — API requires valid token; no fine-grained RBAC in MVP.
* **Input Validation:** DTOs validated via FastEndpoints + FluentValidation in Application layer.
* **Transport:** HTTPS only; HSTS via platform defaults.
* **Headers:** Standard security headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, etc.) at gateway/app level.
* **Secrets:** Azure Key Vault for provider configuration; no secrets in logs.
* **CORS:** Restricted to Axon frontend origins; partner origins added via configuration.
* **Privacy:** **Do not persist or log contact identifiers**; ignore such claims if present in JWTs.

---
