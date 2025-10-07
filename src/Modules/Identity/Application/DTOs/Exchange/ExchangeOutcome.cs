using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Exchange;

/// <summary>
/// Result of the exchange operation with tokens and operation metrics
/// Maps to ExchangeDynamicTokenResponse per PRD requirements
/// </summary>
/// <param name="AccessToken">The generated Axon access token (15min expiry)</param>
/// <param name="RefreshToken">The generated refresh token (30 days expiry), null if not issued</param>
/// <param name="TokenType">Token type (typically "Bearer")</param>
/// <param name="ExpiresIn">Access token expiration time in seconds</param>
/// <param name="AxonUserId">The principal's AxonUserId</param>
/// <param name="Created">Whether a new principal was created</param>
/// <param name="WalletsProcessed">Number of wallets processed from JWT</param>
/// <param name="WalletsLinked">Number of wallets successfully linked</param>
/// <param name="DefaultsApplied">Number of chain defaults applied</param>
/// <param name="Skipped">Number of wallets skipped (already linked)</param>
/// <param name="Conflicts">Number of wallet ownership conflicts encountered</param>
public sealed record ExchangeOutcome(
    string AccessToken,
    string? RefreshToken,
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