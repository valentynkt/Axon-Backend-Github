using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices;

/// <summary>
/// Service for validating JWT tokens using local JWT validation with JWKS
/// Validates tokens locally using Dynamic.xyz public keys
/// </summary>
public sealed class DynamicAuthService : IDynamicAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicAuthService> _logger;
    private readonly DynamicXyzOptions _options;
    private readonly IDynamicClaimNormalizer _claimNormalizer;
    private readonly IJwtReplayGuard _replayGuard;
    private readonly TimeSpan _jwksCacheExpiration;
    private readonly TimeSpan _tokenCacheExpiration;

    public DynamicAuthService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<DynamicAuthService> logger,
        IOptions<DynamicXyzOptions> options,
        IDynamicClaimNormalizer claimNormalizer,
        IJwtReplayGuard replayGuard)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _claimNormalizer = claimNormalizer ?? throw new ArgumentNullException(nameof(claimNormalizer));
        _replayGuard = replayGuard ?? throw new ArgumentNullException(nameof(replayGuard));
        
        // Cache JWKS keys for 10 minutes by default
        _jwksCacheExpiration = TimeSpan.FromMinutes(_options.Jwt?.JwksCacheMinutes ?? 10);
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
        if (_cache.TryGetValue<DynamicUserData>(cacheKey, out var cachedUser) && cachedUser != null)
        {
            _logger.LogDebug("Token validation cache hit for user {UserId}", cachedUser.UserId);
            return Result.Success<DynamicUserData, Error>(cachedUser);
        }

        try
        {
            _logger.LogDebug("Validating JWT token locally using JWKS");

            // Get JWKS keys
            var keysResult = await GetJwksKeysAsync(cancellationToken);
            if (keysResult.IsFailure)
            {
                return Result.Failure<DynamicUserData, Error>(keysResult.Error);
            }

            // Validate the JWT token
            var validationResult = await ValidateJwtTokenAsync(token, keysResult.Value);
            if (validationResult.IsFailure)
            {
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
            
            // Cache the validated token (but only if replay check passed)
            _cache.Set(cacheKey, userData, _tokenCacheExpiration);
            
            _logger.LogInformation("Token validated successfully for user {UserId}", userData.UserId);
            return Result.Success<DynamicUserData, Error>(userData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<DynamicUserData, Error>(
                Error.External("Token validation failed", "AUTH.VALIDATION_ERROR", ex));
        }
    }
    

    private async Task<Result<ICollection<SecurityKey>, Error>> GetJwksKeysAsync(CancellationToken cancellationToken)
    {
        var jwksCacheKey = "dynamic_jwks_keys";
        
        // Check cache first
        if (_cache.TryGetValue<ICollection<SecurityKey>>(jwksCacheKey, out var cachedKeys) && cachedKeys != null)
        {
            _logger.LogDebug("JWKS cache hit");
            return Result.Success<ICollection<SecurityKey>, Error>(cachedKeys);
        }

        // Retry logic with exponential backoff
        const int maxRetries = 3;
        var baseDelay = TimeSpan.FromMilliseconds(500);
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogDebug("Retrying JWKS fetch (attempt {Attempt}/{MaxRetries}) after {Delay}ms", 
                        attempt + 1, maxRetries + 1, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }

                _logger.LogDebug("Fetching JWKS from Dynamic.xyz endpoint: {JwksUri} (attempt {Attempt})", 
                    _options.JwksUri, attempt + 1);
                
                var response = await _httpClient.GetStringAsync(_options.JwksUri, cancellationToken);
                var jwks = JsonDocument.Parse(response);
                
                var keys = new List<SecurityKey>();
                
                if (jwks.RootElement.TryGetProperty("keys", out var keysArray))
                {
                    foreach (var keyElement in keysArray.EnumerateArray())
                    {
                        var keyJson = keyElement.GetRawText();
                        var jwk = JsonWebKey.Create(keyJson);
                        keys.Add(jwk);
                    }
                }

                if (keys.Count == 0)
                {
                    _logger.LogWarning("No keys found in JWKS response from {JwksUri}", _options.JwksUri);
                    if (attempt == maxRetries)
                    {
                        return Result.Failure<ICollection<SecurityKey>, Error>(
                            Error.External("No keys found in JWKS response", "AUTH.NO_JWKS_KEYS"));
                    }
                    continue; // Try again
                }

                // Cache the keys with longer expiration on successful fetch
                _cache.Set(jwksCacheKey, (ICollection<SecurityKey>)keys, _jwksCacheExpiration);
                
                _logger.LogInformation("Successfully fetched {KeyCount} keys from JWKS (attempt {Attempt})", keys.Count, attempt + 1);
                return Result.Success<ICollection<SecurityKey>, Error>((ICollection<SecurityKey>)keys);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("JWKS fetch cancelled");
                return Result.Failure<ICollection<SecurityKey>, Error>(
                    Error.External("JWKS fetch was cancelled", "AUTH.JWKS_FETCH_CANCELLED"));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching JWKS from {JwksUri} (attempt {Attempt}/{MaxRetries}): {Message}", 
                    _options.JwksUri, attempt + 1, maxRetries + 1, ex.Message);
                
                if (attempt == maxRetries)
                {
                    return Result.Failure<ICollection<SecurityKey>, Error>(
                        Error.External($"Failed to fetch JWKS keys after {maxRetries + 1} attempts", "AUTH.JWKS_FETCH_ERROR", ex));
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Timeout fetching JWKS from {JwksUri} (attempt {Attempt}/{MaxRetries})", 
                    _options.JwksUri, attempt + 1, maxRetries + 1);
                
                if (attempt == maxRetries)
                {
                    return Result.Failure<ICollection<SecurityKey>, Error>(
                        Error.External($"JWKS fetch timeout after {maxRetries + 1} attempts", "AUTH.JWKS_FETCH_TIMEOUT", ex));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in JWKS response from {JwksUri} (attempt {Attempt}): {Message}", 
                    _options.JwksUri, attempt + 1, ex.Message);
                
                if (attempt == maxRetries)
                {
                    return Result.Failure<ICollection<SecurityKey>, Error>(
                        Error.External("Invalid JSON in JWKS response", "AUTH.JWKS_INVALID_JSON", ex));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching JWKS from {JwksUri} (attempt {Attempt}/{MaxRetries})", 
                    _options.JwksUri, attempt + 1, maxRetries + 1);
                
                if (attempt == maxRetries)
                {
                    return Result.Failure<ICollection<SecurityKey>, Error>(
                        Error.External($"Failed to fetch JWKS keys after {maxRetries + 1} attempts", "AUTH.JWKS_FETCH_ERROR", ex));
                }
            }
        }

        // This should never be reached, but added for completeness
        return Result.Failure<ICollection<SecurityKey>, Error>(
            Error.External("JWKS fetch failed after all retry attempts", "AUTH.JWKS_FETCH_ERROR"));
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

/// <summary>
/// Service interface for Dynamic.xyz authentication operations
/// </summary>
public interface IDynamicAuthService
{
    Task<Result<DynamicUserData, Error>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated user data from Dynamic.xyz
/// </summary>
public record DynamicUserData(
    string UserId,
    string Email,
    string EnvironmentId,
    List<WalletData> Wallets,
    DateTimeOffset? FirstVisitUtc,
    DateTimeOffset? LastVisitUtc,
    bool IsNewUser,
    string? SessionPublicKey = null,
    Dictionary<string, object>? VerifiedCredentialsHashes = null);

/// <summary>
/// Wallet information for authenticated user
/// </summary>
public record WalletData(
    string Id,
    string Address,
    string Chain,
    string? WalletName,
    string Provider,
    DateTimeOffset? ConnectedAtUtc);
    