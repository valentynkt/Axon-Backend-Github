using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification to check if a wallet is owned with verified signing access mode.
/// Used for existence checking - returns true if any principal owns the wallet with signing access.
/// </summary>
public sealed class WalletOwnedBySigningSpec : Specification<AxonPrincipal>
{
    public WalletOwnedBySigningSpec(WalletId walletId)
    {
        Query.Where(p => p.WalletOwnerships.Any(wo => 
            wo.WalletId == walletId && 
            wo.AccessMode == AccessMode.Signing));
    }
}