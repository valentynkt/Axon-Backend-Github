using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Exchange;

/// <summary>
/// Result of the exchange operation with operation metrics
/// Maps to ExchangeDynamicTokenResponse per PRD requirements
/// </summary>
/// <param name="AxonId">The principal's AxonId</param>
/// <param name="Created">Whether a new principal was created</param>
/// <param name="WalletsProcessed">Number of wallets processed from JWT</param>
/// <param name="WalletsLinked">Number of wallets successfully linked</param>
/// <param name="DefaultsApplied">Number of chain defaults applied</param>
/// <param name="Skipped">Number of wallets skipped (already linked)</param>
/// <param name="Conflicts">Number of wallet ownership conflicts encountered</param>
public sealed record ExchangeOutcome(
    AxonId AxonId,
    bool Created,
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts
);