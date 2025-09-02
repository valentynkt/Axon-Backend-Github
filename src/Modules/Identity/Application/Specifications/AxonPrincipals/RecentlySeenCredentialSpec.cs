using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for credentials that have been seen recently.
/// </summary>
public sealed class RecentlySeenCredentialSpec : Specification<IdentityCredential>
{
    public RecentlySeenCredentialSpec(DateTimeOffset cutoffDate)
    {
        Query.Where(c => c.LastSeenAt >= cutoffDate);
    }
}