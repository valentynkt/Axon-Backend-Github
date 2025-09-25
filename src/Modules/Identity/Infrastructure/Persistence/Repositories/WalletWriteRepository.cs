using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class WalletWriteRepository : EfWriteRepository<Wallet, WalletId>, IWalletWriteRepository
{
    private readonly IWriteUnitOfWork<IdentityModule> _unitOfWork;

    public WalletWriteRepository(IdentityWriteDbContext context, IWriteUnitOfWork<IdentityModule> unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork<IdentityModule> UnitOfWork => _unitOfWork;

    private DbSet<Wallet> GetWalletWithIncludes()
    {
        return DbSet;  // No includes needed for MVP - WalletTags removed
    }

    public override async Task<Wallet?> GetByIdAsync(WalletId id, CancellationToken ct = default)
    {
        return await GetWalletWithIncludes()
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Wallet?> GetByChainAndAddressAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        return await GetWalletWithIncludes()
            .FirstOrDefaultAsync(w => w.ChainId == chainId.Value && w.Address == address, cancellationToken);
    }

    public async Task<IReadOnlyList<Wallet>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds, 
        CancellationToken cancellationToken = default)
    {
        var walletIdsList = walletIds.ToList();
        
        if (walletIdsList.Count == 0)
            return Array.Empty<Wallet>();

        var wallets = await GetWalletWithIncludes()
            .Where(w => walletIdsList.Contains(w.Id))
            .ToListAsync(cancellationToken);

        return wallets.AsReadOnly();
    }

    public async Task<IReadOnlyDictionary<(string chainId, Address address), WalletId>> EnsureManyByChainAndAddressAsync(
        IEnumerable<(string chainId, Address address)> walletSpecs,
        CancellationToken ct = default)
    {
        var specs = walletSpecs.ToList();
        if (specs.Count == 0)
            return new Dictionary<(string chainId, Address address), WalletId>();

        var result = new Dictionary<(string chainId, Address address), WalletId>();

        // First check tracked wallets in the change tracker
        var trackedWallets = new Dictionary<(string chainId, Address address), WalletId>();
        foreach (var spec in specs)
        {
            var trackedWallet = DbSet.Local
                .FirstOrDefault(w => w.ChainId == spec.chainId && w.Address.Value == spec.address.Value);

            if (trackedWallet != null)
            {
                trackedWallets[spec] = trackedWallet.Id;
                result[spec] = trackedWallet.Id;
            }
        }

        // Get specs that are not already tracked
        var specsToQuery = specs.Where(spec => !trackedWallets.ContainsKey(spec)).ToList();

        if (specsToQuery.Count == 0)
        {
            return result;
        }

        // Use individual queries for each spec to avoid complex LINQ translation issues
        // This is more straightforward and avoids EF Core translation problems
        var existingWallets = new List<dynamic>();

        foreach (var spec in specsToQuery)
        {
            var wallet = await DbSet
                .Where(w => w.ChainId == spec.chainId && w.Address == spec.address)
                .Select(w => new { w.Id, w.ChainId, w.Address })
                .FirstOrDefaultAsync(ct);

            if (wallet != null)
            {
                existingWallets.Add(wallet);
            }
        }

        // Map existing wallets to their specs
        var existingWalletMap = new Dictionary<(string chainId, Address address), WalletId>();
        foreach (var wallet in existingWallets)
        {
            var spec = ((string)wallet.ChainId, (Address)wallet.Address);
            existingWalletMap[spec] = wallet.Id;
            result[spec] = wallet.Id;
        }

        // Identify missing wallets that need to be created
        var missingSpecs = specsToQuery.Where(spec => !existingWalletMap.ContainsKey(spec)).ToList();

        // Create missing wallets - EF retry strategy will handle any race conditions
        foreach (var spec in missingSpecs)
        {
            var wallet = Wallet.Create(null, spec.chainId, spec.address);
            await DbSet.AddAsync(wallet, ct);
            result[spec] = wallet.Id;
        }

        // Save changes to persist new wallets to the database
        if (missingSpecs.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return result;
    }

    public async Task<Wallet> UpsertWalletAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        // First try to find existing wallet by dual-key lookup
        var existingWallet = await DbSet
            .FirstOrDefaultAsync(w =>
                w.ChainId == chainId.Value &&
                w.Address == address,
                cancellationToken);

        if (existingWallet != null)
        {
            return existingWallet;
        }

        // Create new wallet if not found
        var newWallet = Wallet.Create(null, chainId.Value, address);
        await DbSet.AddAsync(newWallet, cancellationToken);

        // Save changes to persist the new wallet
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return newWallet;
    }
}