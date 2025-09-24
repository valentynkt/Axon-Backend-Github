using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Exchange;

/// <summary>
/// Result of the exchange operation with token and operation metrics
/// Maps to ExchangeDynamicTokenResponse per PRD requirements
/// </summary>
/// <param name="AccessToken">The generated Axon access token</param>
/// <param name="TokenType">Token type (typically "Bearer")</param>
/// <param name="ExpiresIn">Token expiration time in seconds</param>
/// <param name="AxonUserId">The principal's AxonUserId</param>
/// <param name="Created">Whether a new principal was created</param>
/// <param name="WalletsProcessed">Number of wallets processed from JWT</param>
/// <param name="WalletsLinked">Number of wallets successfully linked</param>
/// <param name="DefaultsApplied">Number of chain defaults applied</param>
/// <param name="Skipped">Number of wallets skipped (already linked)</param>
/// <param name="Conflicts">Number of wallet ownership conflicts encountered</param>
public sealed record ExchangeOutcome(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    AxonUserId AxonUserId,
    bool Created,
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts
);