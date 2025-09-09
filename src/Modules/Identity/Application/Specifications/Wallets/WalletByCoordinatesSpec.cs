using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for finding a wallet by chain ID and address coordinates.
/// Excludes deleted wallets by default.
/// </summary>
public sealed class WalletByCoordinatesSpec : Specification<Wallet>, ISingleResultSpecification<Wallet>
{
    public WalletByCoordinatesSpec(string chainId, Address address, bool includeDeleted = false)
    {
        Query.Where(w => w.Chain == chainId && w.Address == address);
        
        if (!includeDeleted)
        {
            Query.Where(w => !w.IsDeleted);
        }
    }
}