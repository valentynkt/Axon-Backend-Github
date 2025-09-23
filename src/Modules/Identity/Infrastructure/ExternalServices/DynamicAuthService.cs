using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices;

/// <summary>
/// Dynamic JWT validation service with enhanced security features
/// </summary>
public sealed class DynamicAuthService : IDynamicAuthService, IHostedService, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicAuthService> _logger;
    private readonly DynamicXyzOptions _dynamicOptions;
    private readonly DynamicValidationOptions _validationOptions;
    private readonly IDynamicClaimNormalizer _claimNormalizer;
    private readonly IJwksService _jwksService;
    private readonly TimeSpan _tokenCacheExpiration;
    private Timer? _backgroundRefreshTimer;

    // Cache keys
    private const string JWKS_CACHE_KEY = "dynamic:jwks:keys";
    private const string ENVIRONMENT_VALIDATION_CACHE_KEY = "dynamic:env:validation";

    public DynamicAuthService(
        IMemoryCache cache,
        ILogger<DynamicAuthService> logger,
        IOptions<DynamicXyzOptions> dynamicOptions,
        IOptions<DynamicValidationOptions> validationOptions,
        IDynamicClaimNormalizer claimNormalizer,
        IJwksService jwksService)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dynamicOptions = dynamicOptions?.Value ?? throw new ArgumentNullException(nameof(dynamicOptions));
        _validationOptions = validationOptions?.Value ?? throw new ArgumentNullException(nameof(validationOptions));
        _claimNormalizer = claimNormalizer ?? throw new ArgumentNullException(nameof(claimNormalizer));
        _jwksService = jwksService ?? throw new ArgumentNullException(nameof(jwksService));

        // Cache validated tokens for 5 minutes to avoid repeated validation
        _tokenCacheExpiration = TimeSpan.FromMinutes(5);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Pre-warm JWKS cache on startup (AC7)
        _logger.LogInformation("Pre-warming JWKS cache for Dynamic JWT validation");
        var keysResult = await _jwksService.GetJwksKeysAsync(cancellationToken);
        if (keysResult.IsSuccess)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_validationOptions.JwksCacheMinutes)
            };
            _cache.Set(JWKS_CACHE_KEY, keysResult.Value, cacheOptions);
            _logger.LogInformation("JWKS cache pre-warmed successfully with {KeyCount} keys", keysResult.Value.Count);
        }
        else
        {
            _logger.LogWarning("Failed to pre-warm JWKS cache: {Error}", keysResult.Error.Message);
        }

        // Setup background refresh if enabled
        if (_validationOptions.EnableBackgroundRefresh)
        {
            var refreshInterval = TimeSpan.FromMinutes(_validationOptions.BackgroundRefreshMinutes);
            _backgroundRefreshTimer = new Timer(
                RefreshJwksCache,
                null,
                refreshInterval,
                refreshInterval);
            _logger.LogInformation("Background JWKS refresh enabled with {Interval} minute interval",
                _validationOptions.BackgroundRefreshMinutes);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _backgroundRefreshTimer?.Dispose();
        return Task.CompletedTask;
    }

    public async Task<Result<DynamicUserData, Error>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await ValidateTokenWithPartnerContextAsync(token, null, cancellationToken);
    }

    /// <summary>
    /// Enhanced token validation with partner context for audience validation
    /// </summary>
    public async Task<Result<DynamicUserData, Error>> ValidateTokenWithPartnerContextAsync(
        string token,
        string? partnerApiKey,
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

            // Get JWKS keys with caching
            var keysResult = await GetCachedJwksKeysAsync(cancellationToken);
            if (keysResult.IsFailure)
            {
                activity?.SetTag("error", keysResult.Error.Code);
                return Result.Failure<DynamicUserData, Error>(keysResult.Error);
            }

            // Validate the JWT token with enhanced validation
            var validationResult = await ValidateJwtTokenEnhancedAsync(
                token,
                keysResult.Value,
                partnerApiKey,
                cancellationToken);

            if (validationResult.IsFailure)
            {
                activity?.SetTag("error", validationResult.Error.Code);
                return Result.Failure<DynamicUserData, Error>(validationResult.Error);
            }

            var claimsPrincipal = validationResult.Value;

            // Check for replay attacks using jti claim
            var jtiClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Jti);
            if (jtiClaim != null && !string.IsNullOrWhiteSpace(jtiClaim.Value))
            {
                var expClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Exp);
                var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

                if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
                {
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                }

                // TODO: Extract replay prevention to a separate service to avoid circular dependency
                // For now, we'll rely on the token's expiration for protection
                _logger.LogDebug("JWT token has jti claim: {Jti} - replay protection temporarily disabled pending refactor", jtiClaim.Value);
            }
            else
            {
                _logger.LogDebug("JWT token has no jti claim - replay protection not available");
            }

            // Extract user data from JWT claims
            var userData = _claimNormalizer.NormalizeClaimsPrincipal(claimsPrincipal);

            // Cache the validated token data
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

        // If not cached, validate the token first
        var validationResult = await ValidateTokenAsync(token, cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result.Failure<ClaimsPrincipal, Error>(validationResult.Error);
        }

        // Get from cache after validation
        if (_cache.TryGetValue<CachedTokenData>(cacheKey, out var freshCachedData) && freshCachedData != null)
        {
            return Result.Success<ClaimsPrincipal, Error>(freshCachedData.Principal);
        }

        return Result.Failure<ClaimsPrincipal, Error>(
            Error.External("Failed to retrieve validated claims", "AUTH.CACHE_ERROR"));
    }

    private Task<Result<ClaimsPrincipal, Error>> ValidateJwtTokenEnhancedAsync(
        string token,
        ICollection<SecurityKey> securityKeys,
        string? partnerApiKey,
        CancellationToken _ = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            if (!tokenHandler.CanReadToken(token))
            {
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation("Invalid JWT token format", "AUTH.INVALID_TOKEN_FORMAT")));
            }

            // Read token to check issuer format and kid for rotation support
            var jwtToken = tokenHandler.ReadJwtToken(token);

            // Validate issuer format: app.dynamicauth.com/{environmentId} (AC3)
            var issuer = jwtToken.Issuer;
            if (!issuer.StartsWith("app.dynamicauth.com/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation($"Invalid issuer format: {issuer}", "AUTH.INVALID_ISSUER_FORMAT")));
            }

            // Extract and validate environmentId
            var environmentId = issuer.Substring("app.dynamicauth.com/".Length);
            if (!_validationOptions.EnvironmentMapping.TryGetValue(environmentId, out var mappedEnvironment))
            {
                _logger.LogWarning("Unknown Dynamic environmentId: {EnvironmentId}", environmentId);
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation($"Unknown Dynamic environmentId: {environmentId}", "AUTH.UNKNOWN_ENVIRONMENT")));
            }

            // Check kid for rotation support (AC3)
            var kid = jwtToken.Header.Kid;
            if (string.IsNullOrEmpty(kid))
            {
                _logger.LogWarning("JWT missing kid header for rotation tracking");
            }

            // Setup validation parameters with enhanced security
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = _validationOptions.ValidateAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = securityKeys,
                ClockSkew = TimeSpan.FromSeconds(_validationOptions.ClockSkewSeconds), // ±60 seconds max (AC3)
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role"
            };

            // Configure audience validation if enabled (AC3)
            if (_validationOptions.ValidateAudience)
            {
                var allowedAudiences = GetAllowedAudiences(partnerApiKey, jwtToken.Audiences);
                if (allowedAudiences.IsFailure)
                {
                    return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(allowedAudiences.Error));
                }

                validationParameters.ValidAudiences = allowedAudiences.Value;
            }

            // Disable claim mapping to preserve original JWT claims
            tokenHandler.MapInboundClaims = false;

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Ensure RS256 algorithm
            if (validatedToken is JwtSecurityToken validated &&
                !validated.Header.Alg.Equals(SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                    Error.Validation("Token must be signed with RS256", "AUTH.INVALID_ALGORITHM")));
            }

            _logger.LogDebug("JWT validated successfully with environment: {Environment}, Kid: {Kid}",
                mappedEnvironment, kid);

            return Task.FromResult(Result.Success<ClaimsPrincipal, Error>(principal));
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("JWT token has expired");
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED")));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("JWT token has invalid signature");
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized("Token has invalid signature", "AUTH.INVALID_SIGNATURE")));
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            _logger.LogWarning("JWT token audience validation failed: {Audience}", ex.InvalidAudience);
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized($"Invalid audience '{ex.InvalidAudience}' for this API key", "AUTH.INVALID_AUDIENCE")));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogError(ex, "JWT token validation failed");
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.Unauthorized($"Token validation failed: {ex.Message}", "AUTH.VALIDATION_FAILED")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT token validation");
            return Task.FromResult(Result.Failure<ClaimsPrincipal, Error>(
                Error.External("Unexpected validation error", "AUTH.VALIDATION_ERROR", ex)));
        }
    }

    private Result<IEnumerable<string>, Error> GetAllowedAudiences(string? partnerApiKey, IEnumerable<string> tokenAudiences)
    {
        var allowedAudiences = new List<string>();

        // Get partner-specific audience allowlist if API key provided
        if (!string.IsNullOrEmpty(partnerApiKey) &&
            _validationOptions.PartnerAudienceAllowlist.TryGetValue(partnerApiKey, out var partnerAudiences))
        {
            allowedAudiences.AddRange(partnerAudiences);
        }
        else
        {
            // Use default allowed audiences
            allowedAudiences.AddRange(_validationOptions.DefaultAllowedAudiences);
        }

        if (allowedAudiences.Count == 0)
        {
            _logger.LogWarning("No allowed audiences configured for partner: {PartnerApiKey}", partnerApiKey ?? "default");
            return Result.Failure<IEnumerable<string>, Error>(
                Error.Configuration("No allowed audiences configured", "AUTH.NO_ALLOWED_AUDIENCES"));
        }

        // Log if token contains audience not in allowlist
        var tokenAudienceList = tokenAudiences.ToList();
        var invalidAudiences = tokenAudienceList.Except(allowedAudiences).ToList();
        if (invalidAudiences.Count > 0)
        {
            _logger.LogWarning("Token contains audiences not in allowlist: {InvalidAudiences}",
                string.Join(", ", invalidAudiences));
        }

        return Result.Success<IEnumerable<string>, Error>(allowedAudiences);
    }

    private async Task<Result<ICollection<SecurityKey>, Error>> GetCachedJwksKeysAsync(CancellationToken cancellationToken)
    {
        // Try to get from cache first
        if (_cache.TryGetValue<ICollection<SecurityKey>>(JWKS_CACHE_KEY, out var cachedKeys) && cachedKeys != null)
        {
            _logger.LogDebug("Using cached JWKS keys ({Count} keys)", cachedKeys.Count);
            return Result.Success<ICollection<SecurityKey>, Error>(cachedKeys);
        }

        // Fetch from service if not cached
        var keysResult = await _jwksService.GetJwksKeysAsync(cancellationToken);
        if (keysResult.IsFailure)
        {
            return keysResult;
        }

        // Cache with configured duration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_validationOptions.JwksCacheMinutes)
        };
        _cache.Set(JWKS_CACHE_KEY, keysResult.Value, cacheOptions);

        _logger.LogInformation("JWKS keys cached successfully ({Count} keys)", keysResult.Value.Count);
        return keysResult;
    }

    private void RefreshJwksCache(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Background JWKS cache refresh started");
                var keysResult = await _jwksService.GetJwksKeysAsync(CancellationToken.None);
                if (keysResult.IsSuccess)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_validationOptions.JwksCacheMinutes)
                    };
                    _cache.Set(JWKS_CACHE_KEY, keysResult.Value, cacheOptions);
                    _logger.LogDebug("Background JWKS cache refresh completed ({Count} keys)", keysResult.Value.Count);
                }
                else
                {
                    _logger.LogWarning("Background JWKS cache refresh failed: {Error}", keysResult.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background JWKS cache refresh");
            }
        });
    }

    private static string GetTokenHash(string token)
    {
        // Create a hash of the token for cache key
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public void Dispose()
    {
        _backgroundRefreshTimer?.Dispose();
    }
}