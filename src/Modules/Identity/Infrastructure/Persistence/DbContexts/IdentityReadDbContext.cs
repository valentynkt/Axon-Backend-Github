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
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();
    
    // Keep old property names for compatibility
    public DbSet<AxonPrincipal> AxonPrincipals => Principals;

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Essential indexes for query performance
        modelBuilder.Entity<AxonPrincipal>()
            .HasIndex(p => new { p.UpdatedAt, p.Id })
            .HasDatabaseName("ix_principals_updated_at_id");

        modelBuilder.Entity<Wallet>()
            .HasIndex(w => new { w.ChainId, w.UpdatedAt })
            .HasDatabaseName("ix_wallets_chain_updated");

        // Call base implementation for standard timestamp indexes
        base.ConfigureReadModelOptimizations(modelBuilder);
    }
}