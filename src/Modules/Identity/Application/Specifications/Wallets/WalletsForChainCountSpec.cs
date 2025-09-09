using Ardalis.Specification;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Count specification for Wallets by chain.
/// Used with WalletsForChainSpec to provide total count.
/// </summary>
public sealed class WalletsForChainCountSpec : CountSpecification<Wallet>
{
    public WalletsForChainCountSpec(string chainId)
    {
        // Apply same filters as the paged specification
        Query.Where(w => w.Chain == chainId);

        // NOTE: Wallet domain model doesn't support soft deletion
    }
}