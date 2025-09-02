using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Read-side DbContext for Identity module
/// </summary>
public sealed class IdentityReadDbContext : ReadDbContextBase<IdentityModule>, IIdentityReadDbContext
{
    public IdentityReadDbContext(DbContextOptions<IdentityReadDbContext> options, ILogger<IdentityReadDbContext>? logger = null) 
        : base(options, logger) 
    {
        // Additional read-specific optimizations
        Database.SetCommandTimeout(TimeSpan.FromSeconds(30)); // 30-second timeout for read operations
    }

    public override string ModuleName => "identity";

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        // Add read-specific indexes for AxonPrincipal queries if needed
        // For example:
        // modelBuilder.Entity<AxonPrincipal>()
        //     .HasIndex(ap => ap.Email)
        //     .HasDatabaseName("ix_axon_principals_email");

        // Add read-specific indexes for Wallet queries if needed
        // For example:
        // modelBuilder.Entity<Wallet>()
        //     .HasIndex(w => w.Address)
        //     .HasDatabaseName("ix_wallets_address");

        // Call base implementation for standard timestamp indexes
        base.ConfigureReadModelOptimizations(modelBuilder);
    }
}