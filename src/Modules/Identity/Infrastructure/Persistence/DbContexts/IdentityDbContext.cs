using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Write-side DbContext for Identity module
/// </summary>
public sealed class IdentityDbContext : WriteDbContextBase<IdentityModule>, IIdentityWriteDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ILogger<IdentityDbContext>? logger = null) 
        : base(options, logger)
    {
    }

    public override string ModuleName => "identity";

    public DbSet<AxonPrincipal> AxonPrincipals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ToSnakeCaseTables();
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
        builder.UseNpgsql(connectionString, opt => opt.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
               .UseSnakeCaseNamingConvention();
}