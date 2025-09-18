# 8. REST API Specification (Epic 2 Production-Ready)

```yaml
openapi: 3.0.0
info:
  title: "Axon AI - Identity & Memory API"
  version: "2.0.0"
  description: "Production-hardened stateless identity service with rate limiting, ETag caching, and simplified architecture. Epic 2 stabilization complete."
servers:
  - url: "/api/v1"
    description: "API Version 1"

components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: "Provider-issued JWT (e.g., Dynamic.xyz)"

  schemas:
    WalletInfo:
      type: object
      properties:
        walletId: { type: string, description: "Internal wallet ULID (char(26))." }
        chainId:  { type: string, description: "e.g., 'solana'." }
        address:  { type: string, description: "Canonical on-chain address." }
        accessMode: { type: string, enum: [signing, watchOnly] }
        isVerified: { type: boolean }
    CurrentUserResult:
      description: "Snapshot of current user's identity."
      type: object
      properties:
        profile:
          type: object
          properties:
            axonId:   { type: string }
            riskTier: { type: string, enum: [low, medium, high] }
        wallets:
          type: array
          items: { $ref: '#/components/schemas/WalletInfo' }
        chainDefaults:
          type: object
          additionalProperties: { type: string }
          description: "Map<chainId, walletId>"

    ExchangeDynamicTokenResponse:
      description: "Result of exchange with operation metrics."
      type: object
      properties:
        axonId:           { type: string }
        created:          { type: boolean }
        walletsProcessed: { type: integer }
        walletsLinked:    { type: integer }
        defaultsApplied:  { type: integer }
        skipped:          { type: integer }
        conflicts:        { type: integer }

    ApiError:
      type: object
      properties:
        code:    { type: string }
        message: { type: string }
        details:
          type: object
          description: "Privacy-safe details; MUST NOT include other principal IDs."
          properties:
            chainId: { type: string }
            address: { type: string }

paths:
  /auth/exchange:
    post:
      summary: "Exchange provider JWT for Axon identity (Rate Limited)"
      description: |
        **Epic 2 Stabilized**: Validates a provider JWT and creates/updates the Axon Principal.
        Idempotent and safe to retry. **Rate-limited (10 req/min per IP)** with proper headers.
        Routes directly to ExchangeCredentialCommand (dual patterns eliminated).
      security: [{ bearerAuth: [] }]
      requestBody:
        required: false
        content:
          application/json:
            schema:
              type: object
              properties:
                desiredDefaults:
                  type: object
                  additionalProperties:
                    type: object
                    properties:
                      chainId: { type: string }
                      address: { type: string }
                riskTier:
                  type: string
                  enum: [low, medium, high]
      responses:
        '200': 
          description: "Exchange successful"
          headers:
            X-RateLimit-Remaining: { schema: { type: integer }, description: "Requests remaining in window" }
            X-RateLimit-Reset: { schema: { type: integer }, description: "Window reset time (epoch)" }
            X-Correlation-ID: { schema: { type: string }, description: "Request correlation ID" }
          content: 
            application/json: 
              schema: { $ref: '#/components/schemas/ExchangeDynamicTokenResponse' }
        '400': { description: "Malformed request/JWT" }
        '401': { description: "Invalid/expired JWT" }
        '409':
          description: "Wallet ownership conflict"
          content:
            application/json:
              schema: { $ref: '#/components/schemas/ApiError' }
        '422': { description: "Business rule violation" }
        '429':
          description: "Rate limit exceeded (10/min per IP)"
          headers:
            Retry-After: { schema: { type: string }, description: "Seconds to wait before retry" }
            X-RateLimit-Remaining: { schema: { type: integer }, description: "Always 0" }
            X-RateLimit-Reset: { schema: { type: integer }, description: "Window reset time (epoch)" }
            X-RateLimit-Limit: { schema: { type: integer }, description: "Rate limit (10)" }
        '500': { description: "Internal server error" }

  /auth/me:
    get:
      summary: "Get current authenticated user (Epic 2 ETag Optimized)"
      description: |
        **Epic 2 Enhanced**: Returns current Principal snapshot with conditional GET support.
        Routes directly to GetMyPrincipalQuery (dual patterns eliminated).
        ETag-based caching for optimal performance.
      security: [ { bearerAuth: [] } ]
      parameters:
        - in: header
          name: If-None-Match
          schema: { type: string }
          required: false
          description: "ETag from previous response for conditional GET"
          example: "\"sha256-abc123...\""
      responses:
        '200':
          description: "Principal data returned"
          headers:
            ETag: 
              schema: { type: string }
              description: "Deterministic fingerprint for conditional GET"
              example: "\"sha256-def456...\""
            Cache-Control: 
              schema: { type: string }
              description: "Caching directive"
              example: "private, max-age=0, must-revalidate"
            X-Correlation-ID: 
              schema: { type: string }
              description: "Request correlation ID"
          content:
            application/json:
              schema: { $ref: '#/components/schemas/CurrentUserResult' }
        '304': 
          description: "Not Modified - ETag matched If-None-Match"
          headers:
            ETag: 
              schema: { type: string }
              description: "Unchanged ETag value"
            Cache-Control: 
              schema: { type: string }
              example: "private, max-age=0, must-revalidate"
        '401': { description: "Invalid/expired JWT" }
        '404': { description: "Principal not found (no successful exchange yet)" }
```

---
