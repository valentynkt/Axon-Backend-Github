using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for credentials belonging to a specific principal.
/// </summary>
public sealed class PrincipalCredentialSpec : Specification<IdentityCredential>
{
    public PrincipalCredentialSpec(AxonId principalId)
    {
        Query.Where(c => c.AxonId == principalId);
    }
}