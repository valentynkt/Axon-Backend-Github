using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for wallet ownerships that can sign transactions.
/// </summary>
public sealed class SigningWalletOwnershipSpec : Specification<WalletOwnership>
{
    public SigningWalletOwnershipSpec()
    {
        Query.Where(w => !w.IsDeleted && 
                        w.State == OwnershipState.Verified && 
                        w.AccessMode == AccessMode.Signing);
    }
}