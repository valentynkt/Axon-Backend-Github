using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AxonPrincipalWriteRepository : EfWriteRepository<AxonPrincipal, AxonUserId>, IAxonPrincipalWriteRepository
{
    private readonly IWriteUnitOfWork<IdentityModule> _unitOfWork;

    public AxonPrincipalWriteRepository(IdentityWriteDbContext context, IWriteUnitOfWork<IdentityModule> unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork<IdentityModule> UnitOfWork => _unitOfWork;

    // Override removed - the base implementation handles concurrency correctly
    // The previous detaching logic was interfering with EF Core's change tracking
    // and preventing proper concurrency control from working

    private IQueryable<AxonPrincipal> GetPrincipalWithIncludes()
    {
        return DbSet
            .AsSplitQuery()
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships.Where(w => !w.IsDeleted))
            .Include(p => p.PrincipalChainDefaults.Where(pc => !pc.IsDeleted));
    }

    public override async Task<AxonPrincipal?> GetByIdAsync(AxonUserId id, CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludes()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludes()
            .FirstOrDefaultAsync(p => p.Credentials.Any(c =>
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

        var principals = await GetPrincipalWithIncludes()
            .Where(p => p.WalletOwnerships.Any(wo =>
                walletIdsList.Contains(wo.WalletId) &&
                wo.AccessMode == AccessMode.Signing &&
                wo.Status == OwnershipStatus.Verified))
            .ToListAsync(ct);

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

    public async Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await DbContext.Set<AxonPrincipal>()
            .AnyAsync(p => p.Credentials.Any(c =>
                c.Provider == providerType.Value &&
                c.Issuer == issuer &&
                c.Subject == subject), ct);
    }

    public async Task<int> RevokePendingOwnershipsForWalletAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default)
    {
        // Find all principals with pending ownership of this wallet (excluding the specified principal)
        var principalsWithPendingOwnership = await GetPrincipalWithIncludes()
            .Where(p => p.Id != excludePrincipalId &&
                       p.WalletOwnerships.Any(wo => wo.WalletId == walletId &&
                                                   wo.Status == OwnershipStatus.Pending &&
                                                   !wo.IsDeleted))
            .ToListAsync(ct);

        int revokedCount = 0;

        foreach (var principal in principalsWithPendingOwnership)
        {
            // Find the specific pending ownerships for this wallet
            var pendingOwnerships = principal.WalletOwnerships
                .Where(wo => wo.WalletId == walletId &&
                           wo.Status == OwnershipStatus.Pending &&
                           !wo.IsDeleted)
                .ToList();

            foreach (var ownership in pendingOwnerships)
            {
                var revokeResult = ownership.Revoke("Auto-revoked due to exclusivity constraint");
                if (revokeResult.IsSuccess)
                {
                    revokedCount++;
                }
            }
        }

        return revokedCount;
    }

}