using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for wallet ownerships with a specific wallet ID.
/// </summary>
public sealed class SpecificWalletOwnershipSpec : Specification<WalletOwnership>
{
    public SpecificWalletOwnershipSpec(WalletId walletId)
    {
        Query.Where(w => w.WalletId == walletId);
    }
}