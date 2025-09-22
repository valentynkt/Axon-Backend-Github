using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Unified bearer token validator that supports both Dynamic JWT and Axon Access Tokens.
/// Implements token type detection and routing per High-Level Flow Architecture.
/// </summary>
public sealed class UnifiedBearerTokenValidator : IUnifiedBearerTokenValidator
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAxonJwtService _axonJwtService;
    private readonly ILogger<UnifiedBearerTokenValidator> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public UnifiedBearerTokenValidator(
        IDynamicAuthService dynamicAuthService,
        IAxonJwtService axonJwtService,
        ILogger<UnifiedBearerTokenValidator> logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _axonJwtService = axonJwtService ?? throw new ArgumentNullException(nameof(axonJwtService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<Result<UnifiedTokenContext, Error>> ValidateTokenAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Result.Failure<UnifiedTokenContext, Error>(
                Error.Unauthorized("Bearer token is required", "UNIFIED_TOKEN.MISSING"));
        }

        try
        {
            // Determine token type by examining the issuer claim
            var tokenType = await DetermineTokenTypeAsync(bearerToken);

            _logger.LogDebug("Detected token type: {TokenType}", tokenType);

            return tokenType switch
            {
                TokenType.DynamicJwt => await ValidateDynamicTokenAsync(bearerToken, cancellationToken),
                TokenType.AxonAccessToken => await ValidateAxonTokenAsync(bearerToken, cancellationToken),
                _ => Result.Failure<UnifiedTokenContext, Error>(
                    Error.Unauthorized("Unsupported token type", "UNIFIED_TOKEN.UNSUPPORTED_TYPE"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<UnifiedTokenContext, Error>(
                Error.Internal($"Token validation failed: {ex.Message}", "UNIFIED_TOKEN.VALIDATION_ERROR"));
        }
    }

    private async Task<TokenType> DetermineTokenTypeAsync(string token)
    {
        try
        {
            if (!_tokenHandler.CanReadToken(token))
            {
                return TokenType.DynamicJwt; // Default fallback
            }

            var jsonToken = _tokenHandler.ReadJwtToken(token);
            var issuer = jsonToken.Issuer;

            // Axon tokens have our configured issuer
            if (!string.IsNullOrEmpty(issuer) && issuer.Contains("axon", StringComparison.OrdinalIgnoreCase))
            {
                return TokenType.AxonAccessToken;
            }

            // Dynamic tokens have Dynamic's issuer format
            if (!string.IsNullOrEmpty(issuer) && issuer.Contains("dynamic", StringComparison.OrdinalIgnoreCase))
            {
                return TokenType.DynamicJwt;
            }

            // Check for custom Axon claims as fallback
            var axonUserIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "axon_user_id");
            if (axonUserIdClaim != null)
            {
                return TokenType.AxonAccessToken;
            }

            // Default to Dynamic JWT for unknown issuers
            return TokenType.DynamicJwt;
        }
        catch
        {
            // If we can't parse the token, default to Dynamic JWT
            return TokenType.DynamicJwt;
        }
    }

    private async Task<Result<UnifiedTokenContext, Error>> ValidateDynamicTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(token, cancellationToken);
        if (validationResult.IsFailure)
        {
            _logger.LogWarning("Dynamic JWT validation failed: {Error}", validationResult.Error.Message);
            return Result.Failure<UnifiedTokenContext, Error>(validationResult.Error);
        }

        var dynamicUser = validationResult.Value;

        // Get raw claims for creating ClaimsPrincipal
        var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(token, cancellationToken);
        if (rawClaimsResult.IsFailure)
        {
            _logger.LogWarning("Failed to get raw claims from Dynamic JWT: {Error}", rawClaimsResult.Error.Message);
            return Result.Failure<UnifiedTokenContext, Error>(rawClaimsResult.Error);
        }

        var principal = rawClaimsResult.Value;
        var issuer = principal.FindFirst("iss")?.Value ?? "unknown";
        var subject = principal.FindFirst("sub")?.Value ?? dynamicUser.AxonUserId;

        var context = new UnifiedTokenContext(
            TokenType: TokenType.DynamicJwt,
            AxonUserId: dynamicUser.AxonUserId,
            ProviderType: "dynamic",
            Issuer: issuer,
            Subject: subject,
            Principal: principal);

        _logger.LogDebug("Successfully validated Dynamic JWT for AxonUserId: {AxonUserId}", dynamicUser.AxonUserId);
        return Result.Success<UnifiedTokenContext, Error>(context);
    }

    private async Task<Result<UnifiedTokenContext, Error>> ValidateAxonTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var validationResult = await _axonJwtService.ValidateTokenAsync(token, cancellationToken);
        if (validationResult.IsFailure)
        {
            _logger.LogWarning("Axon JWT validation failed: {Error}", validationResult.Error.Message);
            return Result.Failure<UnifiedTokenContext, Error>(validationResult.Error);
        }

        var axonClaims = validationResult.Value;

        // Create ClaimsPrincipal from Axon JWT claims
        var claims = new[]
        {
            new Claim("sub", axonClaims.Subject),
            new Claim("iss", axonClaims.Issuer),
            new Claim("axon_user_id", axonClaims.AxonUserId.Value.ToString()),
            new Claim("provider_type", axonClaims.ProviderType.Value),
            new Claim("original_issuer", axonClaims.Issuer),
            new Claim("original_subject", axonClaims.Subject)
        };

        var identity = new ClaimsIdentity(claims, "AxonJwt");
        var principal = new ClaimsPrincipal(identity);

        var context = new UnifiedTokenContext(
            TokenType: TokenType.AxonAccessToken,
            AxonUserId: axonClaims.AxonUserId.Value.ToString(),
            ProviderType: axonClaims.ProviderType.Value,
            Issuer: axonClaims.Issuer,
            Subject: axonClaims.Subject,
            Principal: principal,
            ExpiresAt: axonClaims.ExpiresAt);

        _logger.LogDebug("Successfully validated Axon JWT for AxonUserId: {AxonUserId}", axonClaims.AxonUserId.Value);
        return Result.Success<UnifiedTokenContext, Error>(context);
    }
}