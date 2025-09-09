using Axon.Modules.Identity.Domain.Enums;
using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding an AxonPrincipal by specific credential details.
/// Includes credentials in the query for efficient lookup.
/// </summary>
public sealed class PrincipalByCredentialSpec : Specification<AxonPrincipal>, ISingleResultSpecification<AxonPrincipal>
{
    public PrincipalByCredentialSpec(ProviderType providerType, string issuer, string subject)
    {
        Query
            .Where(p => p.Credentials.Any(c => 
                c.ProviderType == providerType && 
                c.Issuer == issuer && 
                c.Subject == subject))
            .Include(p => p.Credentials);
    }
}