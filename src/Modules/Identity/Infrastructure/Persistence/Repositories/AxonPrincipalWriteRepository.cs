using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Application.Specifications.Wallets;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AxonPrincipalWriteRepository : EfWriteRepository<AxonPrincipal, AxonId>, IAxonPrincipalWriteRepository
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public AxonPrincipalWriteRepository(IdentityDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;

    public async Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await DbSet
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.Credentials.Any(c => 
                c.ProviderType == providerType && 
                c.Issuer == issuer && 
                c.Subject == subject), ct);
    }

    public async Task<AxonPrincipal?> FindByWalletIdAsync(
        WalletId walletId,
        CancellationToken ct = default)
    {
        return await DbSet
            .Include(p => p.WalletOwnerships)
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
                c.ProviderType == providerType && 
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

        var principals = await DbSet
            .Include(p => p.WalletOwnerships)
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

    public async Task<IReadOnlyCollection<AxonPrincipal>> FindByEmailHashAsync(
        EmailHash emailHash,
        CancellationToken ct = default)
    {
        var principals = await DbSet
            .Where(p => p.PrimaryEmailHash == emailHash)
            .ToListAsync(ct);

        return principals.AsReadOnly();
    }
}