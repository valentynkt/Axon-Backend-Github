using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding an AxonPrincipal by wallet ownership.
/// Includes wallet ownerships for efficient querying.
/// </summary>
public sealed class PrincipalByWalletSpec : Specification<AxonPrincipal>, ISingleResultSpecification<AxonPrincipal>
{
    public PrincipalByWalletSpec(WalletId walletId)
    {
        Query
            .Where(p => p.WalletOwnerships.Any(wo => wo.WalletId == walletId))
            .Include(p => p.WalletOwnerships);
    }
}