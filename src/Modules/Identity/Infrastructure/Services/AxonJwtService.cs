using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service for generating and validating Axon JWT access tokens.
/// Implements the token minting requirements from High-Level Flow Architecture.
/// </summary>
public sealed class AxonJwtService : IAxonJwtService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ILogger<AxonJwtService> _logger;

    public AxonJwtService(IConfiguration configuration, ILogger<AxonJwtService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var axonSection = configuration.GetSection("Axon");
        _issuer = axonSection["Issuer"] ?? throw new InvalidOperationException("Axon:Issuer configuration is required");
        _audience = axonSection["Audience"] ?? "axon-api";
        var signingKey = axonSection["SigningKey"] ?? throw new InvalidOperationException("Axon:SigningKey configuration is required");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<Result<AxonJwtToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = 3600,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddSeconds(expiresIn);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, axonUserId.Value.ToString()),
                new Claim(JwtRegisteredClaimNames.Iss, _issuer),
                new Claim(JwtRegisteredClaimNames.Aud, _audience),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Exp, expiresAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Custom claims for Axon identity context
                new Claim("axon_user_id", axonUserId.Value.ToString()),
                new Claim("provider_type", providerType.Value),
                new Claim("original_issuer", issuer),
                new Claim("original_subject", subject)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt.DateTime,
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            _logger.LogDebug("Generated Axon JWT token for AxonUserId={AxonUserId}, Provider={Provider}, ExpiresIn={ExpiresIn}s",
                axonUserId.Value, providerType.Value, expiresIn);

            var result = new AxonJwtToken(
                AccessToken: tokenString,
                TokenType: "Bearer",
                ExpiresIn: expiresIn,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            await Task.CompletedTask;
            return Result.Success<AxonJwtToken, Error>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Axon JWT token for AxonUserId={AxonUserId}", axonUserId.Value);
            return Result.Failure<AxonJwtToken, Error>(
                Error.Internal($"Failed to generate access token: {ex.Message}", "AXON_JWT.GENERATION_FAILED"));
        }
    }

    public async Task<Result<AxonJwtClaims, Error>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = !string.IsNullOrEmpty(_audience),
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(60),
                NameClaimType = "sub"
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Extract claims
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
                return Result.Failure<AxonJwtClaims, Error>(
                    Error.Validation("Token missing required claims", "AXON_JWT.INVALID_CLAIMS"));
            }

            // Parse and validate values
            if (!Guid.TryParse(axonUserIdClaim, out var axonUserIdGuid))
            {
                return Result.Failure<AxonJwtClaims, Error>(
                    Error.Validation("Invalid AxonUserId format", "AXON_JWT.INVALID_AXON_USER_ID"));
            }

            var axonUserId = new AxonUserId(axonUserIdGuid);

            var providerTypeResult = ProviderType.Create(providerTypeClaim);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<AxonJwtClaims, Error>(providerTypeResult.Error);
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim));
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim));

            var claims = new AxonJwtClaims(
                AxonUserId: axonUserId,
                ProviderType: providerTypeResult.Value,
                Issuer: originalIssuerClaim,
                Subject: originalSubjectClaim,
                IssuedAt: issuedAt,
                ExpiresAt: expiresAt);

            _logger.LogDebug("Successfully validated Axon JWT token for AxonUserId={AxonUserId}",
                axonUserId.Value);

            await Task.CompletedTask;
            return Result.Success<AxonJwtClaims, Error>(claims);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Axon JWT token validation failed: Token expired");
            return Result.Failure<AxonJwtClaims, Error>(
                Error.Unauthorized("Token has expired", "AXON_JWT.EXPIRED"));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Axon JWT token validation failed: Invalid signature");
            return Result.Failure<AxonJwtClaims, Error>(
                Error.Unauthorized("Token signature is invalid", "AXON_JWT.INVALID_SIGNATURE"));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Axon JWT token validation failed: {Message}", ex.Message);
            return Result.Failure<AxonJwtClaims, Error>(
                Error.Unauthorized($"Token validation failed: {ex.Message}", "AXON_JWT.VALIDATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Axon JWT token validation");
            return Result.Failure<AxonJwtClaims, Error>(
                Error.Internal($"Unexpected validation error: {ex.Message}", "AXON_JWT.VALIDATION_ERROR"));
        }
    }
}