using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Repository for wallet ownership read-only operations supporting deterministic principal resolution.
/// All ownership modifications must go through the AxonPrincipal aggregate root to maintain DDD boundaries.
/// </summary>
public interface IWalletOwnershipRepository
{
    /// <summary>
    /// Finds active ownerships (status != revoked) for a specific wallet.
    /// Used in wallet-fallback resolution to find candidate principals.
    /// Returns ownership with associated principal.
    /// </summary>
    Task<IReadOnlyList<WalletOwnershipWithPrincipal>> FindActiveOwnershipsByWalletAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds ownerships by wallet IDs for batch operations.
    /// Returns verified+signing ownerships grouped by wallet ID.
    /// </summary>
    Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default);
}