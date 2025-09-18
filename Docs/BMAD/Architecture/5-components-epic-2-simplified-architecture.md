# 5. Components (Epic 2 Simplified Architecture)

```mermaid
graph TD
    subgraph "Identity Module - Epic 2 Stabilized"
        API_Layer["API Layer<br/>(FastEndpoints + Rate Limiting)"]
        App_Layer["Application Layer<br/>(Single CQRS Handlers)"]
        Domain_Layer["Domain Layer<br/>(Aggregates, VOs - Unchanged)"]
        Infra_Layer["Infrastructure Layer<br/>(Separated Services)"]
        
        subgraph "Separated Infrastructure Services"
            JwksService["IJwksService<br/>(JWKS Caching + Polly)"]
            AuthService["DynamicAuthService<br/>(JWT Validation Only)"]
            ETagService["ETag Fingerprinting<br/>(Conditional GET)"]
            Repositories["Repositories<br/>(Compiled Queries)"]
        end
    end

    API_Layer --> App_Layer
    App_Layer --> Domain_Layer
    Infra_Layer -.implements.-> App_Layer
    Infra_Layer --> Domain_Layer
    Infra_Layer --> JwksService
    Infra_Layer --> AuthService
    Infra_Layer --> ETagService
    Infra_Layer --> Repositories
    AuthService --> JwksService
```

**Epic 2 Component Responsibilities:**

* **API Layer:** FastEndpoints with rate limiting middleware (10/min per IP), direct routing to single handlers, ETag header management.
* **Application Layer:** **Simplified** - Single canonical handlers (`ExchangeCredentialCommand`, `GetMyPrincipalQuery`), eliminated dual patterns.
* **Domain Layer:** **Unchanged** - Aggregates + invariants (pure C#).
* **Infrastructure Layer (Refactored):**
  * **DynamicAuthService:** Focused solely on JWT validation logic
  * **IJwksService:** Extracted JWKS caching, key rotation, Polly retry policies  
  * **ETag Service:** Deterministic fingerprint generation for conditional GET
  * **Repositories:** Enhanced with compiled queries, batch operations, ETag support
  * **Observability:** OpenTelemetry tracing, metrics, structured logging with correlation IDs

---
