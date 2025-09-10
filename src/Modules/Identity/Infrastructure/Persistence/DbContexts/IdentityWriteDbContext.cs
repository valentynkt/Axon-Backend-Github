using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityWriteDbContext).Assembly);
        modelBuilder.ToSnakeCaseTables();
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
        builder.UseNpgsql(connectionString, opt => opt.MigrationsAssembly(typeof(IdentityWriteDbContext).Assembly.FullName))
               .UseSnakeCaseNamingConvention();
}