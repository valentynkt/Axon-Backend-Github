using Ardalis.Specification;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for retrieving Wallets by chain with pagination and filtering.
/// </summary>
public sealed class WalletsForChainSpec : PagedSpecification<Wallet>
{
    public WalletsForChainSpec(
        string chainId,
        Page page)
        : base(page)
    {
        // Filter by chain (using correct property name)
        Query.Where(w => w.Chain == chainId);

        // NOTE: Wallet is not deletable in the current domain model
        // Soft delete logic removed as Wallet doesn't inherit from DeletableEntity
        
        // NOTE: Wallet entity doesn't support soft deletion or have ownership navigation properties
        // Ownership information is stored in separate WalletOwnership entities

        // Default ordering by last seen time, then first seen time, then ID
        Query.OrderByDescending(w => w.LastSeenAt)
            .ThenByDescending(w => w.FirstSeenAt)
            .ThenBy(w => w.Id);
    }
}