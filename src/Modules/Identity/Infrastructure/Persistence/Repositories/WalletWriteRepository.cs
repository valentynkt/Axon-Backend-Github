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

    private IQueryable<Wallet> GetWalletWithIncludes()
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
            .FirstOrDefaultAsync(w => w.Chain == chainId && w.Address == address, cancellationToken);
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
}