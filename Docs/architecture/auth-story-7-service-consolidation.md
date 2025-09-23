# Auth Story 7: Authentication Service Consolidation

**Story ID**: AUTH-007
**Title**: Consolidate Authentication Services and Remove Redundant Code
**Priority**: MEDIUM (Can be HIGH if done with AUTH-006)
**Estimated Effort**: 2-3 days (can be done in parallel with AUTH-006)
**Dependencies**: AUTH-005 (Identity Integration - MUST be complete)
**Parallel Work**: Can be implemented alongside AUTH-006
**Related Stories**: AUTH-008 (Multi-Chain Preparation)

## Overview

Consolidate duplicate authentication logic across multiple services, remove redundant validation code that's now handled by JWT Bearer middleware, and refactor the authentication service layer into a clean orchestration layer.

## Problem Statement

### Current Issues
1. **Service Fragmentation**: Authentication logic spread across multiple services
   - `AuthenticationService.cs` - 823 lines with mixed validation and business logic
   - `TokenService.cs` - 179 lines (already uses UserManager but broken with AxonUser)
   - No separate `DynamicAuthService.cs` file (functionality embedded in AuthenticationService)
2. **Redundant Validation**: Custom validation logic that will be removed by AUTH-006
3. **Unclear Separation**: Business logic mixed with JWT validation in single service
4. **Dependencies Clear**: AUTH-005 fixes TokenService, AUTH-006 removes validation overhead

### Business Impact
- Maintenance burden from duplicate code paths
- Increased bug risk due to multiple validation implementations
- Difficulty adding new authentication providers
- Complex testing due to service interdependencies

## Solution Overview

Consolidate authentication services into a clean, layered architecture where JWT Bearer middleware handles validation, and application services handle business logic orchestration only.

### Key Components
1. **Single Authentication Orchestrator**: Replace multiple services with one orchestration layer
2. **Provider Strategy Pattern**: Clean separation for different auth providers (Dynamic, manual wallet)
3. **Remove Redundant Code**: Delete validation logic now handled by middleware
4. **Clear Interfaces**: Define explicit contracts between layers

## Acceptance Criteria

### ✅ Must Have
1. **Service Consolidation**: Replace `AuthenticationService` + `DynamicAuthService` with single orchestrator
2. **Remove Redundant Validation**: Delete custom JWT validation now handled by middleware
3. **Provider Abstraction**: Clean interface for different authentication providers
4. **Maintain Functionality**: All existing auth flows continue to work
5. **Clear Separation**: Business logic separated from infrastructure concerns
6. **Reduced Code**: 50%+ reduction in authentication service code

### ✅ Should Have
1. **Improved Testing**: Clearer unit test boundaries with reduced mocking
2. **Better Error Handling**: Consistent error response patterns
3. **Audit Logging**: Centralized authentication event logging
4. **Performance Metrics**: Track authentication flow performance

### ✅ Could Have
1. **Authentication Middleware**: Custom middleware for additional business logic
2. **Provider Discovery**: Dynamic registration of authentication providers
3. **Health Checks**: Authentication service health monitoring
4. **Caching Strategy**: Optimize repeated operations

## Technical Specification

### 1. Consolidated Authentication Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    API Controllers                       │
│  (JWT Bearer middleware validates tokens automatically)  │
└─────────────────────────┬───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│              Authentication Orchestrator                │
│        (Business logic and flow coordination)           │
└─────────────────────────┬───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│              Provider Strategy Layer                     │
│   ┌─────────────────┐  ┌─────────────────┐             │
│   │ Manual Wallet   │  │  Dynamic.xyz    │             │
│   │   Provider      │  │   Provider      │             │
│   └─────────────────┘  └─────────────────┘             │
└─────────────────────────┬───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│                 Domain Services                         │
│  (Principal resolution, wallet ownership, etc.)         │
└─────────────────────────────────────────────────────────┘
```

### 2. Authentication Orchestrator

```csharp
namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Single orchestrator for all authentication flows
/// JWT validation is handled by middleware, this focuses on business logic
/// </summary>
public sealed class AuthenticationOrchestrator : IAuthenticationOrchestrator
{
    private readonly IEnumerable<IAuthenticationProvider> _providers;
    private readonly ITokenService _tokenService;
    private readonly IAuthenticationEventLogger _eventLogger;
    private readonly ILogger<AuthenticationOrchestrator> _logger;

    public AuthenticationOrchestrator(
        IEnumerable<IAuthenticationProvider> providers,
        ITokenService tokenService,
        IAuthenticationEventLogger eventLogger,
        ILogger<AuthenticationOrchestrator> logger)
    {
        _providers = providers;
        _tokenService = tokenService;
        _eventLogger = eventLogger;
        _logger = logger;
    }

    /// <summary>
    /// Process wallet-based authentication (manual sign-in flow)
    /// </summary>
    public async Task<Result<AuthenticationResult, Error>> AuthenticateWithWalletAsync(
        WalletAuthenticationRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting wallet authentication for chain {ChainId}, address {Address}",
                request.ChainId, MaskAddress(request.Address));

            // Find appropriate provider
            var provider = GetProvider("wallet");
            if (provider == null)
                return Failure(Error.NotSupported("Wallet authentication not supported"));

            // Process authentication through provider
            var providerResult = await provider.AuthenticateAsync(request, ct);
            if (providerResult.IsFailure)
            {
                await _eventLogger.LogAuthenticationFailureAsync(
                    "wallet", request.ChainId, request.Address, providerResult.Error, ct);
                return Failure(providerResult.Error);
            }

            var authData = providerResult.Value;

            // Generate token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(authData.User, ct);
            if (tokenResult.IsFailure)
                return Failure(tokenResult.Error);

            var result = new AuthenticationResult(
                AccessToken: tokenResult.Value,
                UserId: authData.User.AxonPrincipalId,
                AuthProvider: "wallet",
                ExpiresAt: DateTime.UtcNow.AddMinutes(30));

            await _eventLogger.LogAuthenticationSuccessAsync(
                "wallet", request.ChainId, request.Address, result.UserId, ct);

            _logger.LogInformation("Wallet authentication successful for principal {PrincipalId} in {Duration}ms",
                result.UserId, stopwatch.ElapsedMilliseconds);

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet authentication failed unexpectedly");
            return Failure(Error.Internal("Authentication failed"));
        }
    }

    /// <summary>
    /// Process Dynamic.xyz JWT exchange (token already validated by middleware)
    /// </summary>
    public async Task<Result<AuthenticationResult, Error>> ExchangeDynamicTokenAsync(
        ClaimsPrincipal validatedPrincipal,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var sub = validatedPrincipal.FindFirst("sub")?.Value;
            _logger.LogInformation("Starting Dynamic token exchange for subject {Subject}", sub);

            // Find Dynamic provider
            var provider = GetProvider("dynamic");
            if (provider == null)
                return Failure(Error.NotSupported("Dynamic authentication not supported"));

            // Convert validated claims to exchange request
            var exchangeRequest = CreateExchangeRequest(validatedPrincipal);

            // Process through provider
            var providerResult = await provider.AuthenticateAsync(exchangeRequest, ct);
            if (providerResult.IsFailure)
            {
                await _eventLogger.LogAuthenticationFailureAsync(
                    "dynamic", null, sub, providerResult.Error, ct);
                return Failure(providerResult.Error);
            }

            var authData = providerResult.Value;

            // Generate Axon token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(authData.User, ct);
            if (tokenResult.IsFailure)
                return Failure(tokenResult.Error);

            var result = new AuthenticationResult(
                AccessToken: tokenResult.Value,
                UserId: authData.User.AxonPrincipalId,
                AuthProvider: "dynamic",
                ExpiresAt: DateTime.UtcNow.AddMinutes(30));

            await _eventLogger.LogAuthenticationSuccessAsync(
                "dynamic", null, sub, result.UserId, ct);

            _logger.LogInformation("Dynamic token exchange successful for principal {PrincipalId} in {Duration}ms",
                result.UserId, stopwatch.ElapsedMilliseconds);

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic token exchange failed unexpectedly");
            return Failure(Error.Internal("Token exchange failed"));
        }
    }

    /// <summary>
    /// Get current authenticated user (JWT already validated by middleware)
    /// </summary>
    public async Task<Result<UserInfo, Error>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        try
        {
            var userIdClaim = principal.FindFirst("axon_user_id")?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Failure(Error.Unauthorized("Invalid user token"));

            var userManager = _providers.OfType<IUserProvider>().FirstOrDefault();
            if (userManager == null)
                return Failure(Error.Internal("User provider not available"));

            var userResult = await userManager.GetUserByIdAsync(userId, ct);
            if (userResult.IsFailure)
                return Failure(userResult.Error);

            return Success(userResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
            return Failure(Error.Internal("Failed to retrieve user"));
        }
    }

    private IAuthenticationProvider? GetProvider(string providerType)
        => _providers.FirstOrDefault(p => p.SupportsProviderType(providerType));

    private static string MaskAddress(string address)
        => address.Length > 8 ? $"{address[..4]}...{address[^4..]}" : address;
}
```

### 3. Provider Strategy Interface

```csharp
namespace Axon.Modules.Identity.Application.Contracts;

/// <summary>
/// Strategy interface for different authentication providers
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Indicates if this provider supports the given authentication type
    /// </summary>
    bool SupportsProviderType(string providerType);

    /// <summary>
    /// Process authentication request and return authenticated user data
    /// </summary>
    Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Base authentication request
/// </summary>
public abstract record AuthenticationRequest(string ProviderType);

/// <summary>
/// Wallet-based authentication request
/// </summary>
public sealed record WalletAuthenticationRequest(
    string ChainId,
    string Address,
    byte[] Message,
    byte[] Signature,
    string Mac,
    int KeyVersion) : AuthenticationRequest("wallet");

/// <summary>
/// Dynamic token exchange request
/// </summary>
public sealed record DynamicExchangeRequest(
    string Subject,
    string Issuer,
    Dictionary<string, object> Claims) : AuthenticationRequest("dynamic");

/// <summary>
/// Successful authentication data
/// </summary>
public sealed record AuthenticationData(
    AxonUserAuth User,
    string ProviderType,
    Dictionary<string, object> AdditionalClaims);
```

### 4. Wallet Authentication Provider

```csharp
namespace Axon.Modules.Identity.Application.Providers;

/// <summary>
/// Handles manual wallet signature authentication
/// </summary>
public sealed class WalletAuthenticationProvider : IAuthenticationProvider
{
    private readonly IWalletSignatureVerifier _signatureVerifier;
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IChallengeService _challengeService;
    private readonly ILogger<WalletAuthenticationProvider> _logger;

    public WalletAuthenticationProvider(
        IWalletSignatureVerifier signatureVerifier,
        IAxonPrincipalWriteRepository principalRepo,
        UserManager<AxonUserAuth> userManager,
        IChallengeService challengeService,
        ILogger<WalletAuthenticationProvider> logger)
    {
        _signatureVerifier = signatureVerifier;
        _principalRepo = principalRepo;
        _userManager = userManager;
        _challengeService = challengeService;
        _logger = logger;
    }

    public bool SupportsProviderType(string providerType) => providerType == "wallet";

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken ct = default)
    {
        if (request is not WalletAuthenticationRequest walletRequest)
            return Result<AuthenticationData, Error>.Failure(
                Error.Validation("Invalid request type for wallet provider"));

        try
        {
            // Validate challenge MAC and timing
            var challengeValidation = await _challengeService.ValidateChallengeAsync(
                walletRequest.Message, walletRequest.Mac, walletRequest.KeyVersion, ct);

            if (challengeValidation.IsFailure)
                return Result<AuthenticationData, Error>.Failure(challengeValidation.Error);

            // Verify wallet signature
            var signatureRequest = new WalletSignatureRequest(
                walletRequest.ChainId,
                walletRequest.Address,
                walletRequest.Message,
                walletRequest.Signature);

            var signatureResult = await _signatureVerifier.VerifySignatureAsync(signatureRequest, ct);
            if (signatureResult.IsFailure || !signatureResult.Value)
                return Result<AuthenticationData, Error>.Failure(
                    Error.Unauthorized("Invalid wallet signature"));

            // Resolve or create principal and wallet ownership
            var principalResult = await ResolveOrCreatePrincipalAsync(
                walletRequest.ChainId, walletRequest.Address, ct);

            if (principalResult.IsFailure)
                return Result<AuthenticationData, Error>.Failure(principalResult.Error);

            var principal = principalResult.Value;

            // Get or create Identity user
            var identityUser = await GetOrCreateIdentityUserAsync(principal, walletRequest, ct);
            if (identityUser == null)
                return Result<AuthenticationData, Error>.Failure(
                    Error.Internal("Failed to create Identity user"));

            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "wallet",
                AdditionalClaims: new Dictionary<string, object>
                {
                    ["chain_id"] = walletRequest.ChainId,
                    ["wallet_address"] = walletRequest.Address,
                    ["auth_method"] = "signature"
                });

            _logger.LogInformation("Wallet authentication successful for {Address} on {ChainId}",
                MaskAddress(walletRequest.Address), walletRequest.ChainId);

            return Result<AuthenticationData, Error>.Success(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet authentication failed");
            return Result<AuthenticationData, Error>.Failure(
                Error.Internal("Wallet authentication failed"));
        }
    }

    // ... implementation methods (moved from AuthenticationService)
}
```

### 5. Dynamic Authentication Provider

```csharp
namespace Axon.Modules.Identity.Application.Providers;

/// <summary>
/// Handles Dynamic.xyz JWT token exchange
/// </summary>
public sealed class DynamicAuthenticationProvider : IAuthenticationProvider
{
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IDynamicCredentialParser _credentialParser;
    private readonly ILogger<DynamicAuthenticationProvider> _logger;

    public DynamicAuthenticationProvider(
        IAxonPrincipalWriteRepository principalRepo,
        UserManager<AxonUserAuth> userManager,
        IDynamicCredentialParser credentialParser,
        ILogger<DynamicAuthenticationProvider> logger)
    {
        _principalRepo = principalRepo;
        _userManager = userManager;
        _credentialParser = credentialParser;
        _logger = logger;
    }

    public bool SupportsProviderType(string providerType) => providerType == "dynamic";

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken ct = default)
    {
        if (request is not DynamicExchangeRequest dynamicRequest)
            return Result<AuthenticationData, Error>.Failure(
                Error.Validation("Invalid request type for Dynamic provider"));

        try
        {
            // Parse verified credentials from Dynamic token
            var credentialsResult = _credentialParser.ParseCredentials(dynamicRequest.Claims);
            if (credentialsResult.IsFailure)
                return Result<AuthenticationData, Error>.Failure(credentialsResult.Error);

            var credentials = credentialsResult.Value;

            // Resolve or create principals for all verified wallets
            var principalResults = await Task.WhenAll(
                credentials.Select(cred =>
                    ResolveOrCreatePrincipalAsync(cred.ChainId, cred.Address, ct)));

            var failedResult = principalResults.FirstOrDefault(r => r.IsFailure);
            if (failedResult != null)
                return Result<AuthenticationData, Error>.Failure(failedResult.Error);

            // For now, use the first principal (later we might consolidate)
            var primaryPrincipal = principalResults.First().Value;

            // Get or create Identity user
            var identityUser = await GetOrCreateIdentityUserAsync(primaryPrincipal, dynamicRequest, ct);
            if (identityUser == null)
                return Result<AuthenticationData, Error>.Failure(
                    Error.Internal("Failed to create Identity user"));

            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "dynamic",
                AdditionalClaims: new Dictionary<string, object>
                {
                    ["dynamic_subject"] = dynamicRequest.Subject,
                    ["dynamic_issuer"] = dynamicRequest.Issuer,
                    ["verified_wallets"] = credentials,
                    ["auth_method"] = "jwt_exchange"
                });

            _logger.LogInformation("Dynamic authentication successful for subject {Subject}",
                dynamicRequest.Subject);

            return Result<AuthenticationData, Error>.Success(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic authentication failed");
            return Result<AuthenticationData, Error>.Failure(
                Error.Internal("Dynamic authentication failed"));
        }
    }

    // ... implementation methods (consolidated from DynamicAuthService)
}
```

### 6. Service Registration

```csharp
// In IdentityApiModule.cs
public static IServiceCollection AddAuthenticationServices(
    this IServiceCollection services)
{
    // Register orchestrator
    services.AddScoped<IAuthenticationOrchestrator, AuthenticationOrchestrator>();

    // Register providers
    services.AddScoped<IAuthenticationProvider, WalletAuthenticationProvider>();
    services.AddScoped<IAuthenticationProvider, DynamicAuthenticationProvider>();

    // Register supporting services
    services.AddScoped<IAuthenticationEventLogger, AuthenticationEventLogger>();
    services.AddScoped<IDynamicCredentialParser, DynamicCredentialParser>();
    services.AddScoped<IChallengeService, ChallengeService>();

    // Remove old services
    // services.AddScoped<AuthenticationService>(); // REMOVED
    // services.AddScoped<DynamicAuthService>();    // REMOVED

    return services;
}
```

## Implementation Sequencing

### Coordination with AUTH-006
- If AUTH-006 is done first: Remove JWT validation code, then consolidate
- If done in parallel: Create providers while AUTH-006 removes validation
- If AUTH-007 done first: Keep validation temporarily, remove with AUTH-006

## Implementation Tasks

### Phase 1: Create New Architecture (Day 1)
1. **Define Interfaces**
   - [ ] Create IAuthenticationOrchestrator interface
   - [ ] Create IAuthenticationProvider strategy interface
   - [ ] Define request/response DTOs
   - [ ] Create provider discovery mechanism

2. **Implement Orchestrator**
   - [ ] Create AuthenticationOrchestrator with clean business logic
   - [ ] Implement authentication flow coordination
   - [ ] Add comprehensive logging and metrics
   - [ ] Implement error handling patterns

### Phase 2: Create Providers (Day 2-3)
3. **Wallet Authentication Provider**
   - [ ] Move wallet authentication logic from AuthenticationService
   - [ ] Implement IAuthenticationProvider interface
   - [ ] Clean up and simplify wallet verification flow
   - [ ] Add provider-specific logging

4. **Dynamic Authentication Provider**
   - [ ] Consolidate logic from DynamicAuthService
   - [ ] Implement IAuthenticationProvider interface
   - [ ] Simplify Dynamic credential parsing
   - [ ] Add provider-specific error handling

### Phase 3: Remove Old Code (Day 3)
5. **Delete Redundant Services**
   - [ ] Remove old AuthenticationService (keep minimal orchestration)
   - [ ] Remove DynamicAuthService entirely
   - [ ] Update service registration
   - [ ] Remove redundant interfaces

6. **Update Controllers**
   - [ ] Update auth controllers to use new orchestrator
   - [ ] Remove direct service dependencies
   - [ ] Maintain API contract compatibility
   - [ ] Update error handling

### Phase 4: Testing & Optimization (Day 3-4)
7. **Update Tests**
   - [ ] Refactor unit tests for new architecture
   - [ ] Test provider isolation
   - [ ] Test orchestrator coordination
   - [ ] Update integration tests

8. **Performance Validation**
   - [ ] Benchmark new vs old implementation
   - [ ] Validate memory usage improvements
   - [ ] Test concurrent authentication flows
   - [ ] Monitor authentication metrics

## Code Reduction Analysis

### Before (Current State) - After AUTH-005 & AUTH-006
```
AuthenticationService.cs: 823 lines → ~300 lines after AUTH-006
TokenService.cs: 179 lines (remains, fixed in AUTH-005)
Embedded Dynamic logic: ~100 lines
Service coordination: ~50 lines
Total: ~629 lines
```

### After (Consolidated Architecture)
```
AuthenticationOrchestrator.cs: 200 lines
WalletAuthenticationProvider.cs: 150 lines
DynamicAuthenticationProvider.cs: 100 lines
TokenService.cs: 179 lines (unchanged, uses Identity)
Interfaces and DTOs: 50 lines
Total: 679 lines
```

**Note**: Slight increase due to better separation, but much cleaner architecture
**Cumulative Reduction from original**: 823 → 300 lines in AuthenticationService (64% decrease)

## Testing Strategy

### Unit Tests
- [ ] AuthenticationOrchestrator business logic
- [ ] Individual provider implementations
- [ ] Provider discovery and selection
- [ ] Error handling scenarios

### Integration Tests
- [ ] End-to-end auth flows through orchestrator
- [ ] Provider switching and fallback
- [ ] Multi-provider authentication scenarios
- [ ] Error propagation and logging

### Performance Tests
- [ ] Authentication flow performance comparison
- [ ] Memory usage with new architecture
- [ ] Concurrent provider operations
- [ ] Provider isolation impact

## Benefits of This Architecture

### Maintainability
1. **Single Responsibility**: Each provider handles one auth method
2. **Clear Interfaces**: Well-defined contracts between layers
3. **Testability**: Easy to mock and test individual components
4. **Extensibility**: Easy to add new authentication providers

### Performance
1. **Reduced Redundancy**: No duplicate validation logic
2. **Efficient Flow**: Direct orchestration without service hops
3. **Better Caching**: Provider-specific optimizations
4. **Parallel Processing**: Independent provider operations

### Security
1. **Separation of Concerns**: Business logic separated from validation
2. **Consistent Logging**: Centralized authentication audit trail
3. **Provider Isolation**: Failures in one provider don't affect others
4. **Clear Trust Boundaries**: Explicit validation handoffs

## Success Criteria

### Functional Requirements
- [ ] All existing authentication flows work with new architecture
- [ ] Provider isolation prevents cross-contamination
- [ ] Orchestrator properly coordinates authentication flows
- [ ] Error handling is consistent across providers

### Code Quality Requirements
- [ ] 33% reduction in authentication service code
- [ ] Improved test coverage with cleaner boundaries
- [ ] Better separation of concerns
- [ ] Cleaner dependency graph

### Performance Requirements
- [ ] Authentication performance within 5% of current
- [ ] Reduced memory footprint
- [ ] Better scaling with concurrent requests
- [ ] Improved error handling performance

This story creates a clean, maintainable authentication architecture that eliminates redundancy while providing a solid foundation for future authentication providers.