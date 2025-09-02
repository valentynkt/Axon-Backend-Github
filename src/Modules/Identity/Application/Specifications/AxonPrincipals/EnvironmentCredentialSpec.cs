using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for credentials with a specific environment ID.
/// </summary>
public sealed class EnvironmentCredentialSpec : Specification<IdentityCredential>
{
    public EnvironmentCredentialSpec(string environmentId)
    {
        Query.Where(c => c.EnvironmentId == environmentId);
    }
}