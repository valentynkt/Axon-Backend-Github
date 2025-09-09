using Axon.Modules.Identity.Domain.Enums;
using Ardalis.Specification;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Count specification for AxonPrincipals by provider type.
/// Used with AxonPrincipalsForProviderSpec to provide total count.
/// </summary>
public sealed class AxonPrincipalsForProviderCountSpec : CountSpecification<AxonPrincipal>
{
    public AxonPrincipalsForProviderCountSpec(ProviderType providerType)
    {
        // Apply same filter as the paged specification
        Query.Where(p => p.Credentials.Any(c => c.ProviderType == providerType));
    }
}