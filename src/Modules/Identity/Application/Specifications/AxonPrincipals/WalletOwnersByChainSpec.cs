using Ardalis.Specification;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for retrieving AxonPrincipals that own wallets on a specific chain.
/// </summary>
public sealed class WalletOwnersByChainSpec : PagedSpecification<AxonPrincipal>
{
    public WalletOwnersByChainSpec(
        ChainId chainId,
        Page page,
        bool includeWalletOwnerships = false)
        : base(page)
    {
        // Filter by principals that have wallet ownerships on the specified chain
        Query.Where(p => p.WalletOwnerships.Any(wo => wo.ChainId == chainId));

        // Include wallet ownerships if requested
        if (includeWalletOwnerships)
        {
            Query.Include(p => p.WalletOwnerships.Where(wo => wo.ChainId == chainId));
        }

        // Default ordering by creation date, then by ID for stable sorting
        Query.OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id);
    }
}