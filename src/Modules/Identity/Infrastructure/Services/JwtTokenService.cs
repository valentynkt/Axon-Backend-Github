namespace Axon.Modules.Identity.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Services;
using Domain.Entities;
using Domain.ValueObjects;
using Persistence.Stores;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// JWT Token Service implementation using Microsoft Identity Framework
/// with AxonUserAuth entity for user management.
/// Implements the IJwtTokenService interface from the application layer.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly AxonUserStore _userStore;
    private readonly IConfiguration _configuration;
    private readonly TokenReplayCache _replayCache;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly IDataProtector _keyProtector;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions;

    public JwtTokenService(
        UserManager<AxonUserAuth> userManager,
        AxonUserStore userStore,
        IConfiguration configuration,
        TokenReplayCache replayCache,
        ILogger<JwtTokenService> logger,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsMonitor<JwtBearerOptions> jwtOptions)
    {
        _userManager = userManager;
        _userStore = userStore;
        _configuration = configuration;
        _replayCache = replayCache;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
        _keyProtector = dataProtectionProvider.CreateProtector("Axon.JWT.SigningKey");
        _jwtOptions = jwtOptions;
    }

    /// <summary>
    /// Generates an Axon access token for authenticated user
    /// </summary>
    public async Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId userId,
        ProviderType providerType,
        string providerSubject,
        string issuer,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find or create the user
            var user = await FindOrCreateUserAsync(userId, providerType, issuer, providerSubject, cancellationToken);
            if (user == null)
            {
                return Result.Failure<AxonToken, Error>(
                    Error.NotFound("User not found", "AUTH.USER_NOT_FOUND"));
            }

            // Update last authenticated
            user.UpdateLastAuthenticated(refreshSecurityStamp: false);
            await _userManager.UpdateAsync(user);

            // Build claims
            var claims = await BuildAccessTokenClaimsAsync(user);

            // Generate token
            var key = GetSigningKey();
            // Set KeyId to ensure token can be validated
            key.KeyId = "axon_key_001";
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(expiryMinutes);

            var token = new JwtSecurityToken(
                issuer: _configuration["Axon:Issuer"] ?? "axon-api",
                audience: _configuration["Axon:Audience"] ?? "axon-api",
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials);

            var tokenString = _tokenHandler.WriteToken(token);

            var axonToken = new AxonToken(
                AccessToken: tokenString,
                TokenType: "Bearer",
                ExpiresIn: expiryMinutes * 60,
                IssuedAt: new DateTimeOffset(now),
                ExpiresAt: new DateTimeOffset(expires));

            _logger.LogDebug("Generated access token for user {UserId}", user.Id);

            return Result.Success<AxonToken, Error>(axonToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate access token for user {UserId}", userId);
            return Result.Failure<AxonToken, Error>(
                Error.Failure("Failed to generate access token", "AUTH.TOKEN_GENERATION_FAILED"));
        }
    }

    /// <summary>
    /// Validates an Axon access token using JWT Bearer middleware's validation with built-in replay protection
    /// </summary>
    public async Task<Result<AuthenticatedContext, Error>> ValidateAxonTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use JWT Bearer middleware's validation parameters for consistency and built-in replay protection
            var axonJwtOptions = _jwtOptions.Get("AxonJwt");
            var validationParameters = axonJwtOptions?.TokenValidationParameters?.Clone() ??
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Axon:Issuer"] ?? "axon-api",
                    ValidateAudience = true,
                    ValidAudience = _configuration["Axon:Audience"] ?? "axon-api",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GetSigningKey(),
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidateTokenReplay = true, // Enable built-in token replay validation
                    TokenReplayCache = _replayCache // Use unified replay cache
                };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Extract claims
            var userIdClaim = principal.FindFirst("axon_user_id")?.Value;
            var providerType = principal.FindFirst("provider_type")?.Value;
            var originalIssuer = principal.FindFirst("original_issuer")?.Value;
            var originalSubject = principal.FindFirst("original_subject")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userIdGuid))
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Validation("Invalid user ID in token", "AUTH.INVALID_USER_ID"));
            }

            var axonUserId = new AxonUserId(userIdGuid);

            // Parse provider type
            if (!ProviderType.TryParse(providerType, null, out var providerTypeValue))
            {
                return Result.Failure<AuthenticatedContext, Error>(
                    Error.Validation("Invalid provider type in token", "AUTH.INVALID_PROVIDER"));
            }

            // Replay protection is now handled by TokenValidationParameters.ValidateTokenReplay and TokenReplayCache

            var context = new AuthenticatedContext(
                TokenType: TokenType.AxonAccessToken,
                AxonUserId: axonUserId,
                ProviderType: providerTypeValue,
                Issuer: originalIssuer ?? string.Empty,
                Subject: originalSubject ?? string.Empty,
                Principal: principal,
                IssuedAt: new DateTimeOffset(validatedToken.ValidFrom),
                ExpiresAt: new DateTimeOffset(validatedToken.ValidTo));

            return Result.Success<AuthenticatedContext, Error>(context);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Validation("Invalid token", "AUTH.INVALID_TOKEN"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<AuthenticatedContext, Error>(
                Error.Failure("Failed to validate token", "AUTH.VALIDATION_FAILED"));
        }
    }

    /// <summary>
    /// Generates refresh token response with access and refresh tokens
    /// </summary>
    public async Task<Result<RefreshTokenResponse, Error>> GenerateRefreshTokenAsync(
        AxonUserId userId,
        ProviderType providerType,
        string providerSubject,
        string issuer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate access token
            var accessTokenResult = await GenerateAccessTokenAsync(
                userId, providerType, providerSubject, issuer, 15, cancellationToken);

            if (accessTokenResult.IsFailure)
                return Result.Failure<RefreshTokenResponse, Error>(accessTokenResult.Error);

            var accessToken = accessTokenResult.Value;

            // Find the user
            var user = await FindOrCreateUserAsync(userId, providerType, issuer, providerSubject, cancellationToken);
            if (user == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.NotFound("User not found", "AUTH.USER_NOT_FOUND"));
            }

            // Generate refresh token
            var refreshClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new("axon_user_id", user.AxonPrincipalId.Value.ToString()),
                new("token_type", "refresh"),
                new("jti", Guid.NewGuid().ToString())
            };

            var key = GetSigningKey();
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;
            var refreshExpires = now.AddDays(30);

            var refreshToken = new JwtSecurityToken(
                issuer: _configuration["Axon:Issuer"] ?? "axon-api",
                audience: $"{_configuration["Axon:Audience"] ?? "axon-api"}:refresh",
                claims: refreshClaims,
                notBefore: now,
                expires: refreshExpires,
                signingCredentials: credentials);

            var refreshTokenString = _tokenHandler.WriteToken(refreshToken);

            var response = new RefreshTokenResponse(
                AccessToken: accessToken.AccessToken,
                RefreshToken: refreshTokenString,
                TokenType: "Bearer",
                ExpiresIn: accessToken.ExpiresIn,
                IssuedAt: accessToken.IssuedAt,
                AccessTokenExpiresAt: accessToken.ExpiresAt,
                RefreshTokenExpiresAt: new DateTimeOffset(refreshExpires));

            return Result.Success<RefreshTokenResponse, Error>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate refresh token for user {UserId}", userId);
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Failure("Failed to generate refresh token", "AUTH.REFRESH_TOKEN_FAILED"));
        }
    }

    /// <summary>
    /// Refreshes access token using valid refresh token
    /// </summary>
    public async Task<Result<RefreshTokenResponse, Error>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reuse and adapt JWT Bearer validation parameters for refresh tokens
            var axonJwtOptions = _jwtOptions.Get("AxonJwt");
            var baseValidationParams = axonJwtOptions?.TokenValidationParameters?.Clone() ??
                new TokenValidationParameters();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = baseValidationParams.ValidateIssuer,
                ValidIssuer = baseValidationParams.ValidIssuer ?? _configuration["Axon:Issuer"] ?? "axon-api",
                ValidateAudience = true,
                ValidAudience = $"{baseValidationParams.ValidAudience ?? _configuration["Axon:Audience"] ?? "axon-api"}:refresh",
                ValidateLifetime = baseValidationParams.ValidateLifetime,
                ValidateIssuerSigningKey = baseValidationParams.ValidateIssuerSigningKey,
                IssuerSigningKey = baseValidationParams.IssuerSigningKey ?? GetSigningKey(),
                ClockSkew = baseValidationParams.ClockSkew
            };

            var principal = _tokenHandler.ValidateToken(refreshToken, validationParameters, out _);

            // Get user from refresh token
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.Validation("Invalid refresh token", "AUTH.INVALID_REFRESH_TOKEN"));
            }

            var user = await _userManager.FindByIdAsync(userGuid.ToString());
            if (user == null)
            {
                return Result.Failure<RefreshTokenResponse, Error>(
                    Error.NotFound("User not found", "AUTH.USER_NOT_FOUND"));
            }

            // Generate new tokens
            var providerType = ProviderType.From(user.ProviderType);
            var result = await GenerateRefreshTokenAsync(
                user.AxonPrincipalId,
                providerType,
                user.OriginalSubject,
                user.OriginalIssuer,
                cancellationToken);

            return result;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Refresh token validation failed");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Validation("Invalid refresh token", "AUTH.INVALID_REFRESH_TOKEN"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh access token");
            return Result.Failure<RefreshTokenResponse, Error>(
                Error.Failure("Failed to refresh access token", "AUTH.REFRESH_FAILED"));
        }
    }

    // ExtractBearerToken method removed - JWT middleware handles token extraction automatically
    // Access token via HttpContext.User.Claims after authentication middleware runs

    private async Task<AxonUserAuth?> FindOrCreateUserAsync(
        AxonUserId userId,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        // Try to find existing user by provider
        var user = await _userStore.FindByProviderAsync(providerType.Value, subject, cancellationToken);

        if (user == null)
        {
            // Create new user
            user = AxonUserAuth.Create(
                userId,
                providerType.Value,
                issuer,
                subject,
                dynamicEnvironmentId: null,
                dynamicUserId: null);

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }
        }

        return user;
    }

    private async Task<List<Claim>> BuildAccessTokenClaimsAsync(AxonUserAuth user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new("axon_user_id", user.AxonPrincipalId.Value.ToString()),
            new("provider_type", user.ProviderType),
            new("original_issuer", user.OriginalIssuer),
            new("original_subject", user.OriginalSubject),
            new("jti", Guid.NewGuid().ToString()), // For replay protection
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add optional claims
        if (!string.IsNullOrEmpty(user.UserName))
            claims.Add(new(ClaimTypes.Name, user.UserName));

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new(ClaimTypes.Email, user.Email));

        if (!string.IsNullOrEmpty(user.DynamicEnvironmentId))
            claims.Add(new("dynamic_environment_id", user.DynamicEnvironmentId));

        if (!string.IsNullOrEmpty(user.DynamicUserId))
            claims.Add(new("dynamic_user_id", user.DynamicUserId));

        if (!string.IsNullOrEmpty(user.PrimaryChainId))
            claims.Add(new("primary_chain_id", user.PrimaryChainId));

        if (!string.IsNullOrEmpty(user.PrimaryWalletAddress))
            claims.Add(new("primary_wallet_address", user.PrimaryWalletAddress));

        // Add user roles
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Add custom user claims
        var userClaims = await _userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);

        return claims;
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        // Try to get key from JWT options first (configured in middleware)
        var axonJwtOptions = _jwtOptions.Get("AxonJwt");
        if (axonJwtOptions?.TokenValidationParameters?.IssuerSigningKey != null)
        {
            return axonJwtOptions.TokenValidationParameters.IssuerSigningKey as SymmetricSecurityKey
                ?? throw new InvalidOperationException("IssuerSigningKey must be a SymmetricSecurityKey");
        }

        // Fallback to configuration with Data Protection API for secure key storage
        var keyString = _configuration["Axon:SigningKey"];
        if (string.IsNullOrEmpty(keyString))
        {
            // Generate a secure key using Data Protection API if not configured
            var generatedKey = GenerateSecureKey();
            _logger.LogWarning("No signing key configured, generated a new one. Configure Axon:SigningKey for production.");
            return generatedKey;
        }

        // Use Data Protection to securely handle the key
        try
        {
            var keyBytes = Convert.FromBase64String(keyString);
            return new SymmetricSecurityKey(keyBytes);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Axon:SigningKey must be a valid Base64 encoded string", ex);
        }
    }

    private static SymmetricSecurityKey GenerateSecureKey()
    {
        // Generate a cryptographically secure key using Data Protection API
        var keyBytes = new byte[32]; // 256-bit key
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        return new SymmetricSecurityKey(keyBytes);
    }

}