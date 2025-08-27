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
- Enable wallet-first authentication supporting 30+ wallet providers
- Provide sub-100ms user/wallet data access through local mirroring
- Ensure enterprise-grade security with RS256 JWT validation

**Success Metrics:**
- JWT validation latency: <50ms (95th percentile)
- Local data query performance: <20ms
- Webhook processing time: <30 seconds
- API integration success rate: >99.5%

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
- [ ] Extract user ID, scopes, and environment ID from JWT claims
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
- [ ] Process user.created, user.updated, user.deleted events
- [ ] Process wallet.linked, wallet.unlinked events
- [ ] Acknowledge webhooks within 30 seconds
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
- [ ] Cache user lookups for performance
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
- [ ] Cache scope validations for performance

#### Story 7: MFA Detection and Handling
**As a** sensitive operation endpoint  
**I want to** detect when MFA is required  
**So that** I can enforce additional security measures

**Acceptance Criteria:**
- [ ] Identify "requiresAdditionalAuth" in JWT scopes
- [ ] Return appropriate MFA challenge response
- [ ] Block sensitive operations until MFA is satisfied
- [ ] Log MFA enforcement events
- [ ] Support different MFA method requirements
- [ ] Handle MFA bypass for admin operations

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
│   │   └── IUserRepository.cs         # Repository contract
│   ├── Wallets/
│   │   ├── Wallet.cs                  # Wallet entity
│   │   ├── WalletId.cs               # Strong ID
│   │   ├── DynamicWalletId.cs        # Dynamic wallet ID
│   │   └── BlockchainType.cs         # Enum
│   └── Authentication/
│       ├── JwtPayload.cs             # JWT claims
│       └── AuthenticationEvents.cs    # Domain events
├── Application/
│   ├── Authentication/
│   │   ├── ValidateJwtCommand.cs     # JWT validation
│   │   ├── GetCurrentUserQuery.cs    # User context
│   │   └── RefreshUserDataCommand.cs # Data sync
│   ├── Users/
│   │   ├── SyncUserCommand.cs        # User sync
│   │   └── GetUserQuery.cs           # User retrieval
│   └── Wallets/
│       ├── SyncWalletsCommand.cs     # Wallet sync
│       └── GetUserWalletsQuery.cs    # Wallet retrieval
└── Infrastructure/
    ├── Authentication/
    │   ├── JwtValidationService.cs    # JWT validation
    │   ├── DynamicAuthenticationService.cs # Auth service
    │   └── CurrentUserService.cs      # User context
    ├── External/
    │   ├── DynamicApiClient.cs        # API client
    │   ├── DynamicUserService.cs      # User API
    │   └── DynamicWalletService.cs    # Wallet API
    ├── Persistence/
    │   ├── IdentityDbContext.cs       # EF context
    │   ├── UserRepository.cs          # User repo
    │   └── WalletRepository.cs        # Wallet repo
    └── Webhooks/
        ├── DynamicWebhookProcessor.cs # Webhook handler
        └── WebhookSignatureValidator.cs # Security
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

**Response (Success):**
```json
{
  "userId": "01H8EXAMPLE",
  "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f",
  "email": "user@example.com",
  "scopes": ["read:profile", "write:wallets"],
  "wallets": [
    {
      "id": "01H8WALLETID",
      "dynamicWalletId": "wallet-uuid",
      "address": "So11111111111111111111111111111111111111112",
      "chain": "SOL",
      "provider": "phantom",
      "isPrimary": true
    }
  ],
  "expiresAt": "2025-08-27T18:30:00Z",
  "lastSynced": "2025-08-27T12:00:00Z"
}
```

**Response (Error):**
```json
{
  "error": "invalid_token",
  "message": "JWT signature validation failed",
  "code": "AUTH001"
}
```

#### 4.1.2 Current User Profile
```
GET /api/auth/me
```

**Purpose:** Get current authenticated user profile with wallet information

**Headers:**
```
Authorization: Bearer {dynamic-jwt-token}
```

**Response:**
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
      "address": "So11111111111111111111111111111111111111112",
      "chain": "SOL",
      "provider": "phantom",
      "walletName": "Main Wallet",
      "lastSelectedAt": "2025-08-27T11:30:00Z",
      "connectedAt": "2025-08-01T10:00:00Z",
      "properties": {
        "version": "V1",
        "isSessionKeyCompatible": true
      }
    }
  ],
  "permissions": {
    "scopes": ["read:profile", "write:wallets"],
    "requiresAdditionalAuth": false
  },
  "lastSynced": "2025-08-27T12:00:00Z"
}
```

### 4.2 Webhook Endpoints

#### 4.2.1 Dynamic.xyz Webhook Receiver
```
POST /api/webhooks/dynamic
```

**Purpose:** Process Dynamic.xyz lifecycle events

**Headers:**
```
X-Dynamic-Signature: sha256=...
Content-Type: application/json
```

**Request (User Created):**
```json
{
  "type": "user.created",
  "data": {
    "userId": "95b11417-f18f-457f-8804-68e361f9164f",
    "environmentId": "env-uuid",
    "email": "newuser@example.com",
    "createdAt": "2025-08-27T12:00:00Z"
  },
  "timestamp": "2025-08-27T12:00:01Z"
}
```

**Response:**
```json
{
  "received": true,
  "processedAt": "2025-08-27T12:00:02Z",
  "syncScheduled": true
}
```

### 4.3 Internal API Specifications

#### 4.3.1 User Synchronization
```
POST /internal/users/sync
```

**Purpose:** Trigger user data synchronization from Dynamic.xyz

**Request:**
```json
{
  "dynamicUserId": "95b11417-f18f-457f-8804-68e361f9164f",
  "forceRefresh": false
}
```

#### 4.3.2 Batch User Sync
```
POST /internal/users/batch-sync
```

**Purpose:** Perform bulk user data reconciliation

**Request:**
```json
{
  "userIds": ["user1", "user2", "user3"],
  "maxAge": "6h",
  "batchSize": 10
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
    email VARCHAR(255) NOT NULL,
    display_name VARCHAR(100),
    username VARCHAR(50),
    first_visit TIMESTAMPTZ,
    last_visit TIMESTAMPTZ,
    dynamic_created_at TIMESTAMPTZ NOT NULL,
    dynamic_updated_at TIMESTAMPTZ NOT NULL,
    last_synced_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_dynamic_user_id ON identity.users(dynamic_user_id);
CREATE INDEX idx_users_email ON identity.users(email);
CREATE INDEX idx_users_last_synced ON identity.users(last_synced_at);
```

#### 5.1.2 Wallets Table
```sql
CREATE TABLE identity.wallets (
    id VARCHAR(26) PRIMARY KEY,
    user_id VARCHAR(26) NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    dynamic_wallet_id UUID UNIQUE NOT NULL,
    address VARCHAR(255) NOT NULL,
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
```

#### 5.1.3 Authentication Events Table
```sql
CREATE TABLE identity.authentication_events (
    id BIGSERIAL PRIMARY KEY,
    user_id VARCHAR(26) REFERENCES identity.users(id) ON DELETE SET NULL,
    dynamic_user_id UUID,
    event_type VARCHAR(50) NOT NULL,
    jwt_sub VARCHAR(255),
    scopes TEXT[],
    ip_address INET,
    user_agent TEXT,
    success BOOLEAN NOT NULL,
    error_code VARCHAR(20),
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_auth_events_user_id ON identity.authentication_events(user_id);
CREATE INDEX idx_auth_events_created_at ON identity.authentication_events(created_at);
CREATE INDEX idx_auth_events_success ON identity.authentication_events(success);
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
      "RequestsPerMinute": 1000,
      "BurstSize": 100
    }
  },
  "DynamicJwt": {
    "JwksUri": "https://app.dynamic.xyz/api/v0/sdk/{environmentId}/.well-known/jwks",
    "ValidIssuer": "https://app.dynamic.xyz/api/v0/sdk/{environmentId}",
    "ClockSkewMinutes": 5,
    "CacheDurationMinutes": 10
  },
  "DynamicWebhooks": {
    "Secret": "webhook-secret-key",
    "SignatureHeader": "X-Dynamic-Signature",
    "ProcessingTimeoutSeconds": 30
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

#### 6.1.1 API Endpoints Used
```csharp
public interface IDynamicApiClient
{
    // User Management
    Task<Result<DynamicUserResponse>> GetUserByIdAsync(
        DynamicUserId userId, 
        CancellationToken cancellationToken = default);
    
    // Wallet Management  
    Task<Result<DynamicWalletsResponse>> GetUserWalletsAsync(
        DynamicUserId userId,
        CancellationToken cancellationToken = default);
        
    // Environment Configuration
    Task<Result<DynamicEnvironmentResponse>> GetEnvironmentAsync(
        DynamicEnvironmentId environmentId,
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
            
        // Process based on event type
        return webhookEvent.Type switch
        {
            "user.created" => await ProcessUserCreatedAsync(webhookEvent.Data, cancellationToken),
            "user.updated" => await ProcessUserUpdatedAsync(webhookEvent.Data, cancellationToken),
            "user.deleted" => await ProcessUserDeletedAsync(webhookEvent.Data, cancellationToken),
            "wallet.linked" => await ProcessWalletLinkedAsync(webhookEvent.Data, cancellationToken),
            "wallet.unlinked" => await ProcessWalletUnlinkedAsync(webhookEvent.Data, cancellationToken),
            _ => Result.Success() // Ignore unknown event types
        };
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
    public required string ValidIssuer { get; init; }
    public bool ValidateAudience { get; init; } = false;
    public bool ValidateLifetime { get; init; } = true;
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
    public bool ValidateIssuerSigningKey { get; init; } = true;
    public string SignatureAlgorithm { get; init; } = SecurityAlgorithms.RsaSha256;
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
    public int RequestsPerMinute { get; init; } = 1000;
    public int BurstSize { get; init; } = 100;
    public TimeSpan SlidingWindow { get; init; } = TimeSpan.FromMinutes(1);
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
    
    public bool ValidateSignature(string payload, string signature)
    {
        var expectedSignature = ComputeSignature(payload, _secret);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(signature),
            Convert.FromBase64String(expectedSignature));
    }
    
    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
```

---

## 8. Performance Requirements

### 8.1 Response Time Requirements

| Operation | Target | Maximum | SLA |
|-----------|--------|---------|-----|
| JWT Validation | <50ms | 100ms | 99.5% |
| Local User Query | <20ms | 50ms | 99.9% |
| Dynamic.xyz API Call | <200ms | 1000ms | 99% |
| Webhook Processing | <30s | 60s | 99% |
| User Data Sync | <5s | 15s | 95% |

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
- [ ] JWT validation success rate > 99.5%
- [ ] Local query performance < 20ms (95th percentile)
- [ ] Dynamic.xyz API integration success rate > 99%
- [ ] Webhook processing time < 30 seconds (95th percentile)
- [ ] Zero security vulnerabilities in production
- [ ] System uptime > 99.9%

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

**Document Approval:**
- [ ] Product Owner Review
- [ ] Engineering Lead Review  
- [ ] Security Team Review
- [ ] DevOps Team Review

**Next Steps:**
1. Review and approve PRD
2. Create detailed FRD (Functional Requirements Document)
3. Begin Phase 1 implementation
4. Set up monitoring and alerting infrastructure