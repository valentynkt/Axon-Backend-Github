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
    private readonly TimeProvider _timeProvider;

    public AxonPrincipalWriteRepository(IdentityWriteDbContext context, IWriteUnitOfWork<IdentityModule> unitOfWork, TimeProvider timeProvider) : base(context)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public IWriteUnitOfWork<IdentityModule> UnitOfWork => _unitOfWork;

    // No override needed - the base EfWriteRepository handles concurrency correctly
    // The key insight: EF Core automatically detects changes in owned entities
    // and the xmin concurrency token is checked whenever ANY part of the aggregate changes

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
        // CRITICAL: Check ChangeTracker first to prevent duplicate Principal creation during EF Core ExecutionStrategy retries
        // ExecutionStrategy can retry the entire transaction, including handler execution, without resetting the DbContext.
        // This causes the handler to create a new Principal instance and call AddAsync again, resulting in duplicate INSERTs.
        // By checking ChangeTracker first, we return the already-tracked Principal from the first execution attempt.
        var trackedPrincipal = DbContext.ChangeTracker.Entries<AxonPrincipal>()
            .FirstOrDefault(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added &&
                                e.Entity.Credentials.Any(c =>
                                    c.Provider == providerType.Value &&
                                    c.Issuer == issuer &&
                                    c.Subject == subject))
            ?.Entity;

        if (trackedPrincipal is not null)
            return trackedPrincipal;

        // Not in ChangeTracker, query database
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
            // Use the aggregate method to revoke pending ownerships
            var revokeResult = principal.RevokePendingOwnershipsForWallet(walletId, _timeProvider, "Auto-revoked due to exclusivity constraint");
            if (revokeResult.IsSuccess)
            {
                revokedCount += revokeResult.Value;
            }
        }

        return revokedCount;
    }

    public async Task<List<AxonPrincipal>> GetPrincipalsWithPendingOwnershipAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludes()
            .Where(p => p.Id != excludePrincipalId &&
                       p.WalletOwnerships.Any(wo => wo.WalletId == walletId &&
                                                   wo.Status == OwnershipStatus.Pending &&
                                                   !wo.IsDeleted))
            .ToListAsync(ct);
    }

    public async Task<bool> HasVerifiedSigningOwnershipAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default)
    {
        return await GetPrincipalWithIncludes()
            .AnyAsync(p => p.Id != excludePrincipalId &&
                          p.WalletOwnerships.Any(wo => wo.WalletId == walletId &&
                                                      wo.Status == OwnershipStatus.Verified &&
                                                      wo.AccessMode == AccessMode.Signing &&
                                                      !wo.IsDeleted), ct);
    }
}