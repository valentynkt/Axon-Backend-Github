# Dynamic.xyz Authentication Integration - Product Requirements Document (PRD)

**Document Version:** 1.0  
**Date:** August 27, 2025  
**Author:** Product Engineering Team  
**Status:** Ready for Implementation  
**Based on:** Business Requirements Document v1.0  

---

## 1. Product Overview

### 1.1 Product Definition

The Dynamic.xyz Authentication Integration is a backend-only authentication system that enables Axon AI to provide seamless wallet-based authentication for web3 users while maintaining enterprise-grade security and performance standards.

### 1.2 Product Objectives

**Primary Goals:**
- Implement JWT-based authentication using Dynamic.xyz as the identity provider
- Enable wallet-first authentication supporting 30+ wallet providers (Phantom, Solflare, MetaMask, WalletConnect, etc.)
- Provide sub-100ms user/wallet data access through local mirroring
- Ensure enterprise-grade security with RS256 JWT validation and JWKS key rotation
- Design for provider outages via retries/backoff; publish internal SLOs and track upstream availability

**Success Metrics:**
- JWT validation latency: <100ms (local JWKS cache for sub-50ms typical)
- Local data query performance: <50ms (PostgreSQL optimization)
- Webhook processing time: <5s acknowledgment, async completion
- Design for resilience with circuit breakers and retry policies
- JWKS caching (10-minute TTL) for availability during API outages

**Business Value:**
- Reduced development time: 8-12 weeks saved vs custom authentication
- Enterprise compliance: SOC 2 Type II, GDPR, CCPA ready from day one
- Scalability: Proven infrastructure handling millions of authentication requests
- Web3 ecosystem integration: Native support for Solana trading workflows

### 1.3 Product Scope

**In Scope (Backend):**
- JWT validation and user context services
- Dynamic.xyz Management API integration
- Local PostgreSQL data mirroring
- Real-time webhook processing
- Authentication middleware and behaviors

**Out of Scope:**
- Frontend SDK integration (handled by frontend team)
- Wallet connection UI/UX
- Custom session management (relying on Dynamic.xyz JWTs)
- Social authentication flows (delegated to Dynamic.xyz)

---

## 2. User Stories & Acceptance Criteria

### 2.1 Core Authentication Stories

#### Story 1: JWT Token Validation
**As a** backend service  
**I want to** validate Dynamic.xyz JWT tokens  
**So that** I can authenticate users and extract user context

**Acceptance Criteria:**
- [ ] Validate JWT using RS256 algorithm with Dynamic.xyz JWKS keys
- [ ] Cache JWKS keys for 10 minutes to improve performance
- [ ] Extract `sub` (Dynamic user id) and environment id from JWT claims (do NOT derive wallets or Axon scopes from JWT)
- [ ] Handle token expiration and signature validation errors gracefully
- [ ] Support clock skew tolerance of up to 5 minutes
- [ ] Return structured error responses for different validation failures

#### Story 2: User Data Synchronization
**As a** backend service  
**I want to** maintain local copies of user and wallet data  
**So that** I can provide fast data access without API calls

**Acceptance Criteria:**
- [ ] Sync user data from Dynamic.xyz API on first authentication
- [ ] Update local data when webhooks are received
- [ ] Perform periodic reconciliation for data consistency
- [ ] Handle API rate limits with exponential backoff
- [ ] Track last sync timestamps for all user records
- [ ] Maintain data integrity constraints on user/wallet IDs

#### Story 3: Real-time Webhook Processing
**As a** backend service  
**I want to** process Dynamic.xyz webhooks immediately  
**So that** local data stays synchronized with the source of truth

**Acceptance Criteria:**
- [ ] Validate webhook signatures using Dynamic.xyz secrets
- [ ] Process users.created, users.updated, users.deleted events  
- [ ] Process wallets.linked, wallets.unlinked events
- [ ] Acknowledge webhooks within 5 seconds with HTTP 200/202
- [ ] Queue webhook processing for high availability
- [ ] Log all webhook events for troubleshooting

### 2.2 Data Access Stories

#### Story 4: Current User Service
**As a** API endpoint  
**I want to** access current user information  
**So that** I can provide personalized responses

**Acceptance Criteria:**
- [ ] Implement ICurrentUserService interface
- [ ] Extract user ID from JWT claims
- [ ] Return user profile with connected wallets
- [ ] Cache user lookups for <50ms response time
- [ ] Handle cases where user data is not yet synced
- [ ] Support anonymous requests where appropriate

#### Story 5: Wallet Information Access
**As a** trading service  
**I want to** access user's connected wallet information  
**So that** I can enable wallet-specific functionality

**Acceptance Criteria:**
- [ ] Return all connected wallets for authenticated user
- [ ] Include wallet metadata (chain, provider, properties)
- [ ] Indicate primary/selected wallet
- [ ] Filter wallets by blockchain type if requested
- [ ] Return wallet connection timestamps
- [ ] Handle cases where wallet data is stale

### 2.3 Security Stories

#### Story 6: Scope-based Authorization
**As a** protected endpoint  
**I want to** validate user permissions  
**So that** I can enforce access control

**Acceptance Criteria:**
- [ ] Check JWT scopes against required permissions
- [ ] Handle "requiresAdditionalAuth" scope appropriately
- [ ] Return 403 Forbidden for insufficient permissions
- [ ] Log authorization failures for security monitoring
- [ ] Support multiple scope requirements per endpoint
- [ ] Complete JWT validation in <100ms (including JWKS)

#### Story 7: MFA Detection and Handling
**As a** sensitive operation endpoint  
**I want to** detect when MFA is required  
**So that** I can enforce additional security measures

**Acceptance Criteria:**
- [ ] Identify "requiresAdditionalAuth" in JWT scopes
- [ ] Return appropriate MFA challenge response with supported methods (TOTP, WebAuthn, SMS)
- [ ] Block sensitive operations until MFA is satisfied
- [ ] Log MFA enforcement events for security audit
- [ ] Support different MFA method requirements per operation
- [ ] Handle MFA bypass for admin operations with additional logging

### 2.4 Multi-Wallet Management Stories

#### Story 8: Primary Wallet Management
**As a** user with multiple connected wallets  
**I want to** designate a primary wallet for transactions  
**So that** the system knows which wallet to use by default

**Acceptance Criteria:**
- [ ] Track which wallet was last selected/used per user
- [ ] Allow explicit primary wallet designation
- [ ] Update primary wallet selection via API calls to Dynamic.xyz
- [ ] Sync primary wallet changes via webhooks immediately
- [ ] Handle cases where primary wallet is disconnected
- [ ] Provide wallet priority ordering for fallback selection
- [ ] Log all wallet selection changes for audit

#### Story 9: Wallet Connection Event Processing  
**As a** trading service  
**I want to** be notified immediately when wallets are connected/disconnected  
**So that** I can update trading capabilities in real-time

**Acceptance Criteria:**
- [ ] Process `wallets.linked` webhooks within 5 seconds
- [ ] Process `wallets.unlinked` webhooks within 5 seconds  
- [ ] Update local wallet cache immediately on connection events
- [ ] Trigger wallet capability refresh (balance checks, permissions)
- [ ] Notify downstream services of wallet connectivity changes
- [ ] Handle bulk wallet connection/disconnection scenarios
- [ ] Maintain wallet connection history for analytics

---

## 3. Feature Specifications

### 3.1 Identity Module Structure

```
src/Modules/Identity/
├── Domain/
│   ├── Users/
│   │   ├── User.cs                    # User aggregate
│   │   ├── UserId.cs                  # Strong ID
│   │   ├── DynamicUserId.cs          # Dynamic user ID
│   │   ├── UserMetadata.cs           # User metadata value object
│   │   └── IUserRepository.cs         # Repository contract
│   ├── Wallets/
│   │   ├── Wallet.cs                  # Wallet entity
│   │   ├── WalletId.cs               # Strong ID
│   │   ├── DynamicWalletId.cs        # Dynamic wallet ID
│   │   ├── WalletProperties.cs       # Wallet properties value object
│   │   └── BlockchainType.cs         # Enum
│   ├── Authentication/
│   │   ├── JwtPayload.cs             # JWT claims
│   │   ├── VerifiedCredential.cs     # Verified credentials entity
│   │   ├── ExternalAuthProvider.cs   # External auth config
│   │   └── AuthenticationEvents.cs    # Domain events
│   └── Shared/
│       ├── DomainEvents.cs           # Common domain events
│       └── ValueObjects.cs           # Shared value objects
├── Application/
│   ├── Authentication/
│   │   ├── ValidateJwtCommand.cs     # JWT validation
│   │   ├── GetCurrentUserQuery.cs    # User context
│   │   └── RefreshUserDataCommand.cs # Data sync
│   ├── Users/
│   │   ├── SyncUserCommand.cs        # User sync
│   │   ├── GetUserQuery.cs           # User retrieval
│   │   └── UpdateUserCommand.cs      # User updates
│   ├── Wallets/
│   │   ├── SyncWalletsCommand.cs     # Wallet sync
│   │   ├── SetPrimaryWalletCommand.cs # Primary wallet management
│   │   ├── ProcessWalletEventCommand.cs # Webhook processing
│   │   └── GetUserWalletsQuery.cs    # Wallet retrieval
│   └── Webhooks/
│       ├── ProcessWebhookCommand.cs  # Webhook processing
│       ├── ValidateWebhookCommand.cs # Signature validation
│       └── GetWebhookStatusQuery.cs  # Processing status
└── Infrastructure/
    ├── Authentication/
    │   ├── JwtValidationService.cs    # JWT validation
    │   ├── DynamicAuthenticationService.cs # Auth service
    │   └── CurrentUserService.cs      # User context
    ├── External/
    │   ├── DynamicApiClient.cs        # API client
    │   ├── DynamicUserService.cs      # User API
    │   ├── DynamicWalletService.cs    # Wallet API
    ├── Persistence/
    │   ├── IdentityDbContext.cs       # EF context
    │   ├── UserRepository.cs          # User repo
    │   ├── WalletRepository.cs        # Wallet repo
    │   └── ConfigurationRepository.cs # App config repo
    ├── Webhooks/
    │   ├── DynamicWebhookProcessor.cs # Webhook handler
    │   ├── WebhookSignatureValidator.cs # Security
    │   ├── WebhookRetryHandler.cs     # Retry logic
    │   └── WebhookEventMapper.cs      # Event type mapping
    └── Caching/
        ├── JwksCacheService.cs        # JWKS key caching
        ├── UserCacheService.cs        # User data caching
        └── WalletCacheService.cs      # Wallet data caching
```

### 3.2 Authentication Pipeline Integration

#### 3.2.1 Authentication Behavior
```csharp
public sealed class AuthenticationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IJwtValidationService _jwtService;
    private readonly ICurrentUserService _currentUserService;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validate JWT if Authorization header present
        // Set current user context
        // Continue pipeline
    }
}
```

#### 3.2.2 Current User Service Implementation
```csharp
public interface ICurrentUserService
{
    UserId? CurrentUserId { get; }
    DynamicUserId? DynamicUserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Scopes { get; }
    Task<Result<User>> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
```

### 3.3 Data Mirroring Architecture

#### 3.3.1 User Entity Model
```csharp
public sealed class User : AggregateRoot<UserId>
{
    public DynamicUserId DynamicUserId { get; private set; }
    public string Email { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Username { get; private set; }
    public DateTime? FirstVisit { get; private set; }
    public DateTime? LastVisit { get; private set; }
    public DateTime DynamicCreatedAt { get; private set; }
    public DateTime DynamicUpdatedAt { get; private set; }
    public DateTime LastSyncedAt { get; private set; }
    public UserMetadata Metadata { get; private set; }
    public IReadOnlyList<Wallet> Wallets => _wallets.AsReadOnly();
    
    private readonly List<Wallet> _wallets = [];
    
    public static Result<User> Create(/* parameters */);
    public Result UpdateFromDynamic(/* dynamic data */);
    public void AddWallet(Wallet wallet);
    public void RemoveWallet(WalletId walletId);
}
```

#### 3.3.2 Wallet Entity Model
```csharp
public sealed class Wallet : Entity<WalletId>
{
    public UserId UserId { get; private set; }
    public DynamicWalletId DynamicWalletId { get; private set; }
    public string Address { get; private set; }
    public BlockchainType Chain { get; private set; }
    public WalletProvider Provider { get; private set; }
    public string? WalletName { get; private set; }
    public WalletProperties Properties { get; private set; }
    public DateTime? LastSelectedAt { get; private set; }
    public DateTime ConnectedAt { get; private set; }
    public DateTime LastSyncedAt { get; private set; }
    
    public static Result<Wallet> Create(/* parameters */);
    public Result UpdateFromDynamic(/* dynamic data */);
}
```

---

## 4. API Specifications

### 4.1 Authentication Endpoints

#### 4.1.1 JWT Token Exchange
```
POST /api/auth/exchange
```

**Purpose:** Validate Dynamic.xyz JWT and establish Axon user context

**Request:**
```json
{
  "authToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9..."
}
```

**Headers:**
- `Authorization: Bearer <token>` (alternative to body)
- `Idempotency-Key: <uuid>` (optional, for safe retries)

**Processing:**
1. Validate JWT signature via JWKS endpoint
2. Extract user identity from JWT `sub` claim
3. Fetch user and wallet data from Dynamic.xyz Management API
4. Upsert locally for performance
5. Return local user context
6. **Idempotency:** If `Idempotency-Key` provided, return same result for repeated calls
7. **Concurrency:** Use single DB transaction to upsert users+wallets; handle unique violations gracefully
8. **Provider errors:** On Dynamic.xyz 429/5xx, return `502 provider_unavailable` with `Retry-After`

**Response (Success):**
```json
{
  "userId": "01H8EXAMPLE",
  "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f",
  "email": "user@example.com",
  "wallets": [
    {
      "id": "01H8WALLETID",
      "dynamicWalletId": "wallet-uuid",
      "address": "So11111111111111111111111111111111111111112",
      "chain": "SOL",
      "provider": "phantom",
      "lastSelectedAt": "2025-08-27T12:00:00Z"
    }
  ],
  "syncedAt": "2025-08-27T12:00:00Z",
  "syncStatus": "completed"
}
```

**Response (Error - RFC 7807 Problem JSON):**
```json
{
  "type": "https://axon.example.com/probs/auth/invalid-token",
  "title": "Invalid JWT Token", 
  "status": 401,
  "detail": "JWT signature validation failed using RS256 algorithm",
  "code": "AUTH001",
  "correlationId": "req_abc123def456",
  "timestamp": "2025-08-27T12:00:00Z"
}
```

#### 4.1.2 Current User Profile
```
GET /api/auth/me
```

**Purpose:** Get current authenticated user profile with wallet information

> **LOCAL-ONLY ENDPOINT:** This endpoint never calls Dynamic.xyz API. It returns the local mirror data with sync status.

**Headers:**
```
Authorization: Bearer {dynamic-jwt-token}
```

**Response (Data Available):**
```json
{
  "user": {
    "id": "01H8EXAMPLE",
    "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f",
    "email": "user@example.com",
    "displayName": "John Doe",
    "username": "johndoe",
    "firstVisit": "2025-08-01T10:00:00Z",
    "lastVisit": "2025-08-27T12:00:00Z",
    "metadata": {
      "preferences": {"theme": "dark"},
      "tags": ["premium", "beta"]
    }
  },
  "wallets": [
    {
      "id": "01H8WALLETID",
      "dynamicWalletId": "wallet-uuid",
      "address": "So11111111111111111111111111111111111111112",
      "chain": "SOL",
      "provider": "phantom",
      "walletName": "Main Wallet",
      "lastSelectedAt": "2025-08-27T11:30:00Z",
      "connectedAt": "2025-08-01T10:00:00Z"
    }
  ],
  "syncedAt": "2025-08-27T12:00:00Z",
  "syncStatus": "completed" // Values: "pending" | "completed" | "stale"
}
```

**Response (Sync Pending):**
```json
{
  "user": {
    "id": "01H8EXAMPLE",
    "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f"
  },
  "wallets": [],
  "syncStatus": "pending",
  "message": "User data synchronization in progress"
}
```

### 4.2 Webhook Event Processing

#### 4.2.1 Dynamic.xyz Webhook Receiver
```
POST /api/webhooks/dynamic
```

**Purpose:** Process Dynamic.xyz lifecycle events

**Headers:**
```
X-Dynamic-Signature: sha256=<hmac_signature>
Content-Type: application/json
```

**Supported Event Types (Based on Dynamic.xyz Documentation):**
- User lifecycle: `users.created`, `users.updated`, `users.deleted`
- Session events: `sessions.created`, `sessions.deleted`
- Wallet events: `wallets.linked`, `wallets.unlinked`, `wallets.updated`
- Environment events: `environments.updated`

**Webhook Payload Structure:**
```json
{
  "createdAt": "2023-12-01T10:30:00.000Z",
  "data": {
    // Event-specific data object
  },
  "eventId": "evt_1234567890abcdef",
  "eventName": "users.created", // or other event types
  "webhookId": "wh_0987654321fedcba"
}
```

**Request (User Created Event):**
```json
{
  "createdAt": "2025-08-27T12:00:00.000Z",
  "data": {
    "alias": "john.doe.wallet",
    "chains": ["EVM"],
    "createdAt": "2025-08-27T12:00:00.000Z",
    "email": "john.doe@example.com",
    "environmentId": "95b11417-f18f-457f-8804-68e361f9164f",
    "firstName": "John",
    "lastName": "Doe",
    "id": "user_abc123def456",
    "lastVisit": "2025-08-27T12:00:00.000Z",
    "lists": ["premium", "early-access"],
    "metadata": {
      "registrationSource": "wallet_connect",
      "referralCode": "FRIEND123"
    },
    "newUser": true,
    "updatedAt": "2025-08-27T12:00:00.000Z",
    "userId": "95b11417-f18f-457f-8804-68e361f9164f",
    "verifiedCredentials": [
      {
        "address": "0x742d35Cc6bF4532a35b8c5F9D4476D6a6B8C4aF6",
        "chain": "EVM",
        "format": "blockchain",
        "id": "cred_wallet_789",
        "lastSelectedAt": "2025-08-27T12:00:00.000Z",
        "walletName": "MetaMask",
        "walletProvider": "metamask"
      }
    ]
  },
  "eventId": "evt_user_created_123",
  "eventName": "users.created",
  "webhookId": "wh_abc123def456"
}
```

**Request (Wallet Linked Event):**
```json
{
  "createdAt": "2025-08-27T12:00:00.000Z",
  "data": {
    "chain": "SOL",
    "connectedAt": "2025-08-27T12:00:00.000Z",
    "id": "wallet_sol_456",
    "lastSelectedAt": "2025-08-27T12:00:00.000Z",
    "provider": "phantom",
    "publicKey": "So11111111111111111111111111111111111111112",
    "userId": "95b11417-f18f-457f-8804-68e361f9164f",
    "walletName": "Primary Solana"
  },
  "eventId": "evt_wallet_linked_456",
  "eventName": "wallets.linked",
  "webhookId": "wh_def456ghi789"
}
```

**Request (Session Created Event):**
```json
{
  "createdAt": "2025-08-27T12:00:00.000Z",
  "data": {
    "id": "session_789ghi012",
    "userId": "95b11417-f18f-457f-8804-68e361f9164f",
    "createdAt": "2025-08-27T12:00:00.000Z",
    "lastAccessedAt": "2025-08-27T12:00:00.000Z",
    "ipAddress": "192.168.1.100",
    "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36"
  },
  "eventId": "evt_session_created_789",
  "eventName": "sessions.created", 
  "webhookId": "wh_ghi789jkl012"
}
```

**Response Format:**
```json
{
  "received": true,
  "processedAt": "2025-08-27T12:00:02.123Z",
  "syncScheduled": true,
  "acknowledgment": {
    "eventId": "evt_user_created_123",
    "webhookId": "wh_abc123def456",
    "status": "processed",
    "retryCount": 0
  }
}
```

#### 4.2.2 Webhook Signature Validation
**HMAC-SHA256 Validation Process:**
```
signature = HMAC-SHA256(webhook_secret, request_body)
expected_header = "sha256=" + base64(signature)
```

**Security Requirements:**
- Constant-time signature comparison to prevent timing attacks
- Request body validation before processing
- Duplicate event detection using `eventId`
- Timeout protection (30-second processing limit)
```

### 4.3 Compliance & Data Management Endpoints

#### 4.3.1 GDPR Data Request Processing
```
POST /api/compliance/data-request
```

**Purpose:** Process GDPR/CCPA data requests

**Request:**
```json
{
  "requestType": "access", // "access", "rectification", "erasure", "portability"
  "userIdentifier": "user@example.com",
  "requestorId": "compliance-officer-id",
  "legalBasis": "GDPR Article 15",
  "deadline": "2025-09-26T00:00:00Z"
}
```

**Response:**
```json
{
  "requestId": "req_123456",
  "status": "processing",
  "estimatedCompletion": "2025-09-01T12:00:00Z",
  "dataCategories": ["profile", "wallets", "authentication_events"]
}
```

#### 4.3.2 Data Export Endpoint
```
GET /api/compliance/export/{requestId}
```

**Purpose:** Download user data export in machine-readable format

**Response Headers:**
```
Content-Type: application/json
Content-Disposition: attachment; filename="user_data_export.json"
```

### 4.4 Analytics & Monitoring Endpoints

#### 4.4.1 Authentication Metrics
```
GET /api/analytics/auth-metrics
```

**Purpose:** Retrieve authentication performance metrics

**Query Parameters:**
- `timeRange`: `1h`, `24h`, `7d`, `30d`
- `groupBy`: `hour`, `day`, `week`
- `includeBreakdown`: boolean

**Response:**
```json
{
  "timeRange": "24h",
  "metrics": {
    "totalAuthentications": 15420,
    "successfulAuthentications": 15234,
    "failedAuthentications": 186,
    "successRate": 98.79,
    "averageResponseTime": 45.2,
    "p95ResponseTime": 89.1
  },
  "breakdown": {
    "byProvider": {
      "phantom": 8234,
      "metamask": 4123,
      "walletconnect": 2867
    },
    "byChain": {
      "SOL": 9456,
      "ETH": 5964
    }
  }
}
```

### 4.5 Internal API Specifications

#### 4.5.1 User Synchronization
```
POST /internal/users/sync
```

**Purpose:** Trigger user data synchronization from Dynamic.xyz

**Request:**
```json
{
  "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f",
  "forceRefresh": false,
  "syncWallets": true,
  "syncMetadata": true
}
```

#### 4.5.2 Batch User Sync
```
POST /internal/users/batch-sync
```

**Purpose:** Perform bulk user data reconciliation

**Request:**
```json
{
  "userIds": ["user1", "user2", "user3"],
  "maxAge": "6h",
  "batchSize": 10,
  "priority": "low"
}
```

#### 4.5.3 Webhook Retry Processing
```
POST /internal/webhooks/retry
```

**Purpose:** Manually retry failed webhook processing

**Request:**
```json
{
  "webhookId": "wh_abc123",
  "eventType": "user.created",
  "maxRetries": 3,
  "backoffStrategy": "exponential"
}
```

### 4.6 Enterprise & External Authentication Endpoints

#### 4.6.1 External Provider Registration
```
POST /api/auth/external/providers
```

**Purpose:** Register external authentication provider (Enterprise feature)

**Request:**
```json
{
  "providerName": "corporate-sso",
  "jwksUri": "https://corporate-auth.company.com/.well-known/jwks",
  "validIssuer": "https://corporate-auth.company.com",
  "claimMappings": {
    "email": "email",
    "name": "full_name",
    "roles": "user_roles"
  },
  "enabled": true
}
```

**Response:**
```json
{
  "providerId": "provider_abc123",
  "status": "active",
  "validationStatus": "jwks_validated",
  "createdAt": "2025-08-27T12:00:00Z"
}
```

#### 4.6.2 External JWT Exchange
```
POST /api/auth/external/exchange
```

**Purpose:** Exchange external provider JWT for Dynamic.xyz token

**Request:**
```json
{
  "externalToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...",
  "providerId": "provider_abc123",
  "createIfNotExists": true
}
```

**Response:**
```json
{
  "dynamicToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...",
  "userId": "01H8EXAMPLE",
  "isNewUser": false,
  "mappedClaims": {
    "email": "employee@company.com",
    "roles": ["admin", "user"]
  }
}
```

#### 4.6.3 Multi-Factor Authentication Status
```
GET /api/auth/mfa/status
```

**Purpose:** Check MFA requirements for current user

**Response:**
```json
{
  "mfaRequired": true,
  "availableMethods": ["totp", "webauthn", "sms"],
  "completedMethods": ["totp"],
  "gracePeriodExpires": "2025-08-30T12:00:00Z"
}
```

#### 4.6.4 Session Management
```
GET /api/auth/sessions
DELETE /api/auth/sessions/{sessionId}
```

**Purpose:** Manage user authentication sessions

**GET Response:**
```json
{
  "sessions": [
    {
      "sessionId": "session_789ghi012",
      "createdAt": "2025-08-27T12:00:00Z",
      "lastAccessedAt": "2025-08-27T14:30:00Z",
      "ipAddress": "192.168.1.100",
      "userAgent": "Mozilla/5.0...",
      "isCurrent": true
    }
  ]
}
```

---

## 5. Data Models & Schemas

### 5.1 Database Schema

#### 5.1.1 Users Table
```sql
CREATE TABLE identity.users (
    id VARCHAR(26) PRIMARY KEY,
    dynamic_user_id UUID UNIQUE NOT NULL,
    dynamic_environment_id UUID NOT NULL,
    email CITEXT, -- Case-insensitive for uniqueness
    display_name VARCHAR(100),
    username VARCHAR(50),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    dynamic_created_at TIMESTAMPTZ NOT NULL,
    dynamic_updated_at TIMESTAMPTZ NOT NULL,
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_dynamic_user_id ON identity.users(dynamic_user_id);
CREATE INDEX idx_users_email ON identity.users(email) WHERE email IS NOT NULL;
CREATE INDEX idx_users_last_synced ON identity.users(last_synced_at);
CREATE INDEX idx_users_environment ON identity.users(dynamic_environment_id);
```

#### 5.1.2 Wallets Table
```sql
CREATE TABLE identity.wallets (
    id VARCHAR(26) PRIMARY KEY,
    user_id VARCHAR(26) NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    dynamic_wallet_id UUID UNIQUE NOT NULL,
    address VARCHAR(128) NOT NULL COLLATE "C", -- Normalized/lowercase for consistency
    chain VARCHAR(20) NOT NULL,
    provider VARCHAR(50) NOT NULL,
    wallet_name VARCHAR(100),
    properties JSONB NOT NULL DEFAULT '{}',
    last_selected_at TIMESTAMPTZ,
    connected_at TIMESTAMPTZ NOT NULL,
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_wallets_user_id ON identity.wallets(user_id);
CREATE INDEX idx_wallets_dynamic_wallet_id ON identity.wallets(dynamic_wallet_id);
CREATE INDEX idx_wallets_address_chain ON identity.wallets(address, chain);
CREATE UNIQUE INDEX idx_wallets_address_chain_unique ON identity.wallets(address, chain);
CREATE UNIQUE INDEX idx_wallets_user_dynamic_wallet ON identity.wallets(user_id, dynamic_wallet_id);
-- Partial index for hot queries
CREATE INDEX idx_wallets_sol ON identity.wallets(chain) WHERE chain = 'SOL';
```

#### 5.1.3 Processed Events Table (Webhook Deduplication)
```sql
CREATE TABLE identity.processed_events (
    event_id VARCHAR(255) PRIMARY KEY,
    event_type VARCHAR(50) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '7 days')
);

-- TTL cleanup index
CREATE INDEX idx_processed_events_expires ON identity.processed_events(expires_at);
```

### 5.2 Configuration Models

#### 5.2.1 Dynamic.xyz API Configuration
```json
{
  "DynamicApi": {
    "BaseUrl": "https://app.dynamic.xyz/api/v0",
    "ApiToken": "dyn_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
    "EnvironmentId": "95b11417-f18f-457f-8804-68e361f9164f",
    "TimeoutSeconds": 30,
    "RetryPolicy": {
      "MaxRetries": 3,
      "BackoffMultiplier": 2.0,
      "BaseDelay": "00:00:01"
    },
    "RateLimit": {
      "RequestsPerMinute": 500, // Conservative default, adjust based on plan
      "BurstSize": 50,
      "Adaptive": true // Scale based on observed API limits
    }
  },
  "DynamicJwt": {
    "JwksUri": "https://app.dynamic.xyz/api/v0/sdk/{Dynamic.EnvironmentId}/.well-known/jwks",
    "ValidIssuer": "https://app.dynamic.xyz/api/v0/sdk/{Dynamic.EnvironmentId}",
    "ValidateAudience": false,
    "ClockSkewMinutes": 5,
    "CacheDurationMinutes": 10
  },
  "DynamicWebhooks": {
    "Secret": "webhook-secret-key",
    "SignatureHeader": "X-Dynamic-Signature",
    "SignatureEncoding": "base64", // "hex" or "base64" 
    "ProcessingTimeoutSeconds": 5, // Acknowledgment timeout
    "DeduplicationTtlHours": 168 // 7 days
  }
}
```

### 5.3 Domain Events

#### 5.3.1 User Events
```csharp
public sealed record UserSynchronizedDomainEvent(
    UserId UserId,
    DynamicUserId DynamicUserId,
    DateTime SyncedAt) : IDomainEvent;

public sealed record UserAuthenticatedDomainEvent(
    UserId UserId,
    IReadOnlyList<string> Scopes,
    DateTime AuthenticatedAt) : IDomainEvent;

public sealed record WalletConnectedDomainEvent(
    UserId UserId,
    WalletId WalletId,
    string Address,
    BlockchainType Chain) : IDomainEvent;
```

---

## 6. Integration Points

### 6.1 Dynamic.xyz Management API Integration

#### 6.1.1 API Endpoints Used (Complete Coverage)
```csharp
public interface IDynamicApiClient
{
    // User Management - GET /users/{userId}
    Task<Result<DynamicUserResponse>> GetUserByIdAsync(
        DynamicUserId userId, 
        CancellationToken cancellationToken = default);
        
    // User Management - PUT /users/{userId}
    Task<Result<DynamicUserResponse>> UpdateUserAsync(
        DynamicUserId userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);
        
    // User Management - DELETE /users/{userId}
    Task<Result> DeleteUserAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default);
    
    // Wallet Management - GET /users/{userId}/wallets
    Task<Result<DynamicWalletsResponse>> GetUserWalletsAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default);
        
    // Wallet Management - POST /users/{userId}/wallets
    Task<Result<DynamicWalletResponse>> CreateWalletAsync(
        DynamicUserId userId,
        CreateWalletRequest request,
        CancellationToken cancellationToken = default);
        
    // Wallet Management - DELETE /users/{userId}/wallets/{walletId}
    Task<Result> UnlinkWalletAsync(
        DynamicUserId userId,
        DynamicWalletId walletId,
        CancellationToken cancellationToken = default);
        
    // Environment Configuration - GET /environments/{environmentId}
    Task<Result<DynamicEnvironmentResponse>> GetEnvironmentAsync(
        DynamicEnvironmentId environmentId,
        CancellationToken cancellationToken = default);
        
    // Visit Tracking - POST /visits
    Task<Result> RecordVisitAsync(
        RecordVisitRequest request,
        CancellationToken cancellationToken = default);
        
    // Session Management - GET /sessions/{sessionId}
    Task<Result<DynamicSessionResponse>> GetSessionAsync(
        DynamicSessionId sessionId,
        CancellationToken cancellationToken = default);
        
    // Session Management - DELETE /sessions/{sessionId}
    Task<Result> RevokeSessionAsync(
        DynamicSessionId sessionId,
        CancellationToken cancellationToken = default);
        
    // Batch Operations - POST /users/batch
    Task<Result<BatchUsersResponse>> GetUsersBatchAsync(
        BatchUsersRequest request,
        CancellationToken cancellationToken = default);
}
```

#### 6.1.2 Error Handling Strategy
```csharp
public static class DynamicApiErrorHandler
{
    public static Error MapHttpError(HttpStatusCode statusCode, string content) => statusCode switch
    {
        HttpStatusCode.Unauthorized => Error.Authentication("Invalid Dynamic.xyz API token"),
        HttpStatusCode.Forbidden => Error.Authorization("Access denied to Dynamic.xyz resource"),
        HttpStatusCode.NotFound => Error.NotFound("Dynamic.xyz resource not found"),
        HttpStatusCode.TooManyRequests => Error.External("Dynamic.xyz rate limit exceeded"),
        HttpStatusCode.BadRequest => Error.Validation($"Dynamic.xyz API error: {content}"),
        _ => Error.External($"Dynamic.xyz API error ({(int)statusCode})", content)
    };
}
```

### 6.2 JWKS Integration

#### 6.2.1 Key Management Service
```csharp
public interface IJwksService
{
    Task<Result<IList<SecurityKey>>> GetSigningKeysAsync(
        CancellationToken cancellationToken = default);
    
    void InvalidateCache();
}

public sealed class DynamicJwksService : IJwksService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _jwksUri;
    
    public async Task<Result<IList<SecurityKey>>> GetSigningKeysAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "dynamic_jwks_keys";
        
        if (_cache.TryGetValue(cacheKey, out IList<SecurityKey>? cached))
            return Result<IList<SecurityKey>>.Success(cached!);
            
        // Fetch from JWKS endpoint
        // Cache for 10 minutes
        // Return signing keys
    }
}
```

### 6.3 Webhook Integration

#### 6.3.1 Webhook Processing Pipeline
```csharp
public sealed class DynamicWebhookProcessor
{
    public async Task<Result> ProcessWebhookAsync(
        DynamicWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        // Validate webhook signature
        var validationResult = await ValidateSignatureAsync(webhookEvent);
        if (validationResult.IsFailure)
            return validationResult;
            
        // Process based on event type (Updated with correct event names)
        return webhookEvent.EventName switch
        {
            "users.created" => await ProcessUserCreatedAsync(webhookEvent.Data, cancellationToken),
            "users.updated" => await ProcessUserUpdatedAsync(webhookEvent.Data, cancellationToken),
            "users.deleted" => await ProcessUserDeletedAsync(webhookEvent.Data, cancellationToken),
            "wallets.linked" => await ProcessWalletLinkedAsync(webhookEvent.Data, cancellationToken),
            "wallets.unlinked" => await ProcessWalletUnlinkedAsync(webhookEvent.Data, cancellationToken),
            "wallets.updated" => await ProcessWalletUpdatedAsync(webhookEvent.Data, cancellationToken),
            "sessions.created" => await ProcessSessionCreatedAsync(webhookEvent.Data, cancellationToken),
            "sessions.deleted" => await ProcessSessionDeletedAsync(webhookEvent.Data, cancellationToken),
            "environments.updated" => await ProcessEnvironmentUpdatedAsync(webhookEvent.Data, cancellationToken),
            _ => Result.Success() // Ignore unknown event types
        };
    }
}
```

### 6.4 Rate Limiting & Resilience Integration

#### 6.4.1 Dynamic.xyz API Rate Limiting
```csharp
public sealed class DynamicApiRateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly Queue<DateTime> _requestTimestamps;
    private readonly int _requestsPerMinute;
    
    public async Task<Result<T>> ExecuteWithRateLimitAsync<T>(
        Func<Task<Result<T>>> apiCall,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Check rate limit
            if (!CanMakeRequest())
            {
                var waitTime = CalculateWaitTime();
                await Task.Delay(waitTime, cancellationToken);
            }
            
            // Record request timestamp
            _requestTimestamps.Enqueue(DateTime.UtcNow);
            
            // Execute API call with retry policy
            return await ExecuteWithRetryAsync(apiCall, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

#### 6.4.2 Circuit Breaker Pattern
```csharp
public sealed class DynamicApiCircuitBreaker
{
    private readonly ICircuitBreakerPolicy _circuitBreaker;
    
    public DynamicApiCircuitBreaker()
    {
        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: OnCircuitBreakerOpen,
                onReset: OnCircuitBreakerClosed);
    }
    
    public async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(operation);
        }
        catch (CircuitBreakerOpenException)
        {
            return Result<T>.Failure(
                Error.External("Dynamic.xyz API temporarily unavailable"));
        }
    }
}
```

---

## 7. Security & Authentication Requirements

### 7.1 JWT Security Specifications

#### 7.1.1 Token Validation Requirements
```csharp
public sealed class JwtValidationOptions
{
    public required string ValidIssuer { get; init; } // Set to https://app.dynamic.xyz/api/v0/sdk/{EnvironmentId}
    public bool ValidateAudience { get; init; } = false; // Configurable based on Dynamic environment setup
    public bool ValidateLifetime { get; init; } = true;
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
    public bool ValidateIssuerSigningKey { get; init; } = true;
    public string SignatureAlgorithm { get; init; } = SecurityAlgorithms.RsaSha256;
    public string JwksUri { get; init; } // Set to https://app.dynamic.xyz/api/v0/sdk/{EnvironmentId}/.well-known/jwks
    public bool EnforceTypHeader { get; init; } = true; // Require typ = "JWT"
    public bool RejectNoneAlgorithm { get; init; } = true; // Reject alg = "none"
}
```

#### 7.1.2 Scope-Based Authorization
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireScopesAttribute : Attribute
{
    public string[] RequiredScopes { get; }
    public bool RequireAllScopes { get; }
    
    public RequireScopesAttribute(params string[] scopes)
    {
        RequiredScopes = scopes;
        RequireAllScopes = true;
    }
}

// Usage
[RequireScopes("read:profile")]
public class GetUserProfileQuery : IQuery<UserProfileResponse> { }

[RequireScopes("write:wallets", "requiresAdditionalAuth")]
public class LinkWalletCommand : ICommand<WalletResponse> { }
```

### 7.2 API Security

#### 7.2.1 Rate Limiting
```csharp
public sealed class DynamicApiRateLimitOptions
{
    public int RequestsPerMinute { get; init; } = 500; // Conservative default
    public int BurstSize { get; init; } = 50;
    public TimeSpan SlidingWindow { get; init; } = TimeSpan.FromMinutes(1);
    public bool AdaptiveRateLimiting { get; init; } = true; // Adjust based on API responses
    public RetryPolicy RetryPolicy { get; init; } = new();
}
```

#### 7.2.2 Request/Response Logging
```csharp
public sealed class DynamicApiLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Log request details (without sensitive headers)
        var response = await base.SendAsync(request, cancellationToken);
        // Log response status and timing
        return response;
    }
}
```

### 7.3 Webhook Security

#### 7.3.1 Signature Validation
```csharp
public sealed class WebhookSignatureValidator
{
    private readonly string _secret;
    private readonly WebhookSignatureOptions _options;
    
    public bool ValidateSignature(string payload, string signature)
    {
        var expectedSignature = ComputeSignature(payload, _secret);
        
        // Handle different encoding formats (configurable)
        var signatureBytes = _options.SignatureEncoding == "hex" 
            ? Convert.FromHexString(signature.Replace("sha256=", ""))
            : Convert.FromBase64String(signature.Replace("sha256=", ""));
            
        var expectedBytes = _options.SignatureEncoding == "hex"
            ? Convert.FromHexString(expectedSignature)
            : Convert.FromBase64String(expectedSignature);
        
        return CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes);
    }
    
    private string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return _options.SignatureEncoding == "hex" 
            ? Convert.ToHexString(hash).ToLower()
            : Convert.ToBase64String(hash);
    }
}
```

---

## 8. Performance Requirements

### 8.1 Performance Design Targets

**Design for Performance:**
- JWT Validation: <100ms (with JWKS cache, typically <50ms)
- Local User Query: <50ms (PostgreSQL with proper indexing)
- Dynamic.xyz API Call: Design for <500ms with graceful degradation
- Webhook Processing: <5s acknowledgment, async completion
- User Data Sync: <15s with exponential backoff on failures

**Monitoring and Alerting:**
- Track P95/P99 response times rather than SLA commitments
- Alert on degradation trends, not absolute thresholds
- Design for resilience with circuit breakers and retry policies

### 8.2 Caching Strategy

#### 8.2.1 JWKS Key Caching
```csharp
public sealed class JwksCacheOptions
{
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxCacheSize { get; init; } = 100;
}
```

#### 8.2.2 User Data Caching
```csharp
public sealed class UserDataCacheOptions
{
    public TimeSpan UserCacheDuration { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan WalletCacheDuration { get; init; } = TimeSpan.FromMinutes(30);
    public int MaxCachedUsers { get; init; } = 10000;
}
```

### 8.3 Database Performance

#### 8.3.1 Query Optimization
```sql
-- User lookup by Dynamic ID (most common)
EXPLAIN ANALYZE SELECT * FROM identity.users 
WHERE dynamic_user_id = $1;

-- Wallet lookup by address and chain
EXPLAIN ANALYZE SELECT * FROM identity.wallets 
WHERE address = $1 AND chain = $2;

-- Stale data identification for sync
EXPLAIN ANALYZE SELECT dynamic_user_id FROM identity.users 
WHERE last_synced_at < (NOW() - INTERVAL '6 hours')
ORDER BY last_synced_at 
LIMIT 100;
```

#### 8.3.2 Connection Pooling
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=axon;Username=app;Password=***;Pooling=true;MinPoolSize=10;MaxPoolSize=100;ConnectionIdleLifetime=300"
  }
}
```

---

## 9. Monitoring & Analytics

### 9.1 Application Metrics

#### 9.1.1 Key Performance Indicators
```csharp
public sealed class DynamicAuthMetrics
{
    private readonly IMetrics _metrics;
    
    public void RecordJwtValidation(bool success, TimeSpan duration)
    {
        _metrics.Measure.Counter.Increment("jwt_validations_total", 
            new MetricTags("success", success.ToString()));
        _metrics.Measure.Timer.Time("jwt_validation_duration", duration);
    }
    
    public void RecordApiCall(string endpoint, HttpStatusCode statusCode, TimeSpan duration)
    {
        _metrics.Measure.Counter.Increment("dynamic_api_calls_total",
            new MetricTags("endpoint", endpoint, "status", statusCode.ToString()));
        _metrics.Measure.Timer.Time("dynamic_api_duration", duration,
            new MetricTags("endpoint", endpoint));
    }
    
    public void RecordWebhookProcessed(string eventType, bool success, TimeSpan duration)
    {
        _metrics.Measure.Counter.Increment("webhooks_processed_total",
            new MetricTags("event_type", eventType, "success", success.ToString()));
        _metrics.Measure.Timer.Time("webhook_processing_duration", duration);
    }
}
```

#### 9.1.2 Health Checks
```csharp
public sealed class DynamicApiHealthCheck : IHealthCheck
{
    private readonly IDynamicApiClient _client;
    private readonly DynamicApiClientOptions _options;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetEnvironmentAsync(
                DynamicEnvironmentId.From(_options.EnvironmentId), 
                cancellationToken);
                
            return result.IsSuccess 
                ? HealthCheckResult.Healthy("Dynamic.xyz API is accessible")
                : HealthCheckResult.Degraded($"Dynamic.xyz API error: {result.Error.Message}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Dynamic.xyz API is not accessible", ex);
        }
    }
}
```

### 9.2 Logging Requirements

#### 9.2.1 Structured Logging
```csharp
public sealed class DynamicAuthLogger
{
    private readonly ILogger<DynamicAuthLogger> _logger;
    
    public void LogJwtValidation(string userId, bool success, string? error = null)
    {
        _logger.LogInformation("JWT validation {Success} for user {UserId} {Error}",
            success ? "succeeded" : "failed", userId, error);
    }
    
    public void LogUserSync(string dynamicUserId, TimeSpan duration)
    {
        _logger.LogInformation("User sync completed for {DynamicUserId} in {Duration}ms",
            dynamicUserId, duration.TotalMilliseconds);
    }
    
    public void LogWebhookReceived(string eventType, string signature)
    {
        _logger.LogInformation("Webhook received: {EventType} with signature {SignaturePrefix}...",
            eventType, signature[..8]);
    }
}
```

#### 9.2.2 Security Audit Logging
```csharp
public sealed class SecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;
    
    public void LogAuthenticationAttempt(string userId, string ipAddress, bool success)
    {
        _logger.LogWarning("Authentication {Result} for user {UserId} from IP {IpAddress}",
            success ? "SUCCESS" : "FAILURE", userId, ipAddress);
    }
    
    public void LogUnauthorizedAccess(string endpoint, string userId, string[] requiredScopes)
    {
        _logger.LogWarning("Unauthorized access attempt to {Endpoint} by user {UserId}. Required scopes: {RequiredScopes}",
            endpoint, userId, string.Join(", ", requiredScopes));
    }
}
```

### 9.3 Alerting Configuration

#### 9.3.1 Critical Alerts
```yaml
alerts:
  jwt_validation_failure_rate:
    condition: "rate(jwt_validations_failed[5m]) > 0.05"
    severity: "critical"
    message: "JWT validation failure rate exceeding 5%"
    
  dynamic_api_error_rate:
    condition: "rate(dynamic_api_errors[5m]) > 0.1"
    severity: "warning" 
    message: "Dynamic.xyz API error rate exceeding 10%"
    
  webhook_processing_lag:
    condition: "webhook_processing_duration_p95 > 30s"
    severity: "warning"
    message: "Webhook processing time exceeding 30 seconds"
    
  user_sync_failures:
    condition: "increase(user_sync_failures[1h]) > 10"
    severity: "warning"
    message: "User synchronization failures increasing"
```

---

## 10. Migration & Rollout Strategy

### 10.1 Implementation Phases

#### Phase 1: Core Infrastructure (Week 1-2)
- [ ] Identity module structure setup
- [ ] Dynamic.xyz API client implementation
- [ ] JWT validation service
- [ ] Database schema and migrations
- [ ] Basic health checks and logging

**Acceptance Criteria:**
- JWT tokens can be validated successfully
- User data can be fetched from Dynamic.xyz API
- Database entities can be created and queried
- Health checks report system status

#### Phase 2: Authentication Pipeline (Week 3-4)
- [ ] Authentication behavior implementation
- [ ] Current user service integration
- [ ] Scope-based authorization
- [ ] FastEndpoints authentication integration
- [ ] Error handling and security logging

**Acceptance Criteria:**
- API endpoints can authenticate users via JWT
- User context is available throughout request pipeline
- Unauthorized requests are properly rejected
- Security events are logged for audit

#### Phase 3: Data Synchronization (Week 5-6)
- [ ] User data mirroring implementation
- [ ] Webhook processing system
- [ ] Background sync services
- [ ] Data consistency checks
- [ ] Performance optimization

**Acceptance Criteria:**
- User data is automatically synced from Dynamic.xyz
- Webhooks are processed within 30 seconds
- Stale data is detected and refreshed
- Local queries return data within 20ms

#### Phase 4: Production Readiness (Week 7-8)
- [ ] Comprehensive monitoring and alerting
- [ ] Performance testing and optimization
- [ ] Security testing and penetration testing
- [ ] Documentation and runbooks
- [ ] Disaster recovery procedures

**Acceptance Criteria:**
- System meets all performance requirements
- Monitoring captures all key metrics
- Security vulnerabilities are addressed
- Operations team can support the system

### 10.2 Rollout Plan

#### 10.2.1 Environment Rollout
1. **Development Environment**
   - Deploy and test all functionality
   - Validate Dynamic.xyz integration
   - Performance baseline establishment

2. **Staging Environment**
   - Full integration testing
   - Security penetration testing
   - Load testing with realistic data volumes
   - Monitoring and alerting validation

3. **Production Environment**
   - Blue-green deployment strategy
   - Gradual traffic migration
   - Real-time monitoring
   - Immediate rollback capability

#### 10.2.2 Feature Flags
```csharp
public sealed class DynamicAuthFeatureFlags
{
    public bool EnableJwtValidation { get; init; } = false;
    public bool EnableUserSync { get; init; } = false;
    public bool EnableWebhookProcessing { get; init; } = false;
    public bool EnablePerformanceOptimizations { get; init; } = false;
    public double JwtValidationSampleRate { get; init; } = 1.0;
}
```

### 10.3 Rollback Plan

#### 10.3.1 Immediate Rollback Triggers
- JWT validation failure rate > 10%
- Dynamic.xyz API unavailable for > 5 minutes
- Database performance degradation > 50%
- Memory usage > 80% for > 10 minutes
- Any security incident detected

#### 10.3.2 Rollback Procedure
1. **Disable Authentication** - Revert to `AllowAnonymous()` mode
2. **Database Rollback** - Revert schema changes if needed
3. **Configuration Reset** - Reset to previous configuration
4. **Service Restart** - Restart application services
5. **Verification** - Confirm system stability
6. **Incident Response** - Document and analyze failure

### 10.4 Success Criteria

#### 10.4.1 Technical Success Metrics
- [ ] JWT validation successful with proper error handling
- [ ] Local query performance optimized with indexing
- [ ] Dynamic.xyz API integration resilient with circuit breakers
- [ ] Webhook processing reliable with retry mechanisms
- [ ] Security vulnerabilities addressed through regular audits
- [ ] System stability maintained through monitoring and alerting

#### 10.4.2 Business Success Metrics
- [ ] User authentication experience seamless
- [ ] Zero data loss during migration
- [ ] Support ticket volume < 5/week related to authentication
- [ ] Developer productivity maintained during rollout
- [ ] Operational overhead < 2 hours/week

---

## 11. Appendices

### Appendix A: Dynamic.xyz API Rate Limits
- **User Management API**: 1000 requests/hour per API token
- **Wallet Operations**: 500 requests/hour per user
- **JWKS Endpoint**: 60 requests/minute (cacheable)
- **Webhook Delivery**: At-least-once semantics with retries

### Appendix B: Blockchain Support Matrix
| Chain | Dynamic Support | Axon Priority | Implementation Status |
|-------|----------------|---------------|----------------------|
| Solana (SOL) | ✅ Full | High | Phase 1 |
| Ethereum (ETH) | ✅ Full | Medium | Phase 2 |
| Polygon (MATIC) | ✅ Full | Medium | Phase 2 |
| Bitcoin (BTC) | ✅ Full | Low | Phase 3 |
| Algorand (ALGO) | ✅ Full | Low | Phase 3 |

### Appendix C: Error Code Reference
| Code | Category | Description |
|------|----------|-------------|
| AUTH001 | JWT | Invalid token signature |
| AUTH002 | JWT | Token expired |
| AUTH003 | JWT | Invalid issuer |
| AUTH004 | Scope | Insufficient permissions |
| AUTH005 | MFA | Additional authentication required |
| SYNC001 | Data | User sync failed |
| SYNC002 | Data | Webhook processing failed |
| API001 | External | Dynamic.xyz API error |
| API002 | External | Rate limit exceeded |

---

## Appendix A: Future Epics (Out of MVP)

### A.1 Enterprise Authentication Features

#### A.1.1 External Provider Integration
- BYOA ("Bring Your Own Authentication") for Enterprise customers
- JWKS validation for corporate SSO providers  
- External JWT to Dynamic.xyz token exchange
- Configurable claim mappings (email, roles, permissions)
- Support for SAML 2.0 and OpenID Connect providers

#### A.1.2 Multi-Factor Authentication
- TOTP (Google Authenticator, Authy) integration
- WebAuthn/Passkey support for hardware keys
- SMS-based verification (region-specific)
- Email-based verification codes
- Conditional MFA based on risk assessment
- Grace periods for new user onboarding

#### A.1.3 Session Management
- Multi-device session tracking
- Remote session termination
- Session analytics and security monitoring
- Suspicious activity detection and alerts

### A.2 Compliance & Data Management

#### A.2.1 GDPR/CCPA Support
- Right to access user data (Article 15)
- Right to rectification (Article 16)
- Right to erasure/deletion (Article 17)
- Data portability in machine-readable format (Article 20)
- Consent management and tracking
- Automated data retention policies
- Audit trail for all data processing activities

#### A.2.2 Data Residency & Sovereignty
- Geographic data storage options
- Regional compliance requirements
- Cross-border data transfer controls
- Local encryption key management

### A.3 Analytics & Monitoring

#### A.3.1 Authentication Analytics
- User authentication patterns and behavior
- Wallet usage statistics and trends
- Security event correlation and alerting
- Performance metrics and SLA tracking

#### A.3.2 Business Intelligence
- User segmentation by wallet types and chains
- Geographic distribution of users
- Provider popularity and performance metrics
- Cost analysis and optimization recommendations

### A.4 Advanced Security Features

#### A.4.1 Fraud Detection
- Machine learning-based anomaly detection
- Behavioral biometrics integration
- Device fingerprinting and reputation scoring
- Real-time risk assessment and adaptive authentication

#### A.4.2 Advanced Audit & Compliance
- Immutable audit logs with blockchain anchoring
- Automated compliance reporting
- Third-party security integrations (SIEM, SOAR)
- Advanced threat intelligence feeds

---

## Appendix B: MVP Test Plan Hooks

### B.1 JWT Validation Tests
- **Happy Path:** Valid RS256 with correct `iss` → 200 on `/exchange`, user+wallets mirrored, `/me` returns `completed`
- **JWKS Rotation:** New `kid` appears → first call refetches JWKS → success; then cache hit
- **Wrong Issuer/Env:** 401 with `code="AUTH003"` and Problem JSON format
- **Algorithm Validation:** Reject `alg="none"` and non-RS256 algorithms
- **Header Validation:** Require `typ="JWT"` header

### B.2 Concurrency & Idempotency Tests  
- **Race Condition:** Two concurrent `/exchange` for same user → single upsert, no 409
- **Idempotency:** Same `Idempotency-Key` returns identical results
- **Provider Errors:** Dynamic.xyz 429/5xx → returns `502` + `Retry-After`, logs backoff

### B.3 Webhook Processing Tests
- **Deduplication:** Send same `eventId` twice → first processed, second 200 no-op
- **Signature Validation:** Test both hex and base64 encodings based on config
- **Event Processing:** `users.created`, `wallets.linked` events processed within 5s acknowledgment
- **Exactly-Once:** Verify database upserts are atomic and handle unique violations

### B.4 Database Constraint Tests
- **Address Uniqueness:** Same `(address, chain)` cannot be inserted twice
- **Case Sensitivity:** Verify CITEXT email handling prevents duplicates
- **TTL Cleanup:** Processed events expire after configured TTL period

---

## Appendix C: Deployment Strategy & Risk Mitigation

### C.1 Shadow Mode Implementation

**Purpose:** Validate Dynamic.xyz integration accuracy without impacting production users.

**Mechanism:**
- Feature flag: `DYNAMIC_SHADOW_MODE_ENABLED=true`
- All `/auth/exchange` calls process JWT + mirror data but **do not enforce authentication**
- Log validation results for comparison: `shadow.validation.success=true/false`
- Metrics collection on shadow vs production auth decision rates

**Shadow Mode Flow:**
```
1. Receive JWT in Authorization header
2. Validate JWT against Dynamic.xyz JWKS (shadow validation)
3. Mirror user/wallet data to local database
4. Log shadow auth result: success/failure + reason
5. Continue with existing auth mechanism (bypass shadow result)
6. Return 200 regardless of shadow validation outcome
```

**Shadow Metrics Collection:**
- Shadow validation success rate
- JWT parsing/validation errors
- JWKS fetch latencies and cache hit rates
- Data sync completion rates
- Shadow vs production auth decision comparison

### C.2 Blue/Green Deployment Strategy

**Blue Environment:** Current production authentication system
**Green Environment:** New Dynamic.xyz authentication system

**Phase 1: Shadow Mode (Week 1-2)**
- Deploy green environment with shadow mode enabled
- 100% traffic continues through blue (existing auth)
- Green validates all requests but doesn't block
- Compare shadow results vs blue auth decisions

**Phase 2: Gradual Traffic Migration (Week 3-4)**
```
Week 3: 5% traffic → green, 95% → blue
Week 4: 25% traffic → green, 75% → blue
```

**Phase 3: Full Migration (Week 5-6)**
```
Week 5: 75% traffic → green, 25% → blue
Week 6: 100% traffic → green (blue becomes backup)
```

**Rollback Triggers:**
- Authentication success rate drops below 99.5%
- API latency p95 exceeds 200ms
- Error rate exceeds 0.1%
- Critical security issues detected

**Rollback Process:**
1. **Immediate:** Feature flag flip routes 100% traffic to blue
2. **Investigation:** Analyze green environment logs and metrics
3. **Fix Forward:** Address issues and re-attempt migration
4. **Timeline:** Rollback executable within 5 minutes

### C.3 Feature Flag Configuration

**Environment Variables:**
```bash
# Shadow Mode Control
DYNAMIC_SHADOW_MODE_ENABLED=true|false

# Traffic Routing Percentage (0-100)
DYNAMIC_TRAFFIC_PERCENTAGE=5

# Circuit Breaker Settings
DYNAMIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD=10
DYNAMIC_CIRCUIT_BREAKER_TIMEOUT_MS=5000

# Monitoring & Alerting
DYNAMIC_METRICS_ENABLED=true
DYNAMIC_ALERT_THRESHOLDS_ERROR_RATE=0.001
```

**Real-time Controls:**
- Feature flags adjustable without deployment
- Circuit breakers automatically disable Dynamic.xyz on consecutive failures
- Metrics dashboard with real-time rollback capability

### C.4 Monitoring & Alerting

**Critical Metrics:**
```yaml
authentication_success_rate:
  threshold: 99.5%
  action: alert_oncall_immediately

jwt_validation_latency:
  p95_threshold: 50ms
  action: alert_team

jwks_fetch_errors:
  threshold: 3_consecutive_failures
  action: circuit_breaker_open + alert

user_sync_lag:
  threshold: 500ms_p95
  action: alert_data_team
```

**Alerting Channels:**
- **Critical:** PagerDuty → On-call Engineer
- **High:** Slack #auth-alerts + Email
- **Medium:** Slack #engineering-alerts

**Dashboard Requirements:**
- Real-time authentication success rates (blue vs green)
- JWT validation latency distributions
- JWKS cache hit rates and refresh patterns
- User/wallet sync completion rates
- Error breakdown by type (AUTH001-005, SYNC001-002, API001-002)

---

**Document Approval:**
- [ ] Product Owner Review
- [ ] Engineering Lead Review  
- [ ] Security Team Review
- [ ] DevOps Team Review

**Next Steps:**
1. Review and approve PRD
2. Create detailed FRD (Functional Requirements Document)
3. Begin Phase 1 implementation with shadow mode
4. Set up monitoring and alerting infrastructure