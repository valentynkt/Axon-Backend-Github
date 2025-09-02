using Axon.Modules.Identity.Application.Abstractions.Persistence;
using Axon.Modules.Identity.Application.Specifications.Wallets;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for wallet queries using Entity Framework Core.
/// Supports specification-based queries through Ardalis.Specification integration.
/// Includes compiled queries for optimal performance on hot paths.
/// </summary>
internal sealed class WalletReadRepository : EfSpecificationReadRepository<Wallet>, IWalletReadRepository
{
    private readonly IdentityReadDbContext _identityDbContext;

    /// <summary>
    /// Compiled query for checking wallet existence by chain and address - hot path optimization
    /// </summary>
    private static readonly Func<IdentityReadDbContext, ChainId, Address, Task<bool>> 
        ExistsByChainAndAddressCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, ChainId chainId, Address address) =>
                context.Set<Wallet>()
                    .Any(w => w.Chain == chainId && w.Address == address && w.DeletedAt == null));

    /// <summary>
    /// Compiled query for fetching wallets by chain with pagination - hot path optimization
    /// </summary>
    private static readonly Func<IdentityReadDbContext, ChainId, bool, int, int, IAsyncEnumerable<Wallet>> 
        GetByChainOptimizedCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, ChainId chainId, bool includeDeleted, int skip, int take) =>
                context.Set<Wallet>()
                    .Where(w => w.Chain == chainId && (includeDeleted || w.DeletedAt == null))
                    .OrderBy(w => w.FirstSeenAt)
                    .ThenBy(w => w.Id)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking());

    /// <summary>
    /// Compiled query for counting wallets by chain - optimized for count operations
    /// </summary>
    private static readonly Func<IdentityReadDbContext, ChainId?, bool, Task<int>> 
        GetCountOptimizedCompiled = EF.CompileAsyncQuery(
            (IdentityReadDbContext context, ChainId? chainId, bool includeDeleted) =>
                context.Set<Wallet>()
                    .Where(w => (chainId == null || w.Chain == chainId) && (includeDeleted || w.DeletedAt == null))
                    .Count());

    public WalletReadRepository(IdentityReadDbContext context) : base(context)
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
        var page = new Page(
            Number: (skip / take) + 1,
            Size: take);
            
        var spec = new WalletsForChainSpec(chainId, page);
        // NOTE: includeDeleted and includeOwnership parameters removed as Wallet doesn't support soft delete or ownership navigation properties
        
        return await ListAsync(spec, cancellationToken);
    }

    public async Task<IReadOnlyList<Wallet>> GetByTagOptimizedAsync(
        Tag tag,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var wallets = await _identityDbContext.Set<Wallet>()
            .Where(w => w.Tags.Contains(tag) && (includeDeleted || w.DeletedAt == null))
            .OrderBy(w => w.FirstSeenAt)
            .ThenBy(w => w.Id)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
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
            .Where(w => w.LastSeenAt >= cutoffTime && (includeDeleted || w.DeletedAt == null))
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
            .Where(w => w.LastSeenAt < cutoffTime && (includeDeleted || w.DeletedAt == null))
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
        if (chainId.HasValue)
        {
            var countSpec = new WalletsForChainCountSpec(chainId.Value);
            // NOTE: includeDeleted parameter removed as Wallet doesn't support soft delete
            return await CountAsync(countSpec, cancellationToken);
        }
        
        // For null chainId, use the original compiled query for now
        return await GetCountOptimizedCompiled(_identityDbContext, chainId, includeDeleted);
    }

    public async Task<IReadOnlyList<Wallet>> SearchByAddressAsync(
        string partialAddress,
        ChainId? chainId = null,
        bool includeDeleted = false,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _identityDbContext.Set<Wallet>()
            .Where(w => (includeDeleted || w.DeletedAt == null));

        if (chainId.HasValue)
        {
            query = query.Where(w => w.Chain == chainId.Value);
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
            .Where(w => includeDeleted || w.DeletedAt == null)
            .GroupBy(w => w.Chain.Value)
            .Select(g => new { Chain = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return distribution.ToDictionary(x => x.Chain, x => x.Count);
    }
}