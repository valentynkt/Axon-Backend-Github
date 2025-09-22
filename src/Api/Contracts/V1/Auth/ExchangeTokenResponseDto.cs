namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response for successful token exchange (Axon-issued tokens + processing metrics).
/// </summary>
public sealed record ExchangeTokenResponseDto(
    string AccessToken,      // Axon JWT for subsequent API calls
    string RefreshToken,     // Refresh token for token renewal
    string TokenType,        // "Bearer"
    long ExpiresIn,          // access token seconds until expiry
    string AxonUserId,       // principal id
    bool Created,            // was the principal created during this exchange
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts,
    DateTimeOffset IssuedAt,              // when tokens were issued
    DateTimeOffset AccessTokenExpiresAt,  // when access token expires
    DateTimeOffset RefreshTokenExpiresAt  // when refresh token expires
);