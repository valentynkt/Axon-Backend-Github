using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding principals that own specific wallets with verified signing access.
/// Returns principals with included wallet ownerships for efficient querying.
/// </summary>
public sealed class PrincipalsWithVerifiedSigningWalletsSpec : Specification<AxonPrincipal>
{
    public PrincipalsWithVerifiedSigningWalletsSpec(IEnumerable<WalletId> walletIds)
    {
        var walletIdsList = walletIds.ToList();
        
        Query
            .Where(p => p.WalletOwnerships.Any(wo => 
                walletIdsList.Contains(wo.WalletId) && 
                wo.AccessMode == AccessMode.Signing))
            .Include(p => p.WalletOwnerships);
    }
}