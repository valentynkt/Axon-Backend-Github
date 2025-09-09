using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityReadDbContext : DbContext, IIdentityReadDbContext
{
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();

    public IdentityReadDbContext(DbContextOptions<IdentityReadDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.LazyLoadingEnabled = false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        
        modelBuilder.ApplyConfiguration(new AxonPrincipalConfiguration());
        modelBuilder.ApplyConfiguration(new WalletConfiguration());
        modelBuilder.ApplyConfiguration(new CredentialConfiguration());
        modelBuilder.ApplyConfiguration(new WalletOwnershipConfiguration());
        modelBuilder.ApplyConfiguration(new PrincipalChainDefaultConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}