using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using System.Security.Claims;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Unified service for validating bearer tokens from multiple sources.
/// Supports both Dynamic JWT and Axon Access Tokens per High-Level Flow Architecture.
/// </summary>
public interface IUnifiedBearerTokenValidator
{
    /// <summary>
    /// Validates a bearer token and returns the authenticated user's claims.
    /// Automatically detects token type (Dynamic JWT vs Axon Access Token) and routes to appropriate validator.
    /// </summary>
    /// <param name="bearerToken">The bearer token to validate (without "Bearer " prefix)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with validated user context if successful, error if invalid</returns>
    Task<Result<UnifiedTokenContext, Error>> ValidateTokenAsync(
        string bearerToken,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified token context containing user identity information from validated token
/// </summary>
public record UnifiedTokenContext(
    TokenType TokenType,
    string AxonUserId,
    string ProviderType,
    string Issuer,
    string Subject,
    ClaimsPrincipal Principal,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Token type enumeration
/// </summary>
public enum TokenType
{
    DynamicJwt,
    AxonAccessToken
}