using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.Configurations;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Unified DbContext for Identity module - handles both read and write operations.
/// Inherits from DbContextBase which provides both write capabilities and read optimizations.
/// </summary>
public sealed class IdentityDbContext : DbContextBase<IdentityModule>, IIdentityDbContext
{
    private readonly ILogger<IdentityDbContext> _logger;

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ILogger<IdentityDbContext>? logger = null)
        : base(options, logger)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<IdentityDbContext>.Instance;
    }

    public override string ModuleName => "identity";

    // Aggregate roots
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

    // Query<T>() method now inherited from DbContextBase

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

        // CRITICAL: Explicitly ignore AxonUserAuth entity - it belongs to IdentityContext only
        // AxonUserAuth is managed by ASP.NET Core Identity and uses IdentityContext
        modelBuilder.Ignore<AxonUserAuth>();

        // Apply configurations EXCEPT AxonUserAuthConfiguration (which is for IdentityContext only)
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentityDbContext).Assembly,
            t => t != typeof(AxonUserAuthConfiguration));

        // Child entities are now configured as owned types in AxonPrincipalConfiguration
        // They automatically inherit concurrency control from the parent aggregate

        // Apply read optimizations (indexes for query performance)
        ConfigureReadOptimizations(modelBuilder);
    }

    /// <summary>
    /// Configure read-optimized indexes for query performance.
    /// These indexes improve performance for common read queries without affecting write operations.
    /// </summary>
    private static void ConfigureReadOptimizations(ModelBuilder modelBuilder)
    {
        // Only configure indexes for aggregate roots
        // Skip owned entities as they are configured through their owners
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
    }
}

/// <summary>
/// Design-time factory for IdentityDbContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class IdentityDbContextFactory : DesignTimeDbContextFactoryBase<IdentityDbContext>
{
    protected override IdentityDbContext CreateNewInstance(DbContextOptions<IdentityDbContext> options) =>
        new(options);

    protected override void ConfigureProvider(DbContextOptionsBuilder<IdentityDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, opt =>
        {
            opt.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
            opt.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
        });
}
