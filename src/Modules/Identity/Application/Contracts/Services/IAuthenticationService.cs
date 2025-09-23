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
        int expiresIn = -1, // Use configuration default when -1
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
        string chainId,      // Compound format e.g. "solana-mainnet"
        string walletAddress,
        string audience,
        CancellationToken cancellationToken = default);

    Result<bool, Error> ValidateChallenge(
        string message,
        string expectedChainId,      // Compound format e.g. "solana-mainnet"
        string expectedWalletAddress,
        string expectedAudience);

    // Replay Protection (built-in)
    Task<Result<Unit, Error>> CheckAndMarkTokenUsedAsync(
        string jti,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    // HMAC Operations for Challenge Verification
    string GenerateMacForChallenge(string canonicalJson, string keyVersion);
    Result<bool, Error> ValidateMac(string message, string mac, string keyVersion);
    Task<Result<Unit, Error>> CheckAndMarkNonceUsedAsync(
        string signedMessage, string mkv, CancellationToken ct = default);
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
    string ChainId,      // Compound format e.g. "solana-mainnet"
    string Address,
    long IssuedAt,
    long Exp,
    string Nonce,
    string Aud,
    string Message,
    string Mac,
    string Mkv);

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