using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding a specific principal's wallet ownership.
/// Includes wallet ownerships for efficient querying.
/// </summary>
public sealed class CallerWalletOwnershipSpec : Specification<AxonPrincipal>, ISingleResultSpecification<AxonPrincipal>
{
    public CallerWalletOwnershipSpec(AxonId axonId, WalletId walletId)
    {
        Query
            .Where(p => p.Id == axonId)
            .Where(p => p.WalletOwnerships.Any(wo => wo.WalletId == walletId && !wo.IsDeleted))
            .Include(p => p.WalletOwnerships.Where(wo => wo.WalletId == walletId && !wo.IsDeleted));
    }
}