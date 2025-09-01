using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Specifications;

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

/// <summary>
/// Specification for wallet ownerships with a specific wallet ID.
/// </summary>
public sealed class SpecificWalletOwnershipSpec : Specification<WalletOwnership>
{
    public SpecificWalletOwnershipSpec(long walletId)
    {
        Query.Where(w => w.WalletId == walletId);
    }
}

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