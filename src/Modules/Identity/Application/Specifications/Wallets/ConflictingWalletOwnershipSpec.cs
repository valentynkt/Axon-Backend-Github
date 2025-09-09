using Axon.Modules.Identity.Domain.Enums;
using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.Wallets;

/// <summary>
/// Specification for conflicting wallet ownerships (verified signing that would violate single owner rule).
/// </summary>
public sealed class ConflictingWalletOwnershipSpec : Specification<WalletOwnership>
{
    public ConflictingWalletOwnershipSpec()
    {
        Query.Where(w => w.State == OwnershipState.Verified && 
                        w.AccessMode == AccessMode.Signing && 
                        !w.IsDeleted);
    }
}