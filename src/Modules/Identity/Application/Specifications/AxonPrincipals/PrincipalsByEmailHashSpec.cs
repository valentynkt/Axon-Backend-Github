using Ardalis.Specification;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for finding principals by their primary email hash.
/// Useful for email-based lookups and duplicate detection.
/// </summary>
public sealed class PrincipalsByEmailHashSpec : Specification<AxonPrincipal>
{
    public PrincipalsByEmailHashSpec(EmailHash emailHash)
    {
        Query.Where(p => p.PrimaryEmailHash == emailHash);
    }
}