using System.Security.Claims;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Authentication service for token generation and challenge operations.
/// JWT validation is now handled by ASP.NET Core JWT Bearer middleware.
/// </summary>
public interface IAuthenticationService
{
    // Token Generation Operations
    Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = -1, // Use configuration default when -1
        CancellationToken cancellationToken = default);

    // Process authenticated context from middleware-validated token
    Task<Result<AuthenticatedContext, Error>> ProcessAuthenticatedUserAsync(
        ClaimsPrincipal principal,
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

    // Note: Replay protection is now handled by JwtEventHandlers in the middleware pipeline

    // HMAC Operations for Challenge Verification
    string GenerateMacForChallenge(string canonicalJson, string keyVersion);
    Result<bool, Error> ValidateMac(string message, string mac, string keyVersion);
    Task<UnitResult<Error>> CheckAndMarkNonceUsedAsync(
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