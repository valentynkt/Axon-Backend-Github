using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Write-side DbContext for Identity module  
/// </summary>
public sealed class IdentityWriteDbContext : WriteDbContextBase<IdentityModule>, IIdentityWriteDbContext
{
    public IdentityWriteDbContext(DbContextOptions<IdentityWriteDbContext> options, ILogger<IdentityWriteDbContext>? logger = null) 
        : base(options, logger)
    {
    }

    public override string ModuleName => "identity";

    // Implement IIdentityWriteDbContext interface
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();
    
    // Keep old property names for compatibility
    public DbSet<AxonPrincipal> AxonPrincipals => Principals;

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
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Fix concurrency issue with child entities that are added to existing aggregates
        // When new child entities (PrincipalChainDefault, WalletOwnership) are added to tracked aggregates,
        // EF Core tries to apply concurrency control to them, but they don't have xmin values yet
        HandleChildEntityConcurrency();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void HandleChildEntityConcurrency()
    {
        var addedChildEntities = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added &&
                       (e.Entity is PrincipalChainDefault || e.Entity is WalletOwnership))
            .ToList();

        foreach (var entry in addedChildEntities)
        {
            // For new child entities, don't track the Version property for concurrency
            // They will get their xmin value after insertion
            var versionProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(IVersioned.Version));
            if (versionProperty != null)
            {
                versionProperty.IsModified = false;
                // Set to default value for new entities
                versionProperty.CurrentValue = (uint)0;
            }
        }
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