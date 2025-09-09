using Axon.Modules.Identity.Domain.Enums;
using Ardalis.Specification;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for retrieving AxonPrincipals by provider type with pagination.
/// Optimized for reporting and analytics scenarios.
/// </summary>
public sealed class AxonPrincipalsForProviderSpec : PagedSpecification<AxonPrincipal>
{
    public AxonPrincipalsForProviderSpec(
        ProviderType providerType,
        Page page,
        bool includeCredentials = false)
        : base(page)
    {
        // Filter by principals that have credentials of the specified provider type
        Query.Where(p => p.Credentials.Any(c => c.ProviderType == providerType));

        // Include related credentials if requested
        if (includeCredentials)
        {
            Query.Include(p => p.Credentials.Where(c => c.ProviderType == providerType));
        }

        // Default ordering by creation date, then by ID for stable sorting
        Query.OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id);
    }
}