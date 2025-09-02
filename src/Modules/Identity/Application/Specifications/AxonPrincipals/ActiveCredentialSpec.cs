using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for active identity credentials (not deleted).
/// </summary>
public sealed class ActiveCredentialSpec : Specification<IdentityCredential>
{
    public ActiveCredentialSpec()
    {
        Query.Where(c => !c.IsDeleted);
    }
}