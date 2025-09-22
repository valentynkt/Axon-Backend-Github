using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for wallet ownership operations supporting deterministic principal resolution.
/// </summary>
public sealed class WalletOwnershipRepository : IWalletOwnershipRepository
{
    private readonly IdentityWriteDbContext _writeContext;
    private readonly IdentityReadDbContext _readContext;
    private readonly IWriteUnitOfWork<IdentityModule> _unitOfWork;

    public WalletOwnershipRepository(
        IdentityWriteDbContext writeContext,
        IdentityReadDbContext readContext,
        IWriteUnitOfWork<IdentityModule> unitOfWork)
    {
        _writeContext = writeContext;
        _readContext = readContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<WalletOwnership>> FindActiveOwnershipsByWalletAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        var ownerships = await _readContext.Set<WalletOwnership>()
            .Include(o => o.Principal)
            .Where(o => o.WalletId == walletId && o.Status != OwnershipStatus.Revoked)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ownerships.AsReadOnly();
    }

    public async Task<WalletOwnership?> FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(
        string chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        // First find wallets with matching chain and address across all network environments
        // Since WalletOwnership doesn't have a Wallet navigation property, we need to join
        var ownership = await (from wo in _readContext.Set<WalletOwnership>()
                               join w in _readContext.Set<Wallet>() on wo.WalletId equals w.Id
                               where w.ChainId == chainId &&
                                     w.Address == address &&
                                     wo.Status == OwnershipStatus.Verified &&
                                     wo.AccessMode == AccessMode.Signing &&
                                     !wo.IsDeleted
                               select wo)
            .Include(o => o.Principal)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return ownership;
    }

    public async Task<WalletOwnership> CreateOwnershipAsync(
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status,
        VerificationSource verificationSource,
        CancellationToken cancellationToken = default)
    {
        var ownership = WalletOwnership.Create(
            principalId,
            walletId,
            accessMode,
            status,
            verificationSource);

        await _writeContext.Set<WalletOwnership>().AddAsync(ownership, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ownership;
    }

    public async Task RevokeOwnershipAsync(
        WalletOwnershipId ownershipId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var ownership = await _writeContext.Set<WalletOwnership>()
            .FirstOrDefaultAsync(o => o.Id == ownershipId, cancellationToken);

        if (ownership == null)
            return;

        ownership.UpdateStatus(OwnershipStatus.Revoked);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default)
    {
        var walletIdList = walletIds.ToList();
        if (walletIdList.Count == 0)
            return new Dictionary<WalletId, AxonPrincipal>();

        var ownerships = await _readContext.Set<WalletOwnership>()
            .Include(o => o.Principal)
                .ThenInclude(p => p.WalletOwnerships)
            .Include(o => o.Principal)
                .ThenInclude(p => p.Credentials)
            .Where(o =>
                walletIdList.Contains(o.WalletId) &&
                o.Status == OwnershipStatus.Verified &&
                o.AccessMode == AccessMode.Signing)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ownerships.ToDictionary(o => o.WalletId, o => o.Principal);
    }
}