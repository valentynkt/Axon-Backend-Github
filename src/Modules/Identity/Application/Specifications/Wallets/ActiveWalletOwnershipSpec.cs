using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for active wallet ownerships (not deleted and verified).
/// </summary>
public sealed class ActiveWalletOwnershipSpec : Specification<WalletOwnership>
{
    public ActiveWalletOwnershipSpec()
    {
        Query.Where(w => !w.IsDeleted && w.State == OwnershipState.Verified);
    }
}