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
            .FirstOrDefaultAsync(w => w.ChainId == chainId && w.Address == address, cancellationToken);
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

        // Strategy: Query for existing wallets efficiently
        var existingWallets = new Dictionary<(string chainId, Address address), WalletId>();

        foreach (var spec in specs)
        {
            // First check if the wallet is already being tracked by EF (in change tracker)
            var trackedWallet = DbSet.Local
                .FirstOrDefault(w => w.ChainId == spec.chainId && w.Address == spec.address);

            if (trackedWallet != null)
            {
                existingWallets[spec] = trackedWallet.Id;
                result[spec] = trackedWallet.Id;
                continue;
            }

            // If not in change tracker, check the database
            var existingWallet = await DbSet
                .Where(w => w.ChainId == spec.chainId && w.Address == spec.address)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync(ct);

            if (existingWallet != null)
            {
                existingWallets[spec] = existingWallet.Id;
                result[spec] = existingWallet.Id;
            }
        }

        // Collect specs for wallets that don't exist yet (neither in change tracker nor database)
        var missingSpecs = specs.Where(spec => !existingWallets.ContainsKey(spec)).ToList();

        // Create missing wallets - EF retry strategy will handle any race conditions
        foreach (var spec in missingSpecs)
        {
            var wallet = Wallet.Create(null, spec.chainId, spec.address);
            await DbSet.AddAsync(wallet, ct);
            result[spec] = wallet.Id;
        }

        return result;
    }
}