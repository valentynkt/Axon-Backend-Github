using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for wallet ownership read-only operations supporting deterministic principal resolution.
/// All ownership modifications must go through the AxonPrincipal aggregate root to maintain DDD boundaries.
/// </summary>
public sealed class WalletOwnershipRepository : IWalletOwnershipRepository
{
    private readonly IdentityReadDbContext _readContext;

    public WalletOwnershipRepository(IdentityReadDbContext readContext)
    {
        _readContext = readContext;
    }

    public async Task<IReadOnlyList<WalletOwnershipWithPrincipal>> FindActiveOwnershipsByWalletAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        // Join WalletOwnership with AxonPrincipal to get both entities
        var result = await (from wo in _readContext.Set<WalletOwnership>()
                           join p in _readContext.Set<AxonPrincipal>() on wo.PrincipalId equals p.Id
                           where wo.WalletId == walletId && wo.Status != OwnershipStatus.Revoked
                           select new { Ownership = wo, Principal = p })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return result.Select(r => new WalletOwnershipWithPrincipal(r.Ownership, r.Principal))
                    .ToList()
                    .AsReadOnly();
    }


    public async Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default)
    {
        var walletIdList = walletIds.ToList();
        if (walletIdList.Count == 0)
            return new Dictionary<WalletId, AxonPrincipal>();

        // Join WalletOwnership with AxonPrincipal and include related collections
        var ownerships = await (from wo in _readContext.Set<WalletOwnership>()
                               join p in _readContext.Set<AxonPrincipal>()
                                   .Include(p => p.WalletOwnerships)
                                   .Include(p => p.Credentials)
                                   on wo.PrincipalId equals p.Id
                               where walletIdList.Contains(wo.WalletId) &&
                                     wo.Status == OwnershipStatus.Verified &&
                                     wo.AccessMode == AccessMode.Signing
                               select new { wo.WalletId, Principal = p })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ownerships.ToDictionary(o => o.WalletId, o => o.Principal);
    }
}