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
        // Query principals with their wallet ownerships
        // Since WalletOwnership is an owned entity, we must query through the aggregate root
        var principals = await _readContext.Principals
            .Include(p => p.WalletOwnerships)
            .Where(p => p.WalletOwnerships.Any(wo => wo.WalletId == walletId && wo.Status != OwnershipStatus.Revoked))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<WalletOwnershipWithPrincipal>();
        foreach (var principal in principals)
        {
            var ownerships = principal.WalletOwnerships
                .Where(wo => wo.WalletId == walletId && wo.Status != OwnershipStatus.Revoked);

            foreach (var ownership in ownerships)
            {
                result.Add(new WalletOwnershipWithPrincipal(ownership, principal));
            }
        }

        return result.AsReadOnly();
    }


    public async Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default)
    {
        var walletIdList = walletIds.ToList();
        if (walletIdList.Count == 0)
            return new Dictionary<WalletId, AxonPrincipal>();

        // Query principals that have verified signing ownerships for the specified wallets
        // Since WalletOwnership is an owned entity, we must query through the aggregate root
        var principals = await _readContext.Principals
            .Include(p => p.WalletOwnerships)
            .Include(p => p.Credentials)
            .Where(p => p.WalletOwnerships.Any(wo =>
                walletIdList.Contains(wo.WalletId) &&
                wo.Status == OwnershipStatus.Verified &&
                wo.AccessMode == AccessMode.Signing))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new Dictionary<WalletId, AxonPrincipal>();
        foreach (var principal in principals)
        {
            var verifiedSigningOwnerships = principal.WalletOwnerships
                .Where(wo => walletIdList.Contains(wo.WalletId) &&
                            wo.Status == OwnershipStatus.Verified &&
                            wo.AccessMode == AccessMode.Signing);

            foreach (var ownership in verifiedSigningOwnerships)
            {
                // Only include the first principal for each wallet (should be unique due to constraints)
                if (!result.ContainsKey(ownership.WalletId))
                {
                    result[ownership.WalletId] = principal;
                }
            }
        }

        return result;
    }
}