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

    public override async Task<AxonPrincipal> UpdateAsync(AxonPrincipal aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // Step 1: Completely clear all tracked entities first to avoid any conflicts
        DbContext.ChangeTracker.Clear();

        // Step 2: Find the existing principal in the database and update its properties
        var existingPrincipal = DbContext.Set<AxonPrincipal>().Find(aggregate.Id);
        if (existingPrincipal != null)
        {
            // Update principal properties
            DbContext.Entry(existingPrincipal).CurrentValues.SetValues(aggregate);

            // Step 3: Handle navigation properties manually
            await HandleNavigationPropertiesForExistingPrincipal(existingPrincipal, aggregate);
        }
        else
        {
            // This shouldn't happen in UpdateAsync, but handle it gracefully
            throw new InvalidOperationException($"Principal with ID {aggregate.Id} not found in database during update.");
        }

        return aggregate;
    }
    

    /// <summary>
    /// Handles navigation properties for an existing tracked principal by synchronizing
    /// with the new aggregate data.
    /// </summary>
    private async Task HandleNavigationPropertiesForExistingPrincipal(AxonPrincipal existingPrincipal, AxonPrincipal newAggregate)
    {
        // Handle PrincipalChainDefaults - clear existing and add new ones
        var existingDefaults = DbContext.Set<PrincipalChainDefault>()
            .Where(pcd => pcd.PrincipalId == existingPrincipal.Id)
            .ToList();

        // Remove existing defaults
        DbContext.Set<PrincipalChainDefault>().RemoveRange(existingDefaults);

        // Force save removal before adding new entities to prevent ID conflicts
        await DbContext.SaveChangesAsync();

        // Create fresh entities to avoid any tracking conflicts with domain entities
        foreach (var chainDefault in newAggregate.PrincipalChainDefaults)
        {
            // Create a new instance to avoid any tracking conflicts
            var newDefault = PrincipalChainDefault.Create(
                chainDefault.PrincipalId,
                chainDefault.ChainId,
                chainDefault.WalletId);

            DbContext.Set<PrincipalChainDefault>().Add(newDefault);
        }

        // Handle WalletOwnerships - clear existing and add new ones
        var existingOwnerships = DbContext.Set<WalletOwnership>()
            .Where(wo => wo.PrincipalId == existingPrincipal.Id)
            .ToList();

        // Remove existing ownerships
        DbContext.Set<WalletOwnership>().RemoveRange(existingOwnerships);

        // Force save removal before adding new entities to prevent ID conflicts
        await DbContext.SaveChangesAsync();

        // Add new ownerships from the aggregate
        foreach (var ownership in newAggregate.WalletOwnerships)
        {
            // Create a new instance to avoid any tracking conflicts
            var newOwnership = WalletOwnership.Create(
                ownership.PrincipalId,
                ownership.WalletId,
                ownership.AccessMode,
                ownership.Status);

            DbContext.Set<WalletOwnership>().Add(newOwnership);
        }

        // Handle IdentityCredentials - clear existing and add new ones
        var existingCredentials = DbContext.Set<IdentityCredential>()
            .Where(ic => ic.PrincipalId == existingPrincipal.Id)
            .ToList();

        // Remove existing credentials
        DbContext.Set<IdentityCredential>().RemoveRange(existingCredentials);

        // Add the actual entities from the aggregate (preserving their domain-created IDs)
        foreach (var credential in newAggregate.Credentials)
        {
            // Add the entity as-is from the domain to preserve its ID
            DbContext.Set<IdentityCredential>().Add(credential);
        }
    }


    private IQueryable<AxonPrincipal> GetPrincipalWithIncludes()
    {
        return DbSet
            .AsSplitQuery()
            .Include(p => p.Credentials)
            .Include(p => p.WalletOwnerships)
            .Include(p => p.PrincipalChainDefaults);
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

}