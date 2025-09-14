# 7. Core Workflows

## New User Credential Exchange (`POST /auth/exchange`) - Epic 3 Wallet-First Resolution

```mermaid
sequenceDiagram
    participant Frontend
    participant Rate Limiter
    participant API Layer  
    participant App Handler
    participant JWKS Service
    participant Auth Service
    participant Database
    participant OTel

    Frontend->>+API Layer: POST /auth/exchange (Authorization: Bearer JWT)
    API Layer->>+Rate Limiter: Check rate limit (10/min per IP)
    Note over Rate Limiter: Returns 429 if exceeded
    Rate Limiter-->>-API Layer: Allowed
    
    API Layer->>+App Handler: ExchangeCredentialCommand (Direct - No Dual Pattern)
    App Handler->>+OTel: Start trace with correlation ID
    
    App Handler->>+Auth Service: ValidateTokenAsync(JWT)
    Auth Service->>+JWKS Service: Get signing keys (cached with Polly)
    JWKS Service-->>-Auth Service: Keys
    Auth Service-->>-App Handler: Result<ExchangeUserData>

    App Handler->>+Database: Check wallet ownership first (FindByWalletIdAsync)
    Database-->>-App Handler: Existing principal or null

    alt Wallet Owner Found
        App Handler->>App Handler: Use existing principal, add new credential if needed
    else No Wallet Owner
        App Handler->>+Database: FindByCredentialAsync (Compiled Query)
        Database-->>-App Handler: Principal or null
        App Handler->>App Handler: Create or load Principal aggregate
    end
    App Handler->>+Database: EnsureManyByChainAndAddressAsync (Batch)
    Database-->>-App Handler: Wallet IDs

    App Handler->>App Handler: Link verified ownerships, apply defaults
    App Handler->>+Database: SaveChangesAsync (Single Transaction)
    Database-->>-App Handler: Success

    App Handler->>+OTel: Log metrics (exchange_success, wallets_linked)
    OTel-->>-App Handler: Recorded
    App Handler-->>-API Layer: ExchangeDynamicTokenResponse
    API Layer-->>-Frontend: 200 OK + Rate Limit Headers
```

## Existing User Identity Retrieval (`GET /auth/me`) - Epic 2 with ETag Optimization

```mermaid
sequenceDiagram
    participant Frontend
    participant API Layer
    participant App Handler
    participant Auth Service
    participant ETag Service
    participant Database
    participant OTel

    Frontend->>+API Layer: GET /auth/me (Authorization: Bearer JWT, If-None-Match: "etag-hash")
    API Layer->>+App Handler: GetMyPrincipalQuery (Direct - Single Pattern)
    App Handler->>+OTel: Start trace with correlation ID

    App Handler->>+Auth Service: ValidateTokenAsync(JWT)
    Note over Auth Service: Uses cached JWKS via IJwksService
    Auth Service-->>-App Handler: Claims (iss, sub, provider)

    App Handler->>+Database: FindByCredentialAsync (Compiled Query)
    Database-->>-App Handler: Principal ID

    App Handler->>+ETag Service: GetPrincipalFingerprintAsync(principalId)
    ETag Service->>+Database: SELECT fingerprint (compiled query)
    Note over ETag Service: Hash: principal.updated_at + verified ownerships + defaults
    Database-->>-ETag Service: Current ETag hash
    ETag Service-->>-App Handler: Current ETag

    alt ETag matches If-None-Match
        App Handler->>+OTel: Log metric (etag_hit)
        OTel-->>-App Handler: Recorded
        App Handler-->>-API Layer: 304 Not Modified (No body)
        API Layer-->>-Frontend: 304 Not Modified
    else ETag different or missing
        App Handler->>+Database: GetByIdWithActiveOwnershipsAsync (Compiled Query)
        Database-->>-App Handler: Principal + Ownerships + Defaults
        
        App Handler->>+OTel: Log metric (etag_miss)
        OTel-->>-App Handler: Recorded
        App Handler-->>-API Layer: 200 + ETag Header + CurrentUserResult
        API Layer-->>-Frontend: 200 OK + ETag + Cache-Control: private, max-age=0
    end
```

---
