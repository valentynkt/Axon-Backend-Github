using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Write-side DbContext for Identity module
/// </summary>
public sealed class IdentityWriteDbContext : WriteDbContextBase<IdentityModule>, IIdentityWriteDbContext
{
    private readonly ILogger<IdentityWriteDbContext> _logger;

    public IdentityWriteDbContext(DbContextOptions<IdentityWriteDbContext> options, ILogger<IdentityWriteDbContext>? logger = null)
        : base(options, logger)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<IdentityWriteDbContext>.Instance;
    }

    public override string ModuleName => "identity";

    // Implement IIdentityWriteDbContext interface
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    // Owned entities - exposed temporarily until services are properly refactored
    // WARNING: These should NOT be used directly in production code - access through aggregate root only
    // This is only to satisfy the interface requirement during the refactoring process
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();

    // Keep old property names for compatibility
    public DbSet<AxonPrincipal> AxonPrincipals => Principals;

    /// <summary>
    /// Override SaveChangesAsync to implement auto-revocation of pending ownerships
    /// when a principal gains verified+signing ownership of a wallet.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Process auto-revocation before saving
        await ProcessAutoRevocationsAsync(cancellationToken);

        // Call base SaveChangesAsync
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Processes auto-revocation for wallet ownerships based on domain events.
    /// When a principal gains verified+signing ownership, all other principals'
    /// pending ownerships for that wallet are automatically revoked.
    /// </summary>
    private async Task ProcessAutoRevocationsAsync(CancellationToken cancellationToken)
    {
        // Check ALL tracked principals regardless of state
        // After the first save, principals remain tracked but may not be marked as Modified
        var trackedPrincipals = ChangeTracker.Entries<AxonPrincipal>()
            .Select(e => e.Entity)
            .ToList();

        // Find all principals with new verified+signing ownership events
        var principalsWithNewVerifiedOwnerships = trackedPrincipals
            .Where(p => p.DomainEvents.Any(evt =>
                evt is OwnershipChangedEvent oce &&
                oce.ChangeType == "verified_signing_added" &&
                oce.Metadata != null &&
                oce.Metadata.ContainsKey("RequiresAutoRevocation") &&
                oce.Metadata["RequiresAutoRevocation"] == "true"))
            .ToList();

        if (principalsWithNewVerifiedOwnerships.Count == 0)
            return;

        foreach (var winnerPrincipal in principalsWithNewVerifiedOwnerships)
        {
            // Extract wallet IDs that need auto-revocation from the domain events
            var walletIdsToRevoke = winnerPrincipal.DomainEvents
                .OfType<OwnershipChangedEvent>()
                .Where(evt => evt.ChangeType == "verified_signing_added" &&
                             evt.Metadata != null &&
                             evt.Metadata.ContainsKey("RequiresAutoRevocation") &&
                             evt.Metadata["RequiresAutoRevocation"] == "true")
                .Select(evt => evt.WalletId)
                .Distinct()
                .ToList();

            foreach (var walletId in walletIdsToRevoke)
            {
                _logger.LogDebug("Processing auto-revocation for wallet {WalletId} excluding principal {PrincipalId}", walletId, winnerPrincipal.Id);
                await RevokeCompetingOwnershipsAsync(walletId, winnerPrincipal.Id, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Revokes all pending ownerships for a wallet except for the specified principal.
    /// This maintains the exclusivity constraint for verified+signing ownership.
    /// </summary>
    private async Task RevokeCompetingOwnershipsAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken cancellationToken)
    {
        // Find all principals with pending ownership of this wallet (excluding the winner)
        var principalsWithPendingOwnership = await Principals
            .Include(p => p.WalletOwnerships)
            .Where(p => p.Id != excludePrincipalId &&
                       p.WalletOwnerships.Any(wo => wo.WalletId == walletId &&
                                                   wo.Status == OwnershipStatus.Pending &&
                                                   !wo.IsDeleted))
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} principals with pending ownership for wallet {WalletId}",
            principalsWithPendingOwnership.Count, walletId);

        // Revoke pending ownerships through the aggregate method
        foreach (var principal in principalsWithPendingOwnership)
        {
            var revokeResult = principal.RevokePendingOwnershipsForWallet(
                walletId,
                "Auto-revoked due to exclusivity constraint");

            _logger.LogDebug("Revoked {Count} pending ownerships for principal {PrincipalId} on wallet {WalletId}",
                revokeResult.Value, principal.Id, walletId);

            // The aggregate method handles raising the appropriate domain events
            // and the result indicates how many ownerships were revoked
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        base.ConfigureConventions(configurationBuilder);

        // Configure Address value objects globally
        configurationBuilder.Properties<Address>()
            .HaveConversion<Address.EfCoreValueConverter>()
            .HaveMaxLength(200);

        // Note: Strong ID conversions and column names are configured in individual entity configurations
        // as they require specific per-property configuration
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityWriteDbContext).Assembly);

        // Child entities are now configured as owned types in AxonPrincipalConfiguration
        // They automatically inherit concurrency control from the parent aggregate

    }
}

/// <summary>
/// Design-time factory for IdentityWriteDbContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class IdentityWriteDbContextFactory : DesignTimeDbContextFactoryBase<IdentityWriteDbContext>
{
    protected override IdentityWriteDbContext CreateNewInstance(DbContextOptions<IdentityWriteDbContext> options) =>
        new(options);

    protected override void ConfigureProvider(DbContextOptionsBuilder<IdentityWriteDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, opt =>
        {
            opt.MigrationsAssembly(typeof(IdentityWriteDbContext).Assembly.FullName);
            opt.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
        });
}