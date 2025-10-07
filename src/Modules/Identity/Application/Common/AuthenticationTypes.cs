namespace Axon.Modules.Identity.Application.Common;

using Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Authentication challenge for wallet signing
/// </summary>
public sealed record AuthenticationChallenge(
    string ChainId,
    string Address,
    long IssuedAt,
    long Exp,
    string Nonce,
    string Aud,
    string Message);

/// <summary>
/// Axon JWT token response
/// </summary>
public sealed record AxonToken(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Refresh token response with access and refresh tokens
/// </summary>
public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset IssuedAt,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Authenticated context from validated JWT
/// </summary>
public sealed record AuthenticatedContext(
    TokenType TokenType,
    AxonUserId AxonUserId,
    ProviderType ProviderType,
    string Issuer,
    string Subject,
    System.Security.Claims.ClaimsPrincipal Principal,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Token type enumeration
/// </summary>
public enum TokenType
{
    AxonAccessToken,
    DynamicJwt,
    RefreshToken
}