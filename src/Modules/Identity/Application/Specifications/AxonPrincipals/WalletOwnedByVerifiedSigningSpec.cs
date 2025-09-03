using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification to check if a wallet is owned with a verified signing proof type.
/// Used for conflict checking to ensure only one principal can have verified signing access to a wallet.
/// </summary>
public sealed class WalletOwnedByVerifiedSigningSpec : Specification<AxonPrincipal>, ISingleResultSpecification<AxonPrincipal>
{
    public WalletOwnedByVerifiedSigningSpec(WalletId walletId)
    {
        Query
            .Where(p => p.WalletOwnerships.Any(wo => 
                wo.WalletId == walletId && 
                wo.ProofType == ProofType.DynamicVerified ||
                wo.ProofType == ProofType.DirectSignature))
            .Include(p => p.WalletOwnerships);
    }
}