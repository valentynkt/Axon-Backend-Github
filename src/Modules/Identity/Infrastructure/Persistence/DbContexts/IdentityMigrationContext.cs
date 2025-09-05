using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Simplified DbContext for migrations only
/// </summary>
public class IdentityMigrationContext : DbContext
{
    public IdentityMigrationContext(DbContextOptions<IdentityMigrationContext> options) : base(options)
    {
    }

    public DbSet<AxonPrincipal> AxonPrincipals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        
        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}

/// <summary>
/// Design-time factory for migrations
/// </summary>
public class IdentityMigrationContextFactory : IDesignTimeDbContextFactory<IdentityMigrationContext>
{
    public IdentityMigrationContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityMigrationContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=axon_chat;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention();
        
        return new IdentityMigrationContext(optionsBuilder.Options);
    }
}