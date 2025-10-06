using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for wallet queries using Entity Framework Core.
/// Supports specification-based queries through Ardalis.Specification integration.
/// Includes compiled queries for optimal performance on hot paths.
/// </summary>
public sealed class WalletReadRepository : EfSpecificationReadRepository<Wallet>, IWalletReadRepository
{
    private readonly IdentityDbContext _identityDbContext;

    /// <summary>
    /// Compiled query for checking wallet existence by chain and address - hot path optimization
    /// </summary>
    private static readonly Func<IdentityDbContext, ChainId, Address, Task<bool>>
        ExistsByChainAndAddressCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, ChainId chainId, Address address) =>
                context.Set<Wallet>()
                    .Any(w => w.ChainId == chainId.Value && w.Address == address));

    /// <summary>
    /// Compiled query for fetching wallets by chain with pagination - hot path optimization
    /// </summary>
    private static readonly Func<IdentityDbContext, ChainId, int, int, IAsyncEnumerable<Wallet>>
        GetByChainOptimizedCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, ChainId chainId, int skip, int take) =>
                context.Set<Wallet>()
                    .Where(w => w.ChainId == chainId.Value)
                    .OrderBy(w => w.FirstSeenAt)
                    .ThenBy(w => w.Id)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking());

    /// <summary>
    /// Compiled query for counting wallets by chain - optimized for count operations
    /// </summary>
    private static readonly Func<IdentityDbContext, ChainId, Task<int>>
        GetCountOptimizedCompiled = EF.CompileAsyncQuery(
            (IdentityDbContext context, ChainId chainId) =>
                context.Set<Wallet>()
                    .Where(w => w.ChainId == chainId.Value)
                    .Count());

    public WalletReadRepository(IdentityDbContext context) : base(context)
    {
        _identityDbContext = context;
    }

    public async Task<bool> ExistsByChainAndAddressAsync(
        ChainId chainId, 
        Address address, 
        CancellationToken cancellationToken = default)
    {
        return await ExistsByChainAndAddressCompiled(_identityDbContext, chainId, address);
    }

    public async Task<IReadOnlyList<Wallet>> GetByChainOptimizedAsync(
        ChainId chainId,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Wallet>();
        await foreach (var item in GetByChainOptimizedCompiled(_identityDbContext, chainId, skip, take)
                          .WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results.AsReadOnly();
    }


    public async Task<IReadOnlyList<Wallet>> GetRecentlyActiveAsync(
        TimeSpan withinTimespan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(withinTimespan);
        
        var wallets = await _identityDbContext.Set<Wallet>()
            .Where(w => w.LastSeenAt >= cutoffTime)
            .OrderByDescending(w => w.LastSeenAt)
            .ThenBy(w => w.Id)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
    }

    public async Task<IReadOnlyList<Wallet>> GetInactiveAsync(
        TimeSpan olderThan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(olderThan);
        
        var wallets = await _identityDbContext.Set<Wallet>()
            .Where(w => w.LastSeenAt < cutoffTime)
            .OrderBy(w => w.LastSeenAt)
            .ThenBy(w => w.Id)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
    }

    public async Task<int> GetCountOptimizedAsync(
        ChainId? chainId = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (chainId != null)
        {
            return await GetCountOptimizedCompiled(_identityDbContext, chainId.Value);
        }
        
        // For null chainId, count all wallets
        return await _identityDbContext.Set<Wallet>()
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Wallet>> SearchByAddressAsync(
        string partialAddress,
        ChainId? chainId = null,
        bool includeDeleted = false,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _identityDbContext.Set<Wallet>().AsQueryable();

        if (chainId != null)
        {
            query = query.Where(w => w.ChainId == chainId.Value);
        }

        // Using EF.Functions.Like for partial address matching
        query = query.Where(w => EF.Functions.Like(w.Address.Value, $"%{partialAddress}%"));

        var wallets = await query
            .OrderBy(w => w.Address.Value.Length) // Shorter addresses first for better UX
            .ThenBy(w => w.Address.Value)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetChainDistributionAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var distribution = await _identityDbContext.Set<Wallet>()
            .GroupBy(w => w.ChainId)
            .Select(g => new { Chain = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return distribution.ToDictionary(x => x.Chain, x => x.Count);
    }

    public async Task<IReadOnlyList<Wallet>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var walletIdList = walletIds.ToList();
        if (walletIdList.Count == 0)
            return Array.Empty<Wallet>();

        var wallets = await _identityDbContext.Set<Wallet>()
            .Where(w => walletIdList.Contains(w.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
    }

    public async Task<Wallet?> FindWalletAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        return await _identityDbContext.Set<Wallet>()
            .FirstOrDefaultAsync(w =>
                w.ChainId == chainId.Value &&
                w.Address == address,
                cancellationToken);
    }
}