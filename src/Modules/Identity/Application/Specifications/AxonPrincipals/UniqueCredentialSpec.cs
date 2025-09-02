using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for a unique credential (provider, issuer, subject combination).
/// </summary>
public sealed class UniqueCredentialSpec : Specification<IdentityCredential>
{
    public UniqueCredentialSpec(ProviderType providerType, string issuer, string subject)
    {
        Query.Where(c => c.ProviderType == providerType && 
                        c.Issuer == issuer && 
                        c.Subject == subject);
    }
}