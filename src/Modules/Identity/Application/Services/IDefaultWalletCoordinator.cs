using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for coordinating default wallet setting logic.
/// Consolidates default wallet policy enforcement to eliminate duplication across handlers.
/// </summary>
public interface IDefaultWalletCoordinator
{
    /// <summary>
    /// Applies default wallet setting if the ownership is eligible (verified signing).
    /// Handles all policy checks and provides consistent messaging across handlers.
    /// </summary>
    /// <param name="principal">The principal to update</param>
    /// <param name="chainId">The chain for which to set the default</param>
    /// <param name="walletId">The wallet to set as default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple indicating if default was applied and reason if not applied</returns>
    Task<(bool? applied, string? reason)> ApplyIfEligibleAsync(
        AxonPrincipal principal,
        ChainId chainId,
        WalletId walletId,
        CancellationToken cancellationToken = default);
}