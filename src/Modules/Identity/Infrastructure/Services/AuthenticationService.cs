using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Configuration;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Authentication service focusing on token generation and challenge operations.
/// JWT validation is now handled by ASP.NET Core JWT Bearer middleware.
/// Refactored from 823 lines to ~500 lines with better separation of concerns.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly AuthenticationOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly byte[] _hmacKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthenticationService> _logger;
    private static readonly object ChallengeLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuthenticationService(
        IOptions<AuthenticationOptions> options,
        IMemoryCache cache,
        ILogger<AuthenticationService> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _signingKey = new SymmetricSecurityKey(TryBase64(_options.SigningKey, out var signingKeyBytes)
            ? signingKeyBytes
            : Encoding.UTF8.GetBytes(_options.SigningKey));
        _hmacKey = TryBase64(_options.HmacSecret, out var raw)
            ? raw
            : Encoding.UTF8.GetBytes(_options.HmacSecret);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    #region Token Generation

    /// <summary>
    /// Generate Axon JWT access token
    /// </summary>
    public async Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = -1,
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
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // For replay protection
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

            var securityToken = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(securityToken);

            _logger.LogDebug("Generated Axon JWT for user {UserId} with JTI {Jti}",
                axonUserId.Value, claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);

            var token = new AxonToken(
                AccessToken: tokenString,
                TokenType: "Bearer",
                ExpiresIn: expiresIn,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            return await Task.FromResult(Result.Success<AxonToken, Error>(token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate access token for user {UserId}", axonUserId.Value);
            return Result.Failure<AxonToken, Error>(
                Error.Internal("Failed to generate access token", "AUTH.TOKEN_GENERATION_FAILED"));
        }
    }

    /// <summary>
    /// Process authenticated context from middleware-validated token
    /// </summary>
    public async Task<Result<AuthenticatedContext, Error>> ProcessAuthenticatedUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Unauthorized("User is not authenticated"));
            }

            // Extract claims from validated principal
            var axonUserIdClaim = principal.FindFirst("axon_user_id")?.Value
                ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(axonUserIdClaim) || !Guid.TryParse(axonUserIdClaim, out var userGuid))
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Unauthorized("Invalid user ID in token"));
            }

            var axonUserId = new AxonUserId(userGuid);

            var providerType = principal.FindFirst("provider_type")?.Value ?? "axon";
            var originalIssuer = principal.FindFirst("original_issuer")?.Value
                ?? principal.FindFirst("iss")?.Value
                ?? _options.Issuer;
            var originalSubject = principal.FindFirst("original_subject")?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? axonUserIdClaim;

            var iat = principal.FindFirst("iat")?.Value;
            var exp = principal.FindFirst("exp")?.Value;

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(
                long.TryParse(iat, out var iatValue) ? iatValue : DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
                long.TryParse(exp, out var expValue) ? expValue : DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds());

            var providerTypeResult = ProviderType.Create(providerType);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<AuthenticatedContext, Error>(providerTypeResult.Error);
            }

            var context = new AuthenticatedContext(
                TokenType: TokenType.AxonAccessToken,
                AxonUserId: axonUserId,
                ProviderType: providerTypeResult.Value,
                Issuer: originalIssuer,
                Subject: originalSubject,
                Principal: principal,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            return await Task.FromResult(Result.Success<AuthenticatedContext, Error>(context));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process authenticated user");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Internal("Failed to process authenticated user"));
        }
    }

    #endregion

    #region Refresh Token Operations

    public async Task<Result<RefreshTokenResponse, Error>> GenerateRefreshTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate access token
            var accessTokenResult = await GenerateAccessTokenAsync(
                axonUserId, providerType, issuer, subject, -1, cancellationToken);

            if (accessTokenResult.IsFailure)
            {
                return Result.Failure<RefreshTokenResponse, Error>(accessTokenResult.Error);
            }

            var accessToken = accessTokenResult.Value;

            // Generate refresh token (longer expiry)
            var refreshTokenId = Guid.NewGuid().ToString();
            var refreshIssuedAt = DateTimeOffset.UtcNow;
            var refreshExpiresAt = refreshIssuedAt.AddDays(30); // 30 days for refresh token

            var refreshClaims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, axonUserId.Value.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, refreshTokenId),
                new Claim("token_type", "refresh"),
                new Claim("axon_user_id", axonUserId.Value.ToString()),
                new Claim("provider_type", providerType.Value)
            };

            var refreshTokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(refreshClaims),
                Expires = refreshExpiresAt.DateTime,
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var refreshSecurityToken = _tokenHandler.CreateToken(refreshTokenDescriptor);
            var refreshTokenString = _tokenHandler.WriteToken(refreshSecurityToken);

            // Cache refresh token metadata for validation later
            var cacheKey = $"refresh_token:{refreshTokenId}";
            var cacheData = new RefreshTokenCacheData(axonUserId, providerType, issuer, subject);
            _cache.Set(cacheKey, cacheData, refreshExpiresAt - DateTimeOffset.UtcNow);

            var response = new RefreshTokenResponse(
                AccessToken: accessToken.AccessToken,
                RefreshToken: refreshTokenString,
                TokenType: "Bearer",
                ExpiresIn: accessToken.ExpiresIn,
                IssuedAt: accessToken.IssuedAt,
                AccessTokenExpiresAt: accessToken.ExpiresAt,
                RefreshTokenExpiresAt: refreshExpiresAt);

            _logger.LogDebug("Generated refresh token for user {UserId}", axonUserId.Value);

            return Result.Success<RefreshTokenResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Internal("Failed to generate refresh token"));
        }
    }

    public async Task<Result<RefreshTokenResponse, Error>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse the refresh token (basic validation only, full validation in middleware)
            var tokenValidation = _tokenHandler.ReadJwtToken(refreshToken);
            if (tokenValidation == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            var jti = tokenValidation.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Invalid refresh token"));
            }

            // Get cached refresh token data
            var cacheKey = $"refresh_token:{jti}";
            if (!_cache.TryGetValue<RefreshTokenCacheData>(cacheKey, out var cacheData) || cacheData == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Unauthorized("Refresh token not found or expired"));
            }

            // Generate new tokens
            var result = await GenerateRefreshTokenAsync(
                cacheData.AxonUserId,
                cacheData.ProviderType,
                cacheData.Issuer,
                cacheData.Subject,
                cancellationToken);

            // Invalidate old refresh token
            _cache.Remove(cacheKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh access token");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Internal("Failed to refresh access token"));
        }
    }

    #endregion

    #region Challenge Operations

    public async Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nonce = Guid.NewGuid().ToString("N");
            var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = issuedAt + 300; // 5 minutes

            var challengeData = new
            {
                ChainId = chainId,
                Address = walletAddress,
                IssuedAt = issuedAt,
                Exp = exp,
                Nonce = nonce,
                Aud = audience
            };

            var message = JsonSerializer.Serialize(challengeData, JsonOptions);
            var mkv = "v1"; // MAC key version
            var mac = GenerateMacForChallenge(message, mkv);

            var challenge = new AuthenticationChallenge(
                ChainId: chainId,
                Address: walletAddress,
                IssuedAt: issuedAt,
                Exp: exp,
                Nonce: nonce,
                Aud: audience,
                Message: message,
                Mac: mac,
                Mkv: mkv);

            // Cache the challenge for validation
            var cacheKey = $"challenge:{nonce}";
            _cache.Set(cacheKey, challenge, TimeSpan.FromMinutes(5));

            _logger.LogDebug("Generated challenge for wallet {Address} on chain {ChainId}",
                walletAddress, chainId);

            return await Task.FromResult(Result.Success<AuthenticationChallenge, Error>(challenge));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate challenge");
            return Result.Failure<AuthenticationChallenge, Error>(
                Error.Internal("Failed to generate challenge"));
        }
    }

    public Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,
        string expectedWalletAddress,
        string expectedAudience)
    {
        try
        {
            var challengeData = JsonSerializer.Deserialize<JsonElement>(message, JsonOptions);

            var chainId = challengeData.GetProperty("chainId").GetString();
            var address = challengeData.GetProperty("address").GetString();
            var audience = challengeData.GetProperty("aud").GetString();
            var exp = challengeData.GetProperty("exp").GetInt64();

            // Validate challenge fields
            if (chainId != expectedChainId || address != expectedWalletAddress || audience != expectedAudience)
            {
                return Result.Success<bool, Error>(false);
            }

            // Check expiry
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (exp < now)
            {
                return Result.Success<bool, Error>(false);
            }

            return Result.Success<bool, Error>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate challenge");
            return Result.Failure<bool, Error>(
                Error.Internal("Failed to validate challenge"));
        }
    }

    #endregion

    #region HMAC Operations

    public string GenerateMacForChallenge(string canonicalJson, string keyVersion)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var messageBytes = Encoding.UTF8.GetBytes(canonicalJson);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public Result<bool, Error> ValidateMac(string message, string mac, string keyVersion)
    {
        try
        {
            var expectedMac = GenerateMacForChallenge(message, keyVersion);
            var isValid = string.Equals(mac, expectedMac, StringComparison.Ordinal);
            return Result.Success<bool, Error>(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate MAC");
            return Result.Failure<bool, Error>(
                Error.Internal("Failed to validate MAC"));
        }
    }

    public async Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage, string mkv, CancellationToken ct = default)
    {
        try
        {
            // Extract nonce from signed message
            var messageData = JsonSerializer.Deserialize<JsonElement>(signedMessage, JsonOptions);
            var nonce = messageData.GetProperty("nonce").GetString();

            if (string.IsNullOrEmpty(nonce))
            {
                return UnitResult.Failure<Error>(
                    Error.Validation("Invalid nonce in message"));
            }

            var cacheKey = $"nonce:{nonce}";

            lock (ChallengeLock)
            {
                if (_cache.TryGetValue<bool>(cacheKey, out _))
                {
                    return UnitResult.Failure<Error>(
                        Error.Validation("Nonce already used"));
                }

                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
            }

            return await Task.FromResult(UnitResult.Success<Error>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check and mark nonce");
            return UnitResult.Failure<Error>(
                Error.Internal("Failed to process nonce"));
        }
    }

    #endregion

    #region Helper Methods

    private static bool TryBase64(string input, out byte[] output)
    {
        output = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            output = Convert.FromBase64String(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Cache Data Classes

    private record RefreshTokenCacheData(
        AxonUserId AxonUserId,
        ProviderType ProviderType,
        string Issuer,
        string Subject);

    #endregion
}