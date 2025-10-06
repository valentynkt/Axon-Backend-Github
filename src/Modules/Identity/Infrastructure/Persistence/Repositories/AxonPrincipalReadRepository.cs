using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
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
public sealed class AxonPrincipalReadRepository : EfSpecificationReadRepository<AxonPrincipal>, IAxonPrincipalReadRepository
{
    private readonly IdentityDbContext _identityDbContext;

    /// <summary>
    /// Compiled query for fetching principals by provider type - hot path optimization
    /// </summary>
    private static readonly Func<IdentityDbContext, ProviderType, int, int, IAsyncEnumerable<AxonPrincipal>>
        GetByProviderTypeCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, ProviderType providerType, int skip, int take) =>
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
    private static readonly Func<IdentityDbContext, ProviderType, Task<int>>
        CountByProviderTypeCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, ProviderType providerType) =>
                context.Set<AxonPrincipal>()
                    .Where(p => p.Credentials.Any(c => c.Provider == providerType.Value))
                    .Count());

    /// <summary>
    /// Compiled query for recently created principals - hot path optimization
    /// </summary>
    private static readonly Func<IdentityDbContext, DateTimeOffset, int, IAsyncEnumerable<AxonPrincipal>>
        GetRecentlyCreatedCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, DateTimeOffset cutoffDate, int take) =>
                context.Set<AxonPrincipal>()
                    .Include(p => p.Credentials)
                    .Where(p => p.CreatedAt >= cutoffDate)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(take)
                    .AsNoTracking());

    /// <summary>
    /// Compiled query for finding principal by credential - hot path optimization for /auth/me
    /// </summary>
    private static readonly Func<IdentityDbContext, string, string, string, Task<AxonPrincipal?>>
        FindByCredentialCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, string provider, string issuer, string subject) =>
                context.Set<AxonPrincipal>()
                    .Include(p => p.Credentials)
                    .Include(p => p.PrincipalChainDefaults)
                    .Where(p => p.Credentials.Any(c =>
                        c.Provider == provider &&
                        c.Issuer == issuer &&
                        c.Subject == subject))
                    .AsNoTracking()
                    .FirstOrDefault());

    public AxonPrincipalReadRepository(IdentityDbContext context) : base(context)
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
        var chain = ChainId.From(chainId);
        // Join principals with their wallet ownerships and wallets to filter by chain
        return await _identityDbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => p.WalletOwnerships.Any(wo => 
                _identityDbContext.Set<Wallet>().Any(w => 
                    w.Id == wo.WalletId && w.ChainId == chain)))
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AxonPrincipal?> GetByIdWithActiveOwnershipsAsync(
        AxonUserId AxonUserId,
        CancellationToken cancellationToken = default)
    {
        // Use EF Core to get principal with active wallet ownerships and chain defaults in single query
        // Note: PrincipalChainDefaults are owned entities - automatically loaded by EF Core
        return await _identityDbContext.Set<AxonPrincipal>()
            .Where(p => p.Id == AxonUserId)
            .Include(p => p.WalletOwnerships.Where(wo => wo.Status == OwnershipStatus.Verified))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default)
    {
        return await FindByCredentialCompiled(_identityDbContext, providerType.Value, issuer, subject);
    }

    public async Task<bool> IsWalletOwnedByVerifiedSigningAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        return await _identityDbContext.Set<AxonPrincipal>()
            .AnyAsync(p => p.WalletOwnerships.Any(wo =>
                wo.WalletId == walletId &&
                wo.AccessMode == AccessMode.Signing &&
                wo.Status == OwnershipStatus.Verified), cancellationToken);
    }

    public async Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        AxonUserId? excludePrincipalId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _identityDbContext.Set<AxonPrincipal>()
            .Where(p => p.Credentials.Any(c =>
                c.Provider == providerType.Value &&
                c.Issuer == issuer &&
                c.Subject == subject));

        // Exclude the specified principal from the check (when updating existing principal)
        if (excludePrincipalId is not null)
        {
            query = query.Where(p => p.Id != excludePrincipalId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default)
    {
        var walletIdsList = walletIds.ToList();

        if (walletIdsList.Count == 0)
            return new Dictionary<WalletId, AxonPrincipal>();

        var principals = await _identityDbContext.Set<AxonPrincipal>()
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships)
            .Include(p => p.PrincipalChainDefaults)
            .Where(p => p.WalletOwnerships.Any(wo =>
                walletIdsList.Contains(wo.WalletId) &&
                wo.AccessMode == AccessMode.Signing &&
                wo.Status == OwnershipStatus.Verified))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<WalletId, AxonPrincipal>();

        foreach (var principal in principals)
        {
            foreach (var ownership in principal.WalletOwnerships)
            {
                if (walletIdsList.Contains(ownership.WalletId) &&
                    ownership.AccessMode == AccessMode.Signing &&
                    ownership.Status == OwnershipStatus.Verified)
                {
                    result[ownership.WalletId] = principal;
                }
            }
        }

        return result;
    }
}