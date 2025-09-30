using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.Configurations;
using BuildingBlocks.Core.Diagnostics.Exceptions;
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
    /// Override SaveChangesAsync - owned entities now properly configured with ValueGeneratedNever.
    /// No special handling needed as EF Core will correctly INSERT new owned entities.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Simply delegate to base - owned entities are now correctly configured
        return await base.SaveChangesAsync(cancellationToken);
    }


    private static void ThrowConcurrencyException(DbUpdateConcurrencyException ex)
    {
        var firstEntry = ex.Entries.Count > 0 ? ex.Entries[0] : null;
        if (firstEntry == null)
        {
            throw ex; // Re-throw original if no entries
        }

        var entityType = firstEntry.Entity.GetType().Name;

        // Use EF Core's metadata to get primary key values
        var keyValues = firstEntry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => firstEntry.CurrentValues[p]?.ToString() ?? "null")
            .ToArray() ?? ["unknown"];
        var entityId = string.Join(", ", keyValues);

        // With PostgreSQL xmin, version details are managed by the database
        throw new ConcurrencyException(
            $"The {entityType} with key [{entityId}] has been modified by another user. Please refresh and try again.",
            entityType,
            entityId,
            "xmin", // Using PostgreSQL xmin for concurrency
            "xmin");
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

        // CRITICAL: Explicitly ignore AxonUserAuth entity - it belongs to IdentityContext only
        // AxonUserAuth is managed by ASP.NET Core Identity and uses IdentityContext
        modelBuilder.Ignore<AxonUserAuth>();

        // Apply configurations EXCEPT AxonUserAuthConfiguration (which is for IdentityContext only)
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IdentityWriteDbContext).Assembly,
            t => t != typeof(AxonUserAuthConfiguration));

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