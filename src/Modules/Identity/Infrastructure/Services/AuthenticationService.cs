using System.Buffers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Unified authentication service that consolidates functionality from:
/// - AxonJwtService
/// - UnifiedBearerTokenValidator
/// - CanonicalMessageService
/// - MemoryJwtReplayGuard
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly AuthenticationOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly byte[] _hmacKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly IMemoryCache _cache;
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IExchangeMetricsService _metricsService;
    private readonly ILogger<AuthenticationService> _logger;
    private static readonly object ReplayLock = new();

    public AuthenticationService(
        IOptions<AuthenticationOptions> options,
        IMemoryCache cache,
        IDynamicAuthService dynamicAuthService,
        IExchangeMetricsService metricsService,
        ILogger<AuthenticationService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _hmacKey = TryBase64(_options.HmacSecret, out var raw)
            ? raw
            : Encoding.UTF8.GetBytes(_options.HmacSecret);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = 3600,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (expiresIn <= 0)
                expiresIn = _options.DefaultTokenExpirySeconds;

            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddSeconds(expiresIn);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, axonUserId.Value.ToString()),
                new Claim(JwtRegisteredClaimNames.Iss, _options.Issuer),
                new Claim(JwtRegisteredClaimNames.Aud, _options.Audience),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Exp, expiresAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("axon_user_id", axonUserId.Value.ToString()),
                new Claim("provider_type", providerType.Value),
                new Claim("original_issuer", issuer),
                new Claim("original_subject", subject)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt.DateTime,
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            _logger.LogDebug("Generated Axon JWT token for AxonUserId={AxonUserId}, Provider={Provider}, ExpiresIn={ExpiresIn}s",
                axonUserId.Value, providerType.Value, expiresIn);

            var result = new AxonToken(
                AccessToken: tokenString,
                TokenType: "Bearer",
                ExpiresIn: expiresIn,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            return Result.Success<AxonToken, Error>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Axon JWT token for AxonUserId={AxonUserId}", axonUserId.Value);
            return Result.Failure<AxonToken, Error>(
                Error.Internal($"Failed to generate access token: {ex.Message}", AuthErrors.TokenGenerationFailed));
        }
    }

    public async Task<Result<AuthenticatedContext, Error>> ValidateTokenAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Unauthorized("Bearer token is required", AuthErrors.TokenRequired));
        }

        try
        {
            var tokenType = DetermineTokenType(bearerToken);
            _logger.LogDebug("Detected token type: {TokenType}", tokenType);

            return tokenType switch
            {
                TokenType.DynamicJwt => await ValidateDynamicTokenAsync(bearerToken, cancellationToken),
                TokenType.AxonAccessToken => await ValidateAxonTokenAsync(bearerToken, cancellationToken),
                _ => Result.Failure<AuthenticatedContext, Error>(
                    Error.Unauthorized("Unsupported token type", AuthErrors.TokenUnsupportedType))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Internal($"Token validation failed: {ex.Message}", AuthErrors.ValidationError));
        }
    }

    public Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        NetworkEnvironment networkEnvironment,
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default)
    {
        var chainIdVo = ChainId.Create(chainId);
        if (chainIdVo.IsFailure)
            return Task.FromResult(Result.Failure<AuthenticationChallenge, Error>(chainIdVo.Error));

        var addressVo = Address.Create(walletAddress);
        if (addressVo.IsFailure)
            return Task.FromResult(Result.Failure<AuthenticationChallenge, Error>(addressVo.Error));

        if (string.IsNullOrWhiteSpace(audience))
            audience = _options.DefaultAudience;

        var now = DateTimeOffset.UtcNow;
        var issuedAt = now.ToUnixTimeSeconds();
        var expiration = now.AddSeconds(_options.MaxTtlSeconds).ToUnixTimeSeconds();
        var nonce = GenerateBase64UrlNonce(32);

        var message = BuildChallengeMessage(
            networkEnvironment.Value,
            chainIdVo.Value.Value,
            addressVo.Value.Value,
            issuedAt,
            expiration,
            nonce,
            audience);

        var result = new AuthenticationChallenge(
            NetworkEnvironment: networkEnvironment.Value,
            ChainId: chainIdVo.Value.Value,
            Address: addressVo.Value.Value,
            IssuedAt: issuedAt,
            Exp: expiration,
            Nonce: nonce,
            Aud: audience,
            Message: message);

        return Task.FromResult(Result.Success<AuthenticationChallenge, Error>(result));
    }

    public Result<bool, Error> ValidateChallengeAsync(
        string message,
        NetworkEnvironment expectedNetworkEnvironment,
        string expectedChainId,
        string expectedWalletAddress,
        string expectedAudience)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Result.Failure<bool, Error>(Error.Validation("Message cannot be empty", AuthErrors.ChallengeRequired));

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!TryGetString(root, "network_environment", out var env) ||
                !TryGetString(root, "chain_id", out var chain) ||
                !TryGetString(root, "address", out var address) ||
                !TryGetInt64(root, "issued_at", out var iat) ||
                !TryGetInt64(root, "exp", out var exp) ||
                !TryGetString(root, "nonce", out var nonce) ||
                !TryGetString(root, "aud", out var aud))
            {
                return Result.Failure<bool, Error>(Error.Validation("Message missing required fields", AuthErrors.ChallengeMissingFields));
            }

            if (!string.Equals(env, expectedNetworkEnvironment.Value, StringComparison.Ordinal))
                return Fail($"Network environment mismatch: expected {expectedNetworkEnvironment.Value}, got {env}", AuthErrors.ChallengeNetworkMismatch);

            if (!string.Equals(chain, expectedChainId, StringComparison.Ordinal))
                return Fail($"Chain ID mismatch: expected {expectedChainId}, got {chain}", AuthErrors.ChallengeChainMismatch);

            if (!string.Equals(address, expectedWalletAddress, StringComparison.Ordinal))
                return Fail($"Address mismatch: expected {expectedWalletAddress}, got {address}", AuthErrors.ChallengeAddressMismatch);

            if (!string.Equals(aud, expectedAudience, StringComparison.Ordinal))
                return Fail($"Audience mismatch: expected {expectedAudience}, got {aud}", AuthErrors.ChallengeAudienceMismatch);

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
            var expiration = DateTimeOffset.FromUnixTimeSeconds(exp);
            var ttlResult = ValidateTtl(issuedAt, expiration);
            if (ttlResult.IsFailure) return ttlResult;

            var now = DateTimeOffset.UtcNow;
            if (now < issuedAt.AddSeconds(-_options.ClockSkewSeconds))
                return Fail("Message not yet valid (issued in future)", AuthErrors.ChallengeNotYetValid);
            if (now > expiration.AddSeconds(_options.ClockSkewSeconds))
                return Fail("Message has expired", AuthErrors.ChallengeExpired);

            var chainIdVo = ChainId.Create(chain);
            if (chainIdVo.IsFailure) return Result.Failure<bool, Error>(chainIdVo.Error);
            var addrVo = Address.Create(address);
            if (addrVo.IsFailure) return Result.Failure<bool, Error>(addrVo.Error);

            return Result.Success<bool, Error>(true);
        }
        catch (JsonException)
        {
            return Result.Failure<bool, Error>(Error.Validation("Invalid JSON format", AuthErrors.ChallengeInvalidJson));
        }
        catch (Exception ex)
        {
            return Result.Failure<bool, Error>(Error.Internal($"Error validating challenge: {ex.Message}", AuthErrors.ChallengeValidationError));
        }
    }

    public Task<Result<Unit, Error>> CheckAndMarkTokenUsedAsync(
        string jti,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            _logger.LogWarning("JWT replay check attempted with empty jti");
            return Task.FromResult(Result.Failure<Unit, Error>(
                Error.Validation("JWT ID (jti) is required for replay protection", AuthErrors.TokenRequired)));
        }

        try
        {
            var cacheKey = $"jwt_used_{jti}";

            lock (ReplayLock)
            {
                if (_cache.TryGetValue(cacheKey, out _))
                {
                    _metricsService.RecordReplayAttempt(jti);
                    _logger.LogWarning("JWT replay attempt detected for jti: {Jti}", jti);
                    return Task.FromResult(Result.Failure<Unit, Error>(
                        Error.Unauthorized("JWT token has already been used", AuthErrors.TokenReplayed)));
                }

                var cacheExpiration = expiresAt.Subtract(DateTimeOffset.UtcNow);

                if (cacheExpiration <= TimeSpan.Zero)
                {
                    _logger.LogWarning("Attempt to cache expired JWT with jti: {Jti}", jti);
                    return Task.FromResult(Result.Failure<Unit, Error>(
                        Error.Unauthorized("JWT token has expired", AuthErrors.TokenExpired)));
                }

                var bufferTime = TimeSpan.FromMinutes(_options.ReplayGuardBufferMinutes);
                var effectiveExpiration = cacheExpiration.Add(bufferTime);

                _cache.Set(cacheKey, true, effectiveExpiration);

                _logger.LogDebug("JWT jti marked as used: {Jti}, expires in: {Duration}", jti, effectiveExpiration);
                return Task.FromResult(Result.Success<Unit, Error>(Unit.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during JWT replay check for jti: {Jti}", jti);
            return Task.FromResult(Result.Failure<Unit, Error>(
                Error.External("JWT replay check failed", AuthErrors.ReplayCheckError, ex)));
        }
    }

    // Private helper methods

    private TokenType DetermineTokenType(string token)
    {
        try
        {
            if (!_tokenHandler.CanReadToken(token))
                return TokenType.DynamicJwt;

            var jsonToken = _tokenHandler.ReadJwtToken(token);
            var issuer = jsonToken.Issuer;

            if (!string.IsNullOrEmpty(issuer) && issuer.Contains("axon", StringComparison.OrdinalIgnoreCase))
                return TokenType.AxonAccessToken;

            if (!string.IsNullOrEmpty(issuer) && issuer.Contains("dynamic", StringComparison.OrdinalIgnoreCase))
                return TokenType.DynamicJwt;

            var axonUserIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "axon_user_id");
            if (axonUserIdClaim != null)
                return TokenType.AxonAccessToken;

            return TokenType.DynamicJwt;
        }
        catch
        {
            return TokenType.DynamicJwt;
        }
    }

    private async Task<Result<AuthenticatedContext, Error>> ValidateDynamicTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(token, cancellationToken);
        if (validationResult.IsFailure)
        {
            _logger.LogWarning("Dynamic JWT validation failed: {Error}", validationResult.Error.Message);
            return Result.Failure<AuthenticatedContext, Error>(validationResult.Error);
        }

        var dynamicUser = validationResult.Value;

        var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(token, cancellationToken);
        if (rawClaimsResult.IsFailure)
        {
            _logger.LogWarning("Failed to get raw claims from Dynamic JWT: {Error}", rawClaimsResult.Error.Message);
            return Result.Failure<AuthenticatedContext, Error>(rawClaimsResult.Error);
        }

        var principal = rawClaimsResult.Value;
        var issuer = principal.FindFirst("iss")?.Value ?? "unknown";
        var subject = principal.FindFirst("sub")?.Value ?? dynamicUser.AxonUserId;
        var iatClaim = principal.FindFirst("iat")?.Value;
        var expClaim = principal.FindFirst("exp")?.Value;

        var issuedAt = !string.IsNullOrEmpty(iatClaim) && long.TryParse(iatClaim, out var iat)
            ? DateTimeOffset.FromUnixTimeSeconds(iat)
            : DateTimeOffset.UtcNow;

        var expiresAt = !string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : DateTimeOffset.UtcNow.AddHours(1);

        if (!Guid.TryParse(dynamicUser.AxonUserId, out var axonUserIdGuid))
        {
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Validation("Invalid AxonUserId format in Dynamic token", AuthErrors.TokenInvalidAxonUserId));
        }

        var context = new AuthenticatedContext(
            TokenType: TokenType.DynamicJwt,
            AxonUserId: new AxonUserId(axonUserIdGuid),
            ProviderType: ProviderType.Create("dynamic").Value,
            Issuer: issuer,
            Subject: subject,
            Principal: principal,
            IssuedAt: issuedAt,
            ExpiresAt: expiresAt);

        _logger.LogDebug("Successfully validated Dynamic JWT for AxonUserId: {AxonUserId}", dynamicUser.AxonUserId);
        return Result.Success<AuthenticatedContext, Error>(context);
    }

    private async Task<Result<AuthenticatedContext, Error>> ValidateAxonTokenAsync(
        string token,
        CancellationToken _ = default)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = !string.IsNullOrEmpty(_options.Audience),
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
                NameClaimType = "sub"
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            var axonUserIdClaim = principal.FindFirst("axon_user_id")?.Value;
            var providerTypeClaim = principal.FindFirst("provider_type")?.Value;
            var originalIssuerClaim = principal.FindFirst("original_issuer")?.Value;
            var originalSubjectClaim = principal.FindFirst("original_subject")?.Value;
            var iatClaim = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
            var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            if (string.IsNullOrEmpty(axonUserIdClaim) ||
                string.IsNullOrEmpty(providerTypeClaim) ||
                string.IsNullOrEmpty(originalIssuerClaim) ||
                string.IsNullOrEmpty(originalSubjectClaim) ||
                string.IsNullOrEmpty(iatClaim) ||
                string.IsNullOrEmpty(expClaim))
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Validation("Token missing required claims", AuthErrors.TokenInvalidClaims));
            }

            if (!Guid.TryParse(axonUserIdClaim, out var axonUserIdGuid))
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Validation("Invalid AxonUserId format", AuthErrors.TokenInvalidAxonUserId));
            }

            var axonUserId = new AxonUserId(axonUserIdGuid);

            var providerTypeResult = ProviderType.Create(providerTypeClaim);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<AuthenticatedContext, Error>(providerTypeResult.Error);
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim));
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim));

            var context = new AuthenticatedContext(
                TokenType: TokenType.AxonAccessToken,
                AxonUserId: axonUserId,
                ProviderType: providerTypeResult.Value,
                Issuer: originalIssuerClaim,
                Subject: originalSubjectClaim,
                Principal: principal,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            _logger.LogDebug("Successfully validated Axon JWT token for AxonUserId={AxonUserId}", axonUserId.Value);
            return Result.Success<AuthenticatedContext, Error>(context);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Axon JWT token validation failed: Token expired");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Unauthorized("Token has expired", AuthErrors.TokenExpired));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Axon JWT token validation failed: Invalid signature");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Unauthorized("Token signature is invalid", AuthErrors.TokenInvalidSignature));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Axon JWT token validation failed: {Message}", ex.Message);
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Unauthorized($"Token validation failed: {ex.Message}", AuthErrors.TokenInvalid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Axon JWT token validation");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Internal($"Unexpected validation error: {ex.Message}", AuthErrors.ValidationError));
        }
    }

    public async Task<Result<RefreshTokenResponse, Error>> GenerateRefreshTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedAt = DateTimeOffset.UtcNow;
            var accessTokenExpiresIn = _options.DefaultTokenExpirySeconds; // Use config value
            var refreshTokenExpiresIn = _options.RefreshTokenExpirySeconds; // Use config value

            var accessTokenExpiresAt = issuedAt.AddSeconds(accessTokenExpiresIn);
            var refreshTokenExpiresAt = issuedAt.AddSeconds(refreshTokenExpiresIn);

            // Generate access token
            var accessTokenResult = await GenerateAccessTokenAsync(
                axonUserId, providerType, issuer, subject, accessTokenExpiresIn, cancellationToken);

            if (accessTokenResult.IsFailure)
                return Result.Failure<RefreshTokenResponse, Error>(accessTokenResult.Error);

            // Generate refresh token
            var refreshTokenId = Guid.NewGuid().ToString();
            var refreshTokenClaims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, axonUserId.Value.ToString()),
                new Claim(JwtRegisteredClaimNames.Iss, _options.Issuer),
                new Claim(JwtRegisteredClaimNames.Aud, $"{_options.Audience}:refresh"),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Exp, refreshTokenExpiresAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, refreshTokenId),
                new Claim("token_type", "refresh"),
                new Claim("axon_user_id", axonUserId.Value.ToString()),
                new Claim("provider_type", providerType.Value),
                new Claim("original_issuer", issuer),
                new Claim("original_subject", subject)
            };

            var refreshTokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(refreshTokenClaims),
                Expires = refreshTokenExpiresAt.DateTime,
                Issuer = _options.Issuer,
                Audience = $"{_options.Audience}:refresh",
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var refreshToken = _tokenHandler.CreateToken(refreshTokenDescriptor);
            var refreshTokenString = _tokenHandler.WriteToken(refreshToken);

            // Store refresh token for later validation (simple in-memory cache for now)
            var refreshTokenCacheKey = $"refresh_token_{refreshTokenId}";
            _cache.Set(refreshTokenCacheKey, axonUserId.Value, refreshTokenExpiresAt.Subtract(issuedAt));

            _logger.LogDebug("Generated refresh token for AxonUserId={AxonUserId}", axonUserId.Value);

            var response = new RefreshTokenResponse(
                AccessToken: accessTokenResult.Value.AccessToken,
                RefreshToken: refreshTokenString,
                TokenType: "Bearer",
                ExpiresIn: accessTokenExpiresIn,
                IssuedAt: issuedAt,
                AccessTokenExpiresAt: accessTokenExpiresAt,
                RefreshTokenExpiresAt: refreshTokenExpiresAt);

            return Result.Success<RefreshTokenResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token for AxonUserId={AxonUserId}", axonUserId.Value);
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Internal($"Failed to generate refresh token: {ex.Message}", AuthErrors.TokenGenerationFailed));
        }
    }

    public async Task<Result<RefreshTokenResponse, Error>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate the refresh token
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = $"{_options.Audience}:refresh",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds)
            };

            var principal = _tokenHandler.ValidateToken(refreshToken, validationParameters, out var validatedToken);

            // Extract claims
            var jtiClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var axonUserIdClaim = principal.FindFirst("axon_user_id")?.Value;
            var providerTypeClaim = principal.FindFirst("provider_type")?.Value;
            var originalIssuerClaim = principal.FindFirst("original_issuer")?.Value;
            var originalSubjectClaim = principal.FindFirst("original_subject")?.Value;

            if (string.IsNullOrEmpty(jtiClaim) || string.IsNullOrEmpty(axonUserIdClaim) ||
                string.IsNullOrEmpty(providerTypeClaim) || string.IsNullOrEmpty(originalIssuerClaim) ||
                string.IsNullOrEmpty(originalSubjectClaim))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Validation("Refresh token missing required claims", AuthErrors.TokenInvalidClaims));
            }

            // Check if refresh token is in cache (not revoked)
            var refreshTokenCacheKey = $"refresh_token_{jtiClaim}";
            if (!_cache.TryGetValue(refreshTokenCacheKey, out var cachedUserId))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Refresh token is invalid or revoked", AuthErrors.TokenInvalid));
            }

            if (!Guid.TryParse(axonUserIdClaim, out var axonUserIdGuid))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Validation("Invalid AxonUserId format", AuthErrors.TokenInvalidAxonUserId));
            }

            var axonUserId = new AxonUserId(axonUserIdGuid);
            var providerTypeResult = ProviderType.Create(providerTypeClaim);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Validation("Invalid provider type in refresh token", AuthErrors.TokenInvalidClaims));
            }
            var providerType = providerTypeResult.Value;

            // Revoke the old refresh token
            _cache.Remove(refreshTokenCacheKey);

            // Generate new token pair
            return await GenerateRefreshTokenAsync(
                axonUserId, providerType, originalIssuerClaim, originalSubjectClaim, cancellationToken);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Invalid refresh token provided");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Unauthorized("Invalid refresh token", AuthErrors.TokenInvalid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh access token");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Internal($"Failed to refresh token: {ex.Message}", AuthErrors.TokenRefreshFailed));
        }
    }

    private Result<bool, Error> ValidateTtl(DateTimeOffset issuedAt, DateTimeOffset expiration)
    {
        var ttlSeconds = (expiration - issuedAt).TotalSeconds;
        if (ttlSeconds > _options.MaxTtlSeconds)
            return Fail($"TTL exceeds maximum: {ttlSeconds}s > {_options.MaxTtlSeconds}s", AuthErrors.ChallengeTtlExceeded);
        if (ttlSeconds <= 0)
            return Fail("TTL must be positive", AuthErrors.ChallengeTtlInvalid);
        return Result.Success<bool, Error>(true);
    }

    private static bool TryBase64(string s, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(s); return true; }
        catch { bytes = Array.Empty<byte>(); return false; }
    }

    private static string GenerateBase64UrlNonce(int numBytes)
    {
        var bytes = RandomNumberGenerator.GetBytes(numBytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string BuildChallengeMessage(
        string env, string chain, string address, long issuedAt, long exp, string nonce, string aud)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("network_environment", env);
            writer.WriteString("chain_id", chain);
            writer.WriteString("address", address);
            writer.WriteNumber("issued_at", issuedAt);
            writer.WriteNumber("exp", exp);
            writer.WriteString("nonce", nonce);
            writer.WriteString("aud", aud);
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = default!;
        if (!root.TryGetProperty(name, out var p)) return false;
        if (p.ValueKind != JsonValueKind.String) return false;
        value = p.GetString()!;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt64(JsonElement root, string name, out long value)
    {
        value = default;
        if (!root.TryGetProperty(name, out var p)) return false;
        return p.TryGetInt64(out value);
    }

    private static Result<bool, Error> Fail(string msg, string code)
        => Result.Failure<bool, Error>(Error.Validation(msg, code));
}