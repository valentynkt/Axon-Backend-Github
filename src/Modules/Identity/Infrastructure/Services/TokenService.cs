using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Axon.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Simple JWT token service that replaces 800+ lines of custom authentication logic
/// Uses standard .NET libraries with minimal custom code
/// </summary>
public sealed class TokenService
{
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        UserManager<AxonUserAuth> userManager,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generate JWT access token for user (replaces GenerateAccessTokenAsync)
    /// </summary>
    public async Task<string> GenerateAccessTokenAsync(AxonUserAuth user)
    {
        var claims = await BuildClaimsAsync(user);
        var key = GetSigningKey();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Authentication:Issuer"],
            Audience = _configuration["Authentication:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogDebug("Generated access token for user {AxonUserId}", user.AxonPrincipalId);
        return tokenString;
    }

    /// <summary>
    /// Generate refresh token for user (replaces GenerateRefreshTokenAsync)
    /// </summary>
    public async Task<string> GenerateRefreshTokenAsync(AxonUserAuth user)
    {
        var claims = await BuildRefreshClaimsAsync(user);
        var key = GetSigningKey();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(30), // 30-day refresh token
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Authentication:Issuer"],
            Audience = $"{_configuration["Authentication:Audience"]}:refresh"
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validate and get user from refresh token
    /// </summary>
    public async Task<AxonUserAuth?> GetUserFromRefreshTokenAsync(string refreshToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = GetSigningKey();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Authentication:Issuer"],
                ValidateAudience = true,
                ValidAudience = $"{_configuration["Authentication:Audience"]}:refresh",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(refreshToken, validationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return null;

            return await _userManager.FindByIdAsync(userId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid refresh token");
            return null;
        }
    }

    /// <summary>
    /// Build claims for access token
    /// </summary>
    private async Task<List<Claim>> BuildClaimsAsync(AxonUserAuth user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new("axon_user_id", user.AxonPrincipalId.Value.ToString()),
            new("provider_type", user.ProviderType),
            new("is_new_user", (user.FirstAuthenticatedAt == user.LastAuthenticatedAt).ToString().ToLowerInvariant())
        };

        // Add optional claims
        if (!string.IsNullOrEmpty(user.OriginalIssuer))
            claims.Add(new("original_issuer", user.OriginalIssuer));

        if (!string.IsNullOrEmpty(user.OriginalSubject))
            claims.Add(new("original_subject", user.OriginalSubject));

        if (!string.IsNullOrEmpty(user.DynamicEnvironmentId))
            claims.Add(new("dynamic_environment_id", user.DynamicEnvironmentId));

        if (!string.IsNullOrEmpty(user.DynamicUserId))
            claims.Add(new("dynamic_user_id", user.DynamicUserId));

        // Add user roles
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    /// <summary>
    /// Build claims for refresh token (minimal claims)
    /// </summary>
    private static Task<List<Claim>> BuildRefreshClaimsAsync(AxonUserAuth user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("axon_user_id", user.AxonPrincipalId.Value.ToString()),
            new("token_type", "refresh")
        };

        return Task.FromResult(claims);
    }

    /// <summary>
    /// Get signing key from configuration
    /// </summary>
    private SymmetricSecurityKey GetSigningKey()
    {
        var key = _configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException("Authentication:SigningKey not configured");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Get token expiry from configuration
    /// </summary>
    private int GetTokenExpiryMinutes()
    {
        return _configuration.GetValue("Authentication:TokenExpiryMinutes", 15);
    }
}