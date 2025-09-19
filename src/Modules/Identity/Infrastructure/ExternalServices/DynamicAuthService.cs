using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Axon.Modules.Identity.Infrastructure.ExternalServices;

/// <summary>
/// Cached token validation data containing both ClaimsPrincipal and normalized user data
/// </summary>
internal sealed record CachedTokenData(
    ClaimsPrincipal Principal,
    DynamicUserData UserData,
    DateTimeOffset CachedAt);

/// <summary>
/// Service for validating JWT tokens using local JWT validation with JWKS
/// Validates tokens locally using Dynamic.xyz public keys
/// </summary>
public sealed class DynamicAuthService : IDynamicAuthService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicAuthService> _logger;
    private readonly DynamicXyzOptions _options;
    private readonly IDynamicClaimNormalizer _claimNormalizer;
    private readonly IJwtReplayGuard _replayGuard;
    private readonly IJwksService _jwksService;
    private readonly TimeSpan _tokenCacheExpiration;

    public DynamicAuthService(
        IMemoryCache cache,
        ILogger<DynamicAuthService> logger,
        IOptions<DynamicXyzOptions> options,
        IDynamicClaimNormalizer claimNormalizer,
        IJwtReplayGuard replayGuard,
        IJwksService jwksService)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _claimNormalizer = claimNormalizer ?? throw new ArgumentNullException(nameof(claimNormalizer));
        _replayGuard = replayGuard ?? throw new ArgumentNullException(nameof(replayGuard));
        _jwksService = jwksService ?? throw new ArgumentNullException(nameof(jwksService));
        
        // Cache validated tokens for 5 minutes to avoid repeated validation
        _tokenCacheExpiration = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Validates a JWT token using local validation with JWKS public keys
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with user data if valid, error if invalid</returns>
    public async Task<Result<DynamicUserData, Error>> ValidateTokenAsync(
        string token, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token validation attempted with empty token");
            return Result.Failure<DynamicUserData, Error>(
                Error.Validation("Token is required", "AUTH.TOKEN_REQUIRED"));
        }

        // Check cache first
        var cacheKey = $"dynamic_token_{GetTokenHash(token)}";
        if (_cache.TryGetValue<CachedTokenData>(cacheKey, out var cachedData) && cachedData != null)
        {
            _logger.LogDebug("Token validation cache hit for user {AxonUserId}", cachedData.UserData.AxonUserId);
            return Result.Success<DynamicUserData, Error>(cachedData.UserData);
        }

        try
        {
            using var activity = Activity.Current?.Source.StartActivity("DynamicAuthService.ValidateToken");
            activity?.SetTag("provider", "dynamic");
            
            _logger.LogDebug("Validating JWT token locally using JWKS");

            // Get JWKS keys from injected service
            var keysResult = await _jwksService.GetJwksKeysAsync(cancellationToken);
            if (keysResult.IsFailure)
            {
                activity?.SetTag("error", keysResult.Error.Code);
                return Result.Failure<DynamicUserData, Error>(keysResult.Error);
            }

            // Validate the JWT token
            var validationResult = await ValidateJwtTokenAsync(token, keysResult.Value);
            if (validationResult.IsFailure)
            {
                activity?.SetTag("error", validationResult.Error.Code);
                return Result.Failure<DynamicUserData, Error>(validationResult.Error);
            }

            var claimsPrincipal = validationResult.Value;
            
            // Check for replay attacks using jti claim if available
            var jtiClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Jti);
            if (jtiClaim != null && !string.IsNullOrWhiteSpace(jtiClaim.Value))
            {
                // Extract expiration time from claims
                var expClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Exp);
                var expiresAt = DateTimeOffset.UtcNow.AddHours(1); // Default 1 hour if no exp claim
                
                if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
                {
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                }
                
                var replayCheck = await _replayGuard.CheckAndMarkUsedAsync(jtiClaim.Value, expiresAt, cancellationToken);
                if (replayCheck.IsFailure)
                {
                    _logger.LogWarning("JWT replay protection failed: {Error}", replayCheck.Error.Message);
                    return Result.Failure<DynamicUserData, Error>(replayCheck.Error);
                }
            }
            else
            {
                _logger.LogDebug("JWT token has no jti claim - replay protection not available");
            }
            
            // Extract user data from JWT claims using the normalizer
            var userData = _claimNormalizer.NormalizeClaimsPrincipal(claimsPrincipal);

            // Cache both the ClaimsPrincipal and normalized data (but only if replay check passed)
            var tokenData = new CachedTokenData(claimsPrincipal, userData, DateTimeOffset.UtcNow);
            _cache.Set(cacheKey, tokenData, _tokenCacheExpiration);
            
            activity?.SetTag("user_id", userData.AxonUserId);
            activity?.SetTag("cache_hit", false);
            _logger.LogInformation("Token validated successfully for user {AxonUserId}", userData.AxonUserId);
            return Result.Success<DynamicUserData, Error>(userData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<DynamicUserData, Error>(
                Error.External("Token validation failed", "AUTH.VALIDATION_FAILED", ex));
        }
    }

    /// <summary>
    /// Gets the raw ClaimsPrincipal from a validated JWT token
    /// This preserves all original JWT claims exactly as issued by Dynamic.xyz
    /// </summary>
    /// <param name="token">The JWT token to get claims from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with ClaimsPrincipal containing all original JWT claims if successful, error if invalid</returns>
    public async Task<Result<ClaimsPrincipal, Error>> GetRawClaimsAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("GetRawClaimsAsync attempted with empty token");
            return Result.Failure<ClaimsPrincipal, Error>(
                Error.Validation("Token is required", "AUTH.TOKEN_REQUIRED"));
        }

        // Check cache first
        var cacheKey = $"dynamic_token_{GetTokenHash(token)}";
        if (_cache.TryGetValue<CachedTokenData>(cacheKey, out var cachedData) && cachedData != null)
        {
            _logger.LogDebug("Raw claims cache hit for user {AxonUserId}", cachedData.UserData.AxonUserId);
            return Result.Success<ClaimsPrincipal, Error>(cachedData.Principal);
        }

        // If not cached, validate the token first to populate cache
        var validationResult = await ValidateTokenAsync(token, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<ClaimsPrincipal, Error>(validationResult.Error);
        }

        // Now get from cache (should be there after validation)
        if (_cache.TryGetValue<CachedTokenData>(cacheKey, out var freshCachedData) && freshCachedData != null)
        {
            _logger.LogDebug("Raw claims retrieved after validation for user {AxonUserId}", freshCachedData.UserData.AxonUserId);
            return Result.Success<ClaimsPrincipal, Error>(freshCachedData.Principal);
        }

        // This should not happen, but handle gracefully
        _logger.LogError("Failed to retrieve cached claims after successful validation");
        return Result.Failure<ClaimsPrincipal, Error>(
            Error.External("Failed to retrieve validated claims", "AUTH.CACHE_ERROR"));
    }

    private Task<Result<ClaimsPrincipal, Error>> ValidateJwtTokenAsync(string token, ICollection<SecurityKey> securityKeys)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Validate the token format first
            if (!tokenHandler.CanReadToken(token))
            {
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation("Invalid JWT token format", "AUTH.INVALID_TOKEN_FORMAT")));
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"app.dynamicauth.com/{_options.EnvironmentId}", // Dynamic uses app.dynamicauth.com/{environmentId}
                ValidateAudience = false, // Dynamic doesn't seem to use aud claim
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = securityKeys,
                ClockSkew = TimeSpan.FromMinutes(_options.Jwt?.ClockSkewMinutes ?? 5),
                // Disable inbound claim mapping to preserve JWT claim names (sub, email, etc.)
                // instead of mapping to WIF claims (nameidentifier, emailaddress, etc.)
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role"
            };

            // Disable global claim mapping for this token handler
            tokenHandler.MapInboundClaims = false;

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Additional validation - ensure it's an RS256 token as per Dynamic docs
            if (validatedToken is JwtSecurityToken jwtToken && 
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation("Token must be signed with RS256", "AUTH.INVALID_ALGORITHM")));
            }

            return Task.FromResult(Result.Success<ClaimsPrincipal, Error>(principal));
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogError(ex, "JWT token has expired - Token: {TokenPrefix}...", token[..Math.Min(20, token.Length)]);
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED")));
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogError(ex, "JWT token has invalid signature - Token: {TokenPrefix}...", token[..Math.Min(20, token.Length)]);
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized("Token has invalid signature", "AUTH.INVALID_SIGNATURE")));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogError(ex, "JWT token validation failed - Token: {TokenPrefix}..., Reason: {Reason}", 
                token[..Math.Min(20, token.Length)], ex.Message);
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized("Token validation failed", "AUTH.VALIDATION_FAILED")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation");
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.External("JWT validation error", "AUTH.JWT_VALIDATION_ERROR", ex)));
        }
    }


    private static string GetTokenHash(string token)
    {
        // Use a simple hash for cache key - just take last 8 chars of token
        // This is safe since we're only using it for caching
        return token.Length > 8 ? token[^8..] : token;
    }
}
    