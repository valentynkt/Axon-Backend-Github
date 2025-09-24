namespace Axon.Modules.Identity.Application.Services;

using System.Security.Claims;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Domain.Entities;
using Domain.ValueObjects;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Single orchestrator for all authentication flows.
/// JWT validation is handled by middleware, this focuses on business logic orchestration.
/// Replaces the fragmented logic across AuthenticationService and DynamicAuthService.
/// </summary>
public sealed class AuthenticationOrchestrator : IAuthenticationOrchestrator
{
    private readonly IEnumerable<IAuthenticationProvider> _providers;
    private readonly IJwtTokenService _tokenService;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthenticationOrchestrator> _logger;
    private readonly IChallengeService _challengeService;
    private readonly IOptions<AuthenticationOptions> _authOptions;

    public AuthenticationOrchestrator(
        IEnumerable<IAuthenticationProvider> providers,
        IJwtTokenService tokenService,
        UserManager<AxonUserAuth> userManager,
        IMemoryCache cache,
        ILogger<AuthenticationOrchestrator> logger,
        IChallengeService challengeService,
        IOptions<AuthenticationOptions> authOptions)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    /// <summary>
    /// Generates an authentication challenge for wallet signing - delegates to ChallengeService
    /// </summary>
    public async Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default)
    {
        // Delegate directly to ChallengeService
        return await _challengeService.GenerateChallengeAsync(chainId, walletAddress, audience, cancellationToken);
    }

    /// <summary>
    /// Validates the protected token (replaces HMAC) - delegates to ChallengeService
    /// </summary>
    public Result<bool, Error> ValidateMac(
        string message,
        string protectedToken,
        string keyVersion)
    {
        // Delegate directly to ChallengeService (now using Data Protection API)
        return _challengeService.ValidateMac(message, protectedToken, keyVersion);
    }

    /// <summary>
    /// Validates a challenge message structure and TTL - delegates to ChallengeService
    /// </summary>
    public Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,
        string expectedAddress,
        string expectedAudience)
    {
        // Delegate directly to ChallengeService
        return _challengeService.ValidateChallenge(message, expectedChainId, expectedAddress, expectedAudience);
    }

    /// <summary>
    /// Checks and marks a nonce as used for replay protection - delegates to ChallengeService
    /// </summary>
    public async Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage,
        string mkv,
        CancellationToken cancellationToken = default)
    {
        // Delegate directly to ChallengeService
        return await _challengeService.CheckAndMarkNonceUsedAsync(signedMessage, mkv, cancellationToken);
    }

    /// <summary>
    /// Process wallet-based authentication with signature verification
    /// </summary>
    public async Task<Result<AuthenticationResponse, Error>> AuthenticateWithWalletAsync(
        WalletAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting wallet authentication for chain {ChainId}, address {Address}",
                request.ChainId, MaskAddress(request.Address));

            // Find wallet provider
            var provider = GetProvider("wallet");
            if (provider == null)
            {
                return Result.Failure<AuthenticationResponse, Error>(
                    Error.NotSupported("Wallet authentication not configured"));
            }

            // Process authentication through provider
            var providerResult = await provider.AuthenticateAsync(request, cancellationToken);
            if (providerResult.IsFailure)
            {
                _logger.LogWarning("Wallet authentication failed: {Error}", providerResult.Error);
                return Result.Failure<AuthenticationResponse, Error>(providerResult.Error);
            }

            var authData = providerResult.Value;

            // Generate token using existing TokenService
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(
                authData.User.AxonPrincipalId,
                ProviderType.From(authData.ProviderType),
                authData.User.OriginalSubject,
                authData.User.OriginalIssuer,
                30, // 30 minutes
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<AuthenticationResponse, Error>(tokenResult.Error);
            }

            var accessToken = tokenResult.Value.AccessToken;

            var response = new AuthenticationResponse(
                AccessToken: accessToken,
                UserId: authData.User.Id,
                ProviderType: "wallet",
                ExpiresAt: DateTime.UtcNow.AddMinutes(30),
                AdditionalData: authData.AdditionalClaims);

            _logger.LogInformation("Wallet authentication successful for principal {PrincipalId} in {Duration}ms",
                authData.User.AxonPrincipalId.Value, stopwatch.ElapsedMilliseconds);

            return Result.Success<AuthenticationResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet authentication failed unexpectedly");
            return Result.Failure<AuthenticationResponse, Error>(
                Error.Internal("Authentication failed"));
        }
    }

    /// <summary>
    /// Process Dynamic.xyz JWT token exchange
    /// </summary>
    public async Task<Result<AuthenticationResponse, Error>> ExchangeDynamicTokenAsync(
        string dynamicToken,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting Dynamic token exchange");

            // Find Dynamic provider
            var provider = GetProvider("dynamic");
            if (provider == null)
            {
                return Result.Failure<AuthenticationResponse, Error>(
                    Error.NotSupported("Dynamic authentication not configured"));
            }

            // Create exchange request
            var exchangeRequest = new DynamicExchangeRequest(dynamicToken);

            // Process through provider
            var providerResult = await provider.AuthenticateAsync(exchangeRequest, cancellationToken);
            if (providerResult.IsFailure)
            {
                _logger.LogWarning("Dynamic token exchange failed: {Error}", providerResult.Error);
                return Result.Failure<AuthenticationResponse, Error>(providerResult.Error);
            }

            var authData = providerResult.Value;

            // Generate Axon token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(
                authData.User.AxonPrincipalId,
                ProviderType.From(authData.ProviderType),
                authData.User.OriginalSubject,
                authData.User.OriginalIssuer,
                30, // 30 minutes
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<AuthenticationResponse, Error>(tokenResult.Error);
            }

            var accessToken = tokenResult.Value.AccessToken;

            var response = new AuthenticationResponse(
                AccessToken: accessToken,
                UserId: authData.User.Id,
                ProviderType: "dynamic",
                ExpiresAt: authData.TokenExpiresAt ?? DateTime.UtcNow.AddMinutes(30),
                AdditionalData: authData.AdditionalClaims);

            _logger.LogInformation("Dynamic token exchange successful for principal {PrincipalId} in {Duration}ms",
                authData.User.AxonPrincipalId.Value, stopwatch.ElapsedMilliseconds);

            return Result.Success<AuthenticationResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic token exchange failed unexpectedly");
            return Result.Failure<AuthenticationResponse, Error>(
                Error.Internal("Token exchange failed"));
        }
    }

    /// <summary>
    /// Process pre-validated claims from JWT middleware (for already validated tokens)
    /// </summary>
    public async Task<Result<AuthenticationResponse, Error>> ProcessValidatedClaimsAsync(
        ClaimsPrincipal principal,
        string providerType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        try
        {
            _logger.LogDebug("Processing validated claims for provider {ProviderType}", providerType);

            // Extract claims into dictionary
            var claims = principal.Claims.ToDictionary(
                c => c.Type,
                c => (object)c.Value);

            // Find appropriate provider
            var provider = GetProvider(providerType);
            if (provider == null)
            {
                return Result.Failure<AuthenticationResponse, Error>(
                    Error.NotSupported($"Provider {providerType} not configured"));
            }

            // Create validated request
            var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subject))
            {
                return Result.Failure<AuthenticationResponse, Error>(
                    Error.Validation("No subject found in claims"));
            }

            var validatedRequest = new ValidatedClaimsRequest(claims, providerType, subject);

            // Process through provider
            var providerResult = await provider.AuthenticateAsync(validatedRequest, cancellationToken);
            if (providerResult.IsFailure)
            {
                return Result.Failure<AuthenticationResponse, Error>(providerResult.Error);
            }

            var authData = providerResult.Value;

            // Generate new Axon token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(
                authData.User.AxonPrincipalId,
                ProviderType.From(authData.ProviderType),
                authData.User.OriginalSubject,
                authData.User.OriginalIssuer,
                30, // 30 minutes
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<AuthenticationResponse, Error>(tokenResult.Error);
            }

            var accessToken = tokenResult.Value.AccessToken;

            var response = new AuthenticationResponse(
                AccessToken: accessToken,
                UserId: authData.User.Id,
                ProviderType: providerType,
                ExpiresAt: authData.TokenExpiresAt ?? DateTime.UtcNow.AddMinutes(30),
                AdditionalData: authData.AdditionalClaims);

            return Result.Success<AuthenticationResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process validated claims");
            return Result.Failure<AuthenticationResponse, Error>(
                Error.Internal("Failed to process authenticated user"));
        }
    }

    /// <summary>
    /// Get current authenticated user from claims principal (JWT already validated by middleware)
    /// </summary>
    public async Task<Result<AxonUserAuth, Error>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        try
        {
            var userIdClaim = principal.FindFirst("axon_user_id")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Result.Failure<AxonUserAuth, Error>(
                    Error.Unauthorized("Invalid user token"));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return Result.Failure<AxonUserAuth, Error>(
                    Error.NotFound("User not found"));
            }

            return Result.Success<AxonUserAuth, Error>(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
            return Result.Failure<AxonUserAuth, Error>(
                Error.Internal("Failed to retrieve user"));
        }
    }

    /// <summary>
    /// Refresh an existing authentication token
    /// </summary>
    public async Task<Result<RefreshTokenResponse, Error>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse refresh token to extract user info
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(refreshToken);

            if (jsonToken == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Get JTI for tracking
            var jti = jsonToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Check if refresh token was already used (replay protection)
            var cacheKey = $"refresh:used:{jti}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Refresh token already used"));
            }

            // Get user ID from token
            var userIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "axon_user_id")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Get user from database
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.NotFound("User not found"));
            }

            // Generate new access token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(
                user.AxonPrincipalId,
                ProviderType.From(user.ProviderType),
                user.OriginalSubject,
                user.OriginalIssuer,
                30, // 30 minutes
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<RefreshTokenResponse, Error>(tokenResult.Error);
            }

            var newAccessToken = tokenResult.Value.AccessToken;

            // Generate new refresh token
            var newRefreshTokenResult = await _tokenService.GenerateRefreshTokenAsync(
                user.AxonPrincipalId,
                ProviderType.From(user.ProviderType),
                user.OriginalSubject,
                user.OriginalIssuer,
                cancellationToken);

            if (newRefreshTokenResult.IsFailure)
            {
                return Result.Failure<RefreshTokenResponse, Error>(newRefreshTokenResult.Error);
            }

            var newRefreshToken = newRefreshTokenResult.Value.RefreshToken;

            // Mark old refresh token as used (prevent replay)
            _cache.Set(cacheKey, true, TimeSpan.FromDays(30));

            var response = new RefreshTokenResponse(
                AccessToken: newAccessToken,
                RefreshToken: newRefreshToken,
                TokenType: "Bearer",
                ExpiresIn: 1800, // 30 minutes in seconds
                IssuedAt: DateTimeOffset.UtcNow,
                AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

            _logger.LogInformation("Token refreshed successfully for user {UserId}", userId);

            return Result.Success<RefreshTokenResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Internal("Failed to refresh token"));
        }
    }

    /// <summary>
    /// Invalidate a user's current session (logout)
    /// </summary>
    public async Task<UnitResult<Error>> InvalidateSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return UnitResult.Failure(Error.NotFound("User not found"));
            }

            // Update security stamp to invalidate all existing tokens
            await _userManager.UpdateSecurityStampAsync(user);

            // Clear any cached data for this user
            var userCacheKey = $"user:session:{userId}";
            _cache.Remove(userCacheKey);

            _logger.LogInformation("Session invalidated for user {UserId}", userId);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate session");
            return UnitResult.Failure(Error.Internal("Failed to invalidate session"));
        }
    }

    private IAuthenticationProvider? GetProvider(string providerType)
    {
        return _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderType, providerType, StringComparison.OrdinalIgnoreCase));
    }

    private static string MaskAddress(string address)
    {
        return address.Length > 8
            ? $"{address[..4]}...{address[^4..]}"
            : address;
    }

}