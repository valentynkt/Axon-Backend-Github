using Ardalis.Specification;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Application.Specifications;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

namespace Axon.Modules.Identity.Application.Specifications.AxonPrincipals;

/// <summary>
/// Specification for retrieving recently created AxonPrincipals with pagination.
/// Useful for monitoring new user registrations.
/// </summary>
public sealed class RecentlyCreatedPrincipalsSpec : PagedSpecification<AxonPrincipal>
{
    public RecentlyCreatedPrincipalsSpec(
        TimeSpan within,
        Page page,
        bool includeCredentials = false)
        : base(page)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(within);

        // Filter by creation date
        Query.Where(p => p.CreatedAt >= cutoffTime);

        // Include credentials if requested
        if (includeCredentials)
        {
            Query.Include(p => p.Credentials);
        }

        // Order by most recent first
        Query.OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id);
    }
}