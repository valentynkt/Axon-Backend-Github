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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;

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
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(
        UserManager<AxonUserAuth> userManager,
        AxonUserStore userStore,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<JwtTokenService> logger)
    {
        _userManager = userManager;
        _userStore = userStore;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
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
    /// Validates an Axon access token
    /// </summary>
    public async Task<Result<AuthenticatedContext, Error>> ValidateAxonTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check replay protection
            var jti = GetJti(token);
            if (!string.IsNullOrEmpty(jti))
            {
                var cacheKey = $"jwt:used:{jti}";
                if (_memoryCache.TryGetValue(cacheKey, out _))
                {
                    return Result.Failure<AuthenticatedContext, Error>(
                        Error.Validation("Token has already been used", "AUTH.TOKEN_REPLAY"));
                }
            }

            // Validate token
            var key = GetSigningKey();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Axon:Issuer"] ?? "axon-api",
                ValidateAudience = true,
                ValidAudience = _configuration["Axon:Audience"] ?? "axon-api",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(5)
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

            // Mark token as used (replay protection)
            if (!string.IsNullOrEmpty(jti))
            {
                var cacheKey = $"jwt:used:{jti}";
                var jwtToken = validatedToken as JwtSecurityToken;
                var expiry = jwtToken?.ValidTo ?? DateTime.UtcNow.AddHours(1);
                _memoryCache.Set(cacheKey, true, expiry);
            }

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
            // Validate refresh token
            var key = GetSigningKey();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Axon:Issuer"] ?? "axon-api",
                ValidateAudience = true,
                ValidAudience = $"{_configuration["Axon:Audience"] ?? "axon-api"}:refresh",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(5)
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

    /// <summary>
    /// Extracts bearer token from Authorization header
    /// </summary>
    /// <param name="httpContext">HTTP context containing the request</param>
    /// <returns>Result containing the bearer token or error if invalid/missing</returns>
    public Result<string, Error> ExtractBearerToken(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Extract authorization header
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Request missing Authorization header or Bearer token for path {Path}",
                httpContext.Request.Path);
            return Result.Failure<string, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required"));
        }

        // Extract token value
        var bearerToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            _logger.LogWarning("Request has empty Bearer token for path {Path}",
                httpContext.Request.Path);
            return Result.Failure<string, Error>(
                Error.Unauthorized("Bearer token cannot be empty"));
        }

        return Result.Success<string, Error>(bearerToken);
    }

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
        var keyString = _configuration["Axon:SigningKey"];
        if (string.IsNullOrEmpty(keyString))
        {
            throw new InvalidOperationException("Axon:SigningKey not configured");
        }

        // Always expect Base64 encoded key for consistency with middleware
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

    private string? GetJti(string token)
    {
        try
        {
            var jwt = _tokenHandler.ReadJwtToken(token);
            return jwt?.Claims?.FirstOrDefault(c => c.Type == "jti")?.Value;
        }
        catch
        {
            return null;
        }
    }
}