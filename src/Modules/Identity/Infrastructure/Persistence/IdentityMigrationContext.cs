using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Migration-only context for EF Core migrations.
/// </summary>
public sealed class IdentityMigrationContext : DbContext
{
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();

    public IdentityMigrationContext(DbContextOptions<IdentityMigrationContext> options) : base(options)
    {
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

/// <summary>
/// Design-time factory for EF migrations.
/// </summary>
public class IdentityMigrationContextFactory : IDesignTimeDbContextFactory<IdentityMigrationContext>
{
    public IdentityMigrationContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityMigrationContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=axon_dev;Username=postgres;Password=password");
        
        return new IdentityMigrationContext(optionsBuilder.Options);
    }
}