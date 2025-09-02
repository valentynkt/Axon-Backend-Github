using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for credentials of a specific provider type.
/// </summary>
public sealed class ProviderCredentialSpec : Specification<IdentityCredential>
{
    public ProviderCredentialSpec(ProviderType providerType)
    {
        Query.Where(c => c.ProviderType == providerType);
    }
}