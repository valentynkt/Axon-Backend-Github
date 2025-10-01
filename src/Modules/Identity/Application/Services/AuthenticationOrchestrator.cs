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
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly SignInManager<AxonUserAuth> _signInManager;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuthenticationOrchestrator> _logger;
    private readonly IChallengeService _challengeService;
    private readonly IRefreshTokenProvider _refreshTokenProvider;
    private readonly IOptions<AuthenticationOptions> _authOptions;

    public AuthenticationOrchestrator(
        IEnumerable<IAuthenticationProvider> providers,
        IJwtTokenService tokenService,
        UserManager<AxonUserAuth> userManager,
        SignInManager<AxonUserAuth> signInManager,
        IDistributedCache cache,
        ILogger<AuthenticationOrchestrator> logger,
        IChallengeService challengeService,
        IRefreshTokenProvider refreshTokenProvider,
        IOptions<AuthenticationOptions> authOptions)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
        _refreshTokenProvider = refreshTokenProvider ?? throw new ArgumentNullException(nameof(refreshTokenProvider));
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
                UserId: authData.User.AxonPrincipalId.Value,
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

            // Create exchange request with the raw token
            var exchangeRequest = new DynamicExchangeRequest(dynamicToken);

            // Process through provider - this now handles ALL the complex logic
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

            // Ensure we have all the metrics in additional data for the response
            var additionalData = authData.AdditionalClaims ?? new Dictionary<string, object>();

            // The provider should have set these, but we ensure they exist
            if (!additionalData.ContainsKey("created"))
                additionalData["created"] = false;
            if (!additionalData.ContainsKey("wallets_processed"))
                additionalData["wallets_processed"] = 0;
            if (!additionalData.ContainsKey("wallets_linked"))
                additionalData["wallets_linked"] = 0;
            if (!additionalData.ContainsKey("defaults_applied"))
                additionalData["defaults_applied"] = 0;
            if (!additionalData.ContainsKey("skipped"))
                additionalData["skipped"] = 0;
            if (!additionalData.ContainsKey("conflicts"))
                additionalData["conflicts"] = 0;

            var response = new AuthenticationResponse(
                AccessToken: accessToken,
                UserId: authData.User.AxonPrincipalId.Value,
                ProviderType: "dynamic",
                ExpiresAt: authData.TokenExpiresAt ?? DateTime.UtcNow.AddMinutes(30),
                AdditionalData: additionalData);

            _logger.LogInformation("Dynamic token exchange successful for principal {PrincipalId} in {Duration}ms",
                authData.User.AxonPrincipalId.Value, stopwatch.ElapsedMilliseconds);

            return Result.Success<AuthenticationResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Dynamic token exchange failed unexpectedly - Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ex.GetType().FullName, ex.Message, ex.StackTrace);

            // Log inner exception if present
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception - Type: {InnerType}, Message: {InnerMessage}, StackTrace: {InnerStackTrace}",
                    ex.InnerException.GetType().FullName, ex.InnerException.Message, ex.InnerException.StackTrace);
            }

            return Result.Failure<AuthenticationResponse, Error>(
                Error.Internal($"Token exchange failed: {ex.Message}"));
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
                UserId: authData.User.AxonPrincipalId.Value,
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
    /// Refresh an existing authentication token using Identity's token system
    /// </summary>
    public async Task<Result<RefreshTokenResponse, Error>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract JTI for replay protection
            var jti = _refreshTokenProvider.GetJtiFromToken(refreshToken);
            if (string.IsNullOrEmpty(jti))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Check if refresh token was already used (replay protection)
            var cacheKey = $"refresh:used:{jti}";
            var existingValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(existingValue))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Refresh token already used"));
            }

            // Extract user ID from token for direct lookup (performance optimization)
            var userId = _refreshTokenProvider.GetUserIdFromToken(refreshToken);
            if (userId == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token format"));
            }

            // Direct lookup instead of O(n) iteration
            var validUser = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (validUser == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("User not found"));
            }

            // Validate token against the specific user
            var isValid = await _refreshTokenProvider.ValidateAsync(
                "RefreshToken",
                refreshToken,
                _userManager,
                validUser);

            if (!isValid)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Generate new access token
            var tokenResult = await _tokenService.GenerateAccessTokenAsync(
                validUser.AxonPrincipalId,
                ProviderType.From(validUser.ProviderType),
                validUser.OriginalSubject,
                validUser.OriginalIssuer,
                30, // 30 minutes
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<RefreshTokenResponse, Error>(tokenResult.Error);
            }

            var newAccessToken = tokenResult.Value.AccessToken;

            // Generate new refresh token using Identity's token system
            var newRefreshToken = await _refreshTokenProvider.GenerateAsync(
                "RefreshToken",
                _userManager,
                validUser);

            // Mark old refresh token as used (prevent replay) with sliding expiration
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(30)
            };
            await _cache.SetStringAsync(cacheKey, "used", options, cancellationToken);

            var response = new RefreshTokenResponse(
                AccessToken: newAccessToken,
                RefreshToken: newRefreshToken,
                TokenType: "Bearer",
                ExpiresIn: 1800, // 30 minutes in seconds
                IssuedAt: DateTimeOffset.UtcNow,
                AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
                RefreshTokenExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

            _logger.LogInformation("Token refreshed successfully for user {UserId}", validUser.Id);

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
    /// Invalidate a user's current session using SignInManager for complete cleanup
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

            // Use SignInManager for complete session cleanup
            // This updates security stamp and clears cookies/session automatically
            await _signInManager.SignOutAsync();

            // Update security stamp to invalidate all existing tokens
            await _userManager.UpdateSecurityStampAsync(user);

            // Clear distributed cache entries for this user
            var cacheKeys = new[]
            {
                $"user:session:{userId}",
                $"user:tokens:{userId}",
                $"user:refresh:{userId}"
            };

            foreach (var key in cacheKeys)
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }

            _logger.LogInformation("Session completely invalidated for user {UserId} using SignInManager", userId);

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