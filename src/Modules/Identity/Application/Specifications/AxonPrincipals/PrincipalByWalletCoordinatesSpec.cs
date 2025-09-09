using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding a principal that owns a wallet by chain ID.
/// This specification finds principals that own any wallet on the specified chain.
/// Address matching must be done at the repository level due to lack of navigation property.
/// </summary>
public sealed class PrincipalByWalletCoordinatesSpec : Specification<AxonPrincipal>, ISingleResultSpecification<AxonPrincipal>
{
    public PrincipalByWalletCoordinatesSpec(string chainId)
    {
        Query
            .Where(p => p.WalletOwnerships.Any(wo => 
                wo.ChainId == chainId && 
                !wo.IsDeleted))
            .Include(p => p.WalletOwnerships.Where(wo => 
                wo.ChainId == chainId && 
                !wo.IsDeleted));
    }
}