using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AxonPrincipalWriteRepository : EfWriteRepository<AxonPrincipal, AxonId>, IAxonPrincipalWriteRepository
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public AxonPrincipalWriteRepository(IdentityWriteDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;

    private IQueryable<AxonPrincipal> GetPrincipalWithIncludesForUpdate()
    {
        return DbSet
            .AsSplitQuery()
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships)
            .Include(p => p.PrincipalChainDefaults);
    }

    private IQueryable<AxonPrincipal> GetPrincipalWithIncludesForRead()
    {
        return DbSet
            .AsSplitQuery()
            .AsNoTracking()
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships)
            .Include(p => p.PrincipalChainDefaults);
    }

    private IQueryable<AxonPrincipal> GetPrincipalForRead()
    {
        return DbSet
            .Include(p => p.Credentials)
            .AsNoTracking();
    }

    public override async Task<AxonPrincipal?> GetByIdAsync(AxonId id, CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludesForUpdate()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await GetPrincipalForRead()
            .FirstOrDefaultAsync(p => p.Credentials.Any(c => 
                c.Provider == providerType.Value && 
                c.Issuer == issuer && 
                c.Subject == subject), ct);
    }

    public async Task<AxonPrincipal?> FindByWalletIdAsync(
        WalletId walletId,
        CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludesForRead()
            .FirstOrDefaultAsync(p => p.WalletOwnerships.Any(wo => wo.WalletId == walletId), ct);
    }

    public async Task<bool> IsWalletOwnedByVerifiedSigningAsync(
        WalletId walletId,
        CancellationToken ct = default)
    {
        return await DbSet
            .AnyAsync(p => p.WalletOwnerships.Any(wo => 
                wo.WalletId == walletId && 
                wo.AccessMode == AccessMode.Signing), ct);
    }

    public async Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await DbSet
            .AnyAsync(p => p.Credentials.Any(c => 
                c.Provider == providerType.Value && 
                c.Issuer == issuer && 
                c.Subject == subject), ct);
    }

    public async Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default)
    {
        var walletIdsList = walletIds.ToList();
        
        if (walletIdsList.Count == 0)
            return new Dictionary<WalletId, AxonPrincipal>();

        var principals = await GetPrincipalWithIncludesForRead()
            .Where(p => p.WalletOwnerships.Any(wo =>
                walletIdsList.Contains(wo.WalletId) &&
                wo.AccessMode == AccessMode.Signing))
            .ToListAsync(ct);

        var result = new Dictionary<WalletId, AxonPrincipal>();

        foreach (var principal in principals)
        {
            foreach (var ownership in principal.WalletOwnerships)
            {
                if (walletIdsList.Contains(ownership.WalletId) && 
                    ownership.AccessMode == AccessMode.Signing)
                {
                    result[ownership.WalletId] = principal;
                }
            }
        }

        return result;
    }

}