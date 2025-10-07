namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Unified response for token issuance endpoints
/// </summary>
public sealed record AuthTokenResponseDto(
    string AccessToken,        // Axon JWT (15min expiry)
    string? RefreshToken,      // Refresh token (30 days expiry), null if not issued
    string TokenType,          // "Bearer"
    long ExpiresIn,           // Seconds until access token expiry
    string AxonUserId,        // Principal ID
    bool Created,             // Was principal created in this call
    int WalletsLinked,        // Number of wallets linked
    int Conflicts             // Number of conflicts encountered
);