using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

public sealed class IdentityReadDbContext : ReadDbContextBase<IdentityModule>, IIdentityReadDbContext
{
    public IdentityReadDbContext(DbContextOptions<IdentityReadDbContext> options, ILogger<IdentityReadDbContext>? logger = null)
        : base(options, logger)
    {
        // Additional read-specific optimizations
        Database.SetCommandTimeout(TimeSpan.FromSeconds(60)); // 60-second timeout for read operations
    }

    public override string ModuleName => "identity";

    // Implement IIdentityReadDbContext interface
    // Aggregate roots
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    // These are owned entities in the write model, but we need to expose them for read queries
    // They will be configured properly via the same configuration classes
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();
    
    // Keep old property names for compatibility
    public DbSet<AxonPrincipal> AxonPrincipals => Principals;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Apply the same configurations as the write context
        // This ensures consistency between read and write models
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Only configure indexes for aggregate roots
        // Skip owned entities as they are configured through their owners
        var aggregateRootTypes = new[] { typeof(AxonPrincipal), typeof(Wallet) };

        // Essential indexes for query performance on aggregate roots only
        var principalEntity = modelBuilder.Model.FindEntityType(typeof(AxonPrincipal));
        if (principalEntity != null && !principalEntity.IsOwned())
        {
            modelBuilder.Entity<AxonPrincipal>()
                .HasIndex(p => new { p.UpdatedAt, p.Id })
                .HasDatabaseName("ix_principals_updated_at_id");
        }

        var walletEntity = modelBuilder.Model.FindEntityType(typeof(Wallet));
        if (walletEntity != null && !walletEntity.IsOwned())
        {
            modelBuilder.Entity<Wallet>()
                .HasIndex(w => new { w.ChainId, w.UpdatedAt })
                .HasDatabaseName("ix_wallets_chain_updated");
        }

        // Don't call base implementation as it tries to configure all entities
        // including owned ones which causes the conflict
    }
}