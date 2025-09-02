using Axon.Modules.Identity.Application.Abstractions.Persistence;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for AxonPrincipal queries using Entity Framework Core.
/// Supports specification-based queries through Ardalis.Specification integration.
/// Includes compiled queries for optimal performance on hot paths.
/// </summary>
internal sealed class AxonPrincipalReadRepository : EfSpecificationReadRepository<AxonPrincipal>, IAxonPrincipalReadRepository
{
    private readonly IdentityReadDbContext _identityDbContext;


    public AxonPrincipalReadRepository(IdentityReadDbContext context) : base(context)
    {
        _identityDbContext = context;
    }

    public async Task<IReadOnlyList<AxonPrincipal>> GetByProviderTypeAsync(
        ProviderType providerType,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var page = new Page(
            Number: (skip / take) + 1,
            Size: take);
            
        var spec = new AxonPrincipalsForProviderSpec(providerType, page, includeCredentials: true);
        
        return await ListAsync(spec, cancellationToken);
    }

    public async Task<int> CountByProviderTypeAsync(
        ProviderType providerType,
        CancellationToken cancellationToken = default)
    {
        var countSpec = new AxonPrincipalsForProviderCountSpec(providerType);
        
        return await CountAsync(countSpec, cancellationToken);
    }

    public async Task<IReadOnlyList<AxonPrincipal>> GetRecentlyCreatedAsync(
        TimeSpan within,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var page = new Page(1, take);
        var spec = new RecentlyCreatedPrincipalsSpec(within, page, includeCredentials: true);
        
        return await ListAsync(spec, cancellationToken);
    }

    public async Task<IReadOnlyList<AxonPrincipal>> GetWalletOwnersByChainAsync(
        ChainId chainId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var page = new Page(
            Number: (skip / take) + 1,
            Size: take);
            
        var spec = new WalletOwnersByChainSpec(chainId, page, includeWalletOwnerships: true);
        
        return await ListAsync(spec, cancellationToken);
    }
}