namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Response for successful token exchange
/// </summary>
public sealed record ExchangeTokenResponseDto(
    string AxonId,
    bool Created,
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts);