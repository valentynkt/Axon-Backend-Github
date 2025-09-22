namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response for successful token exchange (Axon-issued token + processing metrics).
/// </summary>
public sealed record ExchangeTokenResponseDto(
    string AccessToken,      // Axon JWT for subsequent API calls
    string TokenType,        // "Bearer"
    long ExpiresIn,          // seconds until expiry
    string AxonUserId,       // principal id
    bool Created,            // was the principal created during this exchange
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts
);