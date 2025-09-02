using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for wallet ownerships of a specific principal.
/// </summary>
public sealed class PrincipalWalletOwnershipSpec : Specification<WalletOwnership>
{
    public PrincipalWalletOwnershipSpec(AxonId principalId)
    {
        Query.Where(w => w.AxonId == principalId);
    }
}