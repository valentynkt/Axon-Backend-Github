using System.Security.Claims;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Unified authentication service that handles all JWT and challenge operations.
/// Consolidates functionality from AxonJwtService, UnifiedBearerTokenValidator,
/// CanonicalMessageService, and MemoryJwtReplayGuard.
/// </summary>
public interface IAuthenticationService
{
    // Token Operations
    Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = 900, // Default to 15 minutes for security
        CancellationToken cancellationToken = default);

    Task<Result<AuthenticatedContext, Error>> ValidateTokenAsync(
        string bearerToken,
        CancellationToken cancellationToken = default);

    // Refresh Token Operations
    Task<Result<RefreshTokenResponse, Error>> GenerateRefreshTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default);

    Task<Result<RefreshTokenResponse, Error>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    // Challenge Operations
    Task<Result<AuthenticationChallenge, Error>> GenerateChallengeAsync(
        NetworkEnvironment networkEnvironment,
        string chainId,
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default);

    Result<bool, Error> ValidateChallengeAsync(
        string message,
        NetworkEnvironment expectedNetworkEnvironment,
        string expectedChainId,
        string expectedWalletAddress,
        string expectedAudience);

    // Replay Protection (built-in)
    Task<Result<Unit, Error>> CheckAndMarkTokenUsedAsync(
        string jti,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified token response
/// </summary>
public record AxonToken(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Unified authentication context from validated token
/// </summary>
public record AuthenticatedContext(
    TokenType TokenType,
    AxonUserId AxonUserId,
    ProviderType ProviderType,
    string Issuer,
    string Subject,
    ClaimsPrincipal Principal,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Simplified challenge response
/// </summary>
public record AuthenticationChallenge(
    string NetworkEnvironment,
    string ChainId,
    string Address,
    long IssuedAt,
    long Exp,
    string Nonce,
    string Aud,
    string Message);

/// <summary>
/// Token type enumeration
/// </summary>
public enum TokenType
{
    DynamicJwt,
    AxonAccessToken
}

/// <summary>
/// Refresh token response with both access and refresh tokens
/// </summary>
public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset IssuedAt,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);