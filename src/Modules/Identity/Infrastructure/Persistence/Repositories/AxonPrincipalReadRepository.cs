using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
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

    /// <summary>
    /// Compiled query for fetching principals by provider type - hot path optimization
    /// </summary>
    private static readonly Func<IdentityReadDbContext, ProviderType, int, int, IAsyncEnumerable<AxonPrincipal>> 
        GetByProviderTypeCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, ProviderType providerType, int skip, int take) =>
                context.Set<AxonPrincipal>()
                    .Include(p => p.Credentials)
                    .Where(p => p.Credentials.Any(c => c.Provider == providerType.Value))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking());

    /// <summary>
    /// Compiled query for counting principals by provider type - optimized for count operations
    /// </summary>
    private static readonly Func<IdentityReadDbContext, ProviderType, Task<int>> 
        CountByProviderTypeCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, ProviderType providerType) =>
                context.Set<AxonPrincipal>()
                    .Where(p => p.Credentials.Any(c => c.Provider == providerType.Value))
                    .Count());

    /// <summary>
    /// Compiled query for recently created principals - hot path optimization
    /// </summary>
    private static readonly Func<IdentityReadDbContext, DateTimeOffset, int, IAsyncEnumerable<AxonPrincipal>> 
        GetRecentlyCreatedCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, DateTimeOffset cutoffDate, int take) =>
                context.Set<AxonPrincipal>()
                    .Include(p => p.Credentials)
                    .Where(p => p.CreatedAt >= cutoffDate)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(take)
                    .AsNoTracking());

    public AxonPrincipalReadRepository(IdentityReadDbContext context) : base(context)
    {
        _identityDbContext = context;
    }

    /// <summary>
    /// Optimized method for getting principals by provider type using compiled queries.
    /// </summary>
    public async Task<IReadOnlyList<AxonPrincipal>> GetByProviderTypeAsync(
        ProviderType providerType,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AxonPrincipal>();
        await foreach (var item in GetByProviderTypeCompiled(_identityDbContext, providerType, skip, take)
                          .WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results.AsReadOnly();
    }

    /// <summary>
    /// Optimized method for counting principals by provider type using compiled queries.
    /// </summary>
    public Task<int> CountByProviderTypeAsync(
        ProviderType providerType,
        CancellationToken cancellationToken = default)
    {
        return CountByProviderTypeCompiled(_identityDbContext, providerType);
    }

    /// <summary>
    /// Optimized method for getting recently created principals using compiled queries.
    /// </summary>
    public async Task<IReadOnlyList<AxonPrincipal>> GetRecentlyCreatedAsync(
        TimeSpan within,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTimeOffset.UtcNow.Subtract(within);
        var results = new List<AxonPrincipal>();
        await foreach (var item in GetRecentlyCreatedCompiled(_identityDbContext, cutoffDate, take)
                          .WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<AxonPrincipal>> GetWalletOwnersByChainAsync(
        string chainId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var chain = ChainId.Create(chainId);
        // Join principals with their wallet ownerships and wallets to filter by chain
        return await _identityDbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => p.WalletOwnerships.Any(wo => 
                _identityDbContext.Set<Wallet>().Any(w => 
                    w.Id == wo.WalletId && w.Chain == chain)))
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AxonPrincipal?> GetByIdWithActiveOwnershipsAsync(
        AxonId axonId,
        CancellationToken cancellationToken = default)
    {
        // Use EF Core to get principal with active wallet ownerships and chain defaults in single query
        return await _identityDbContext.Set<AxonPrincipal>()
            .Where(p => p.Id == axonId)
            .Include(p => p.WalletOwnerships.Where(wo => wo.Status == OwnershipStatus.Verified))
            .Include(p => p.ChainDefaults)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }
}