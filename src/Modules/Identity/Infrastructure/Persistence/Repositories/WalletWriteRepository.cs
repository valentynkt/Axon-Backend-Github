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
    private readonly IWriteUnitOfWork _unitOfWork;

    public WalletWriteRepository(IdentityWriteDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;

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

        var context = (IdentityWriteDbContext)Context;
        var result = new Dictionary<(string chainId, Address address), WalletId>();

        // Stage 1: Get candidate wallets using simple Contains operations (EF Core friendly)
        var chainIdValues = specs.Select(s => s.chainId).Distinct().ToList();
        var addresses = specs.Select(s => s.address).Distinct().ToList();

        var candidateWallets = await DbSet
            .Where(w => chainIdValues.Contains(w.ChainId) && addresses.Contains(w.Address))
            .Select(w => new { w.Id, w.ChainId, w.Address })
            .ToListAsync(ct);

        // Stage 2: Filter candidates for exact (chainId, address) pairs in memory
        var existingWallets = candidateWallets
            .Where(w => specs.Any(s => s.chainId.Equals(w.ChainId, StringComparison.OrdinalIgnoreCase) && s.address.Equals(w.Address)))
            .ToList();

        // Map existing wallets to the result
        foreach (var existing in existingWallets)
        {
            var spec = specs.First(s => s.chainId.Equals(existing.ChainId, StringComparison.OrdinalIgnoreCase) && s.address.Equals(existing.Address));
            result[spec] = existing.Id;
        }

        // Create missing wallets
        var missingSpecs = specs.Where(spec => !result.ContainsKey(spec)).ToList();
        foreach (var spec in missingSpecs)
        {
            var wallet = Wallet.Create(null, spec.chainId, spec.address);
            await DbSet.AddAsync(wallet, ct);
            result[spec] = wallet.Id;
        }

        // Let UnitOfWork handle the SaveChanges - don't call it directly
        return result;
    }
}