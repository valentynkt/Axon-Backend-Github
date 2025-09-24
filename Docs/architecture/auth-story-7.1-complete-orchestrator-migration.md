# Authentication Architecture - Story 7.1: Complete Orchestrator Migration

## Overview

This document describes the refactored authentication architecture following Story 7.1, which enforces strict separation of concerns through the Authentication Orchestrator pattern. All authentication flows now follow a consistent, layered approach where business logic is properly encapsulated and JWT token generation is centralized.

## High-Level Architecture

```
┌─────────────┐     ┌──────────┐     ┌─────────┐     ┌──────────────┐     ┌──────────┐     ┌──────────┐
│ API Endpoint│────▶│ Command  │────▶│ Handler │────▶│ Orchestrator │────▶│ Provider │────▶│ Services │
└─────────────┘     └──────────┘     └─────────┘     └──────────────┘     └──────────┘     └──────────┘
      │                   │                │                 │                   │                │
      │                   │                │                 │                   │                │
   Extract            Validate         Delegate          Coordinate          Business         Domain
   Token              Request         to Orch.           & Generate         Logic           Operations
                                                          JWT Token

```

## Core Principles

1. **API Endpoints have NO business logic** - Only extract data from HTTP context
2. **Commands are simple DTOs** - Carry minimal data between layers
3. **Handlers are thin wrappers** - Delegate everything to orchestrator
4. **Orchestrator coordinates** - Manages provider selection and JWT generation
5. **Providers encapsulate logic** - All provider-specific business logic
6. **Services handle domain operations** - Reusable domain logic

## Exchange Endpoint Flow (Dynamic.xyz Integration)

### 1. API Endpoint Layer
**File**: `src/Api/Endpoints/V1/Auth/Commands/ExchangeEndpoint.cs`

**Responsibilities**:
- Extract Bearer token from Authorization header
- Create command with bearer token
- Return HTTP response

**What it does NOT do**:
- Validate JWT tokens (bypassed in middleware)
- Make any service calls
- Process any business logic

**Important**: The exchange endpoint is excluded from ASP.NET Core authentication middleware to prevent double token validation. JWT validation is performed entirely by the DynamicAuthenticationProvider.

```csharp
protected override Task<Result<ExchangeCredentialCommand, Error>> ExecuteCommand(
    ExchangeTokenRequestDto _,
    CancellationToken ct)
{
    var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        return Task.FromResult(Result.Failure<ExchangeCredentialCommand, Error>(
            Error.Unauthorized("Missing or invalid Authorization header")));
    }

    var bearerToken = authHeader["Bearer ".Length..].Trim();
    var command = new ExchangeCredentialCommand(bearerToken);
    return Task.FromResult(Result.Success<ExchangeCredentialCommand, Error>(command));
}
```

### 2. Command Layer
**File**: `src/Modules/Identity/Application/Commands/ExchangeCredential/ExchangeCredentialCommand.cs`

**Structure**:
```csharp
public sealed record ExchangeCredentialCommand(string BearerToken)
    : IdentityBaseCommand<ExchangeOutcome>;
```

**Validation**: `ExchangeCredentialCommandValidator.cs`
- Validates token format (3 parts separated by dots)
- Basic JWT structure validation only

### 3. Handler Layer
**File**: `src/Modules/Identity/Application/Commands/ExchangeCredential/ExchangeCredentialHandler.cs`

**Responsibilities**:
- Receive command
- Call orchestrator's `ExchangeDynamicTokenAsync`
- Map orchestrator response to `ExchangeOutcome`

```csharp
public override async Task<Result<ExchangeOutcome, Error>> Handle(
    ExchangeCredentialCommand command,
    CancellationToken cancellationToken)
{
    // Delegate EVERYTHING to orchestrator
    var exchangeResult = await _orchestrator.ExchangeDynamicTokenAsync(
        command.BearerToken,
        cancellationToken);

    if (exchangeResult.IsFailure)
        return Result.Failure<ExchangeOutcome, Error>(exchangeResult.Error);

    // Map response to outcome
    var response = exchangeResult.Value;
    var outcome = new ExchangeOutcome(
        AccessToken: response.AccessToken,
        TokenType: "Bearer",
        ExpiresIn: (int)(response.ExpiresAt - DateTime.UtcNow).TotalSeconds,
        AxonUserId: new AxonUserId(response.UserId),
        Created: response.AdditionalData["created"],
        WalletsProcessed: response.AdditionalData["wallets_processed"],
        // ... other metrics
    );

    return Result.Success<ExchangeOutcome, Error>(outcome);
}
```

### 4. Orchestrator Layer
**File**: `src/Modules/Identity/Application/Services/AuthenticationOrchestrator.cs`

**Responsibilities**:
- Find appropriate provider ("dynamic")
- Create provider-specific request
- Call provider's `AuthenticateAsync`
- Generate JWT token via `IJwtTokenService`
- Return unified `AuthenticationResponse`

```csharp
public async Task<Result<AuthenticationResponse, Error>> ExchangeDynamicTokenAsync(
    string dynamicToken,
    CancellationToken cancellationToken)
{
    // Find Dynamic provider
    var provider = GetProvider("dynamic");

    // Create exchange request
    var exchangeRequest = new DynamicExchangeRequest(dynamicToken);

    // Process through provider - handles ALL business logic
    var providerResult = await provider.AuthenticateAsync(exchangeRequest, cancellationToken);

    // Generate Axon JWT token
    var tokenResult = await _tokenService.GenerateAccessTokenAsync(
        authData.User.AxonPrincipalId,
        ProviderType.From(authData.ProviderType),
        authData.User.OriginalSubject,
        authData.User.OriginalIssuer,
        30, // 30 minutes
        cancellationToken);

    // Return complete response with token and metrics
    return new AuthenticationResponse(
        AccessToken: tokenResult.Value.AccessToken,
        UserId: authData.User.Id,
        ProviderType: "dynamic",
        ExpiresAt: DateTime.UtcNow.AddMinutes(30),
        AdditionalData: authData.AdditionalClaims);
}
```

### 5. Provider Layer
**File**: `src/Modules/Identity/Application/Providers/DynamicAuthenticationProvider.cs`

**Responsibilities** (ALL Dynamic-specific business logic):
- Validate Dynamic JWT token
- Extract user data from token
- Normalize wallet addresses
- Resolve or create AxonPrincipal
- Process wallet ownership batch
- Apply chain defaults
- Create/update Identity user
- Persist changes
- Warm caches
- Return metrics

**Key Methods**:
```csharp
public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
    AuthenticationRequest request,
    CancellationToken cancellationToken)
{
    // Step 1: Validate Dynamic token
    var validationResult = await _dynamicAuthService.ValidateTokenAsync(
        dynamicRequest.Token, cancellationToken);

    // Step 2: Normalize wallets
    var exchangeWallets = NormalizeWallets(dynamicUserData.Wallets);

    // Step 3: Process exchange (principal resolution, wallet linking, defaults)
    var exchangeResult = await ProcessExchange(userData, cancellationToken);

    // Step 4: Get or create Identity user
    var identityUser = await GetOrCreateIdentityUserAsync(principal, dynamicUserData);

    // Step 5: Persist changes
    await _principalRepo.UnitOfWork.SaveChangesAsync(cancellationToken);

    // Step 6: Warm caches
    await WarmUserContextCaches(dynamicUserData.AxonUserId, principal.Id);

    // Return complete authentication data with metrics
    return new AuthenticationData(
        User: identityUser,
        ProviderType: "dynamic",
        AdditionalClaims: metrics);
}
```

## Service Responsibilities

### Core Authentication Services

#### `IAuthenticationOrchestrator`
- **Purpose**: Single entry point for all authentication flows
- **Location**: `Application/Services/AuthenticationOrchestrator.cs`
- **Responsibilities**:
  - Provider selection and routing
  - JWT token generation coordination
  - Response standardization
  - Error handling and logging

#### `IJwtTokenService`
- **Purpose**: Generate and manage Axon JWT tokens
- **Location**: `Infrastructure/Services/JwtTokenService.cs`
- **Responsibilities**:
  - JWT token creation with claims
  - Token signing (Azure Key Vault or local)
  - Token expiration management
  - Refresh token handling

#### `IChallengeService`
- **Purpose**: Generate and validate authentication challenges
- **Location**: `Infrastructure/Services/ChallengeService.cs`
- **Responsibilities**:
  - Nonce generation for wallet signing
  - Challenge validation
  - Replay protection
  - SIWS message formatting

### Domain Services

#### `IPrincipalResolutionService`
- **Purpose**: Resolve identity conflicts across authentication methods
- **Location**: `Application/Services/PrincipalResolutionService.cs`
- **Responsibilities**:
  - Find existing principals by credential/wallet
  - Handle cross-network identity conflicts
  - Create new principals when appropriate
  - Maintain identity uniqueness

#### `IWalletVerificationService`
- **Purpose**: Verify wallet ownership claims
- **Location**: `Application/Services/WalletVerificationService.cs`
- **Responsibilities**:
  - Validate wallet ownership
  - Check for conflicts with other principals
  - Set verification status and access modes
  - Handle attestation sources

#### `IAddressNormalizationService`
- **Purpose**: Normalize blockchain addresses
- **Location**: `Application/Services/AddressNormalizationService.cs`
- **Responsibilities**:
  - Chain-specific address normalization
  - Checksum validation
  - Format standardization
  - Cross-chain compatibility

### External Services

#### `IDynamicAuthService`
- **Purpose**: Integration with Dynamic.xyz
- **Location**: `Infrastructure/ExternalServices/DynamicAuthService.cs`
- **Responsibilities**:
  - JWT token validation against Dynamic JWKS
  - User data extraction from tokens
  - JWKS caching and rotation
  - Dynamic-specific claim normalization
- **Note**: Should ONLY be used by `DynamicAuthenticationProvider`

## Middleware Configuration

**File**: `src/Api/Program.cs`

The exchange endpoint is explicitly excluded from JWT authentication middleware to prevent double validation:

```csharp
// Authentication and authorization (skip JWT validation for exchange endpoint)
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/v1/auth/exchange"),
    appBuilder =>
    {
        appBuilder.UseAuthentication();
        appBuilder.UseAuthorization();
    });
```

This ensures:
- Single token validation path (in DynamicAuthenticationProvider only)
- No redundant JWT parsing and JWKS validation
- Clear separation of authentication concerns

## Data Flow Example: Dynamic Token Exchange

```
1. Client Request:
   POST /api/v1/auth/exchange
   Authorization: Bearer <dynamic_jwt_token>

2. Middleware:
   - Authentication/Authorization BYPASSED for this route
   - Request proceeds directly to endpoint

3. ExchangeEndpoint:
   - Extracts bearer token from header
   - Creates ExchangeCredentialCommand(bearerToken)

4. ExchangeCredentialHandler:
   - Calls orchestrator.ExchangeDynamicTokenAsync(bearerToken)

5. AuthenticationOrchestrator:
   - Finds DynamicAuthenticationProvider
   - Creates DynamicExchangeRequest(bearerToken)
   - Calls provider.AuthenticateAsync(request)
   - Receives AuthenticationData with metrics
   - Generates Axon JWT via IJwtTokenService
   - Returns AuthenticationResponse

6. DynamicAuthenticationProvider:
   - Validates token via IDynamicAuthService
   - Extracts user data (id, email, wallets)
   - Resolves/creates principal via IPrincipalResolutionService
   - Links wallets via IWalletVerificationService
   - Applies chain defaults
   - Creates/updates Identity user
   - Saves to database
   - Warms caches
   - Returns AuthenticationData with all metrics

7. Response to Client:
   {
     "access_token": "axon_jwt_token",
     "token_type": "Bearer",
     "expires_in": 1800,
     "axon_user_id": "01234567-89ab-cdef",
     "created": false,
     "wallets_processed": 3,
     "wallets_linked": 2,
     "defaults_applied": 2,
     "skipped": 1,
     "conflicts": 0
   }
```

## Key Architecture Decisions

### 1. No Business Logic in API Endpoints
**Rationale**: Endpoints should only handle HTTP concerns (headers, status codes, response formatting)
**Benefit**: Business logic can be tested independently of HTTP layer

### 2. Orchestrator as Single Entry Point
**Rationale**: Centralized coordination ensures consistent token generation and error handling
**Benefit**: All auth flows follow same pattern, easier to maintain and audit

### 3. Provider Pattern for Authentication Methods
**Rationale**: Each auth method (Dynamic, Wallet, OAuth) has unique requirements
**Benefit**: New providers can be added without modifying orchestrator

### 4. Domain Services for Reusable Logic
**Rationale**: Principal resolution, wallet verification used across multiple providers
**Benefit**: Consistent business rules, avoiding duplication

### 5. Metrics in Additional Claims
**Rationale**: Operation metrics (wallets processed, conflicts) needed for monitoring
**Benefit**: Client can track operation success without additional API calls

## Migration Impact

### Before (Story 7.0)
- Endpoints directly called services
- Business logic scattered across layers
- JWT generation inconsistent
- No clear separation of concerns

### After (Story 7.1)
- Clean architectural layers
- All business logic in providers
- Centralized JWT generation
- Clear service responsibilities
- Testable components

## Testing Strategy

### Unit Tests
- Providers: Mock all injected services, test business logic
- Orchestrator: Mock providers and token service
- Handlers: Mock orchestrator only
- Services: Test domain logic in isolation

### Integration Tests
- Full flow from endpoint to database
- Token generation and validation
- Cache warming verification
- Transaction rollback scenarios

### Key Test Scenarios
1. New user registration with wallets
2. Existing user with new wallets
3. Wallet ownership conflicts
4. Identity resolution across providers
5. Token expiration and refresh
6. Cache hit rates

## Security Considerations

1. **Single Token Validation**: Exchange endpoint bypasses middleware to prevent double validation
2. **Token Validation**: Dynamic tokens validated against JWKS in provider only
3. **Replay Protection**: Challenges include nonces and timestamps
4. **Wallet Ownership**: Verified through signature or attestation
5. **Principal Isolation**: Each principal has unique identity across system
6. **Audit Trail**: All authentications logged with correlation IDs

## Performance Optimizations

1. **JWKS Caching**: Dynamic JWKS cached for 24 hours
2. **Batch Processing**: Wallets processed in single query
3. **Cache Warming**: User context cached for 15-30 minutes
4. **Connection Pooling**: EF Core connection pooling enabled
5. **Async Operations**: All I/O operations fully async

## Monitoring and Observability

### Key Metrics
- Authentication success/failure rates by provider
- Token generation latency
- Cache hit rates
- Wallet processing metrics
- Identity conflict rates

### Logging
- Structured logging with correlation IDs
- Provider-specific log scopes
- Performance timing on all operations
- Error details with stack traces

### Distributed Tracing
- Activity spans for each layer
- Cross-service correlation
- Database query timing
- External service calls

## Future Enhancements

1. **Additional Providers**
   - OAuth (Google, GitHub, Discord)
   - Sign-In with Ethereum (SIWE)
   - Email/Password (traditional)

2. **Enhanced Security**
   - Multi-factor authentication
   - Device fingerprinting
   - Anomaly detection

3. **Performance**
   - Redis distributed cache
   - Read replicas for queries
   - GraphQL subscriptions

4. **Compliance**
   - GDPR data handling
   - Audit log retention
   - PII encryption

## Conclusion

The Story 7.1 refactoring successfully establishes a clean, maintainable authentication architecture with:
- Clear separation of concerns
- Centralized token generation
- Provider-based extensibility
- Comprehensive metrics and monitoring
- Production-ready security

All authentication flows now follow the same pattern, making the system easier to understand, test, and maintain.