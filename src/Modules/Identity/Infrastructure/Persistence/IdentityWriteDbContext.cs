using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.EntityConfigurations;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityWriteDbContext : WriteDbContextBase<IdentityWriteDbContext>, IIdentityWriteDbContext
{
    public DbSet<AxonPrincipal> Principals => Set<AxonPrincipal>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<IdentityCredential> Credentials => Set<IdentityCredential>();
    public DbSet<WalletOwnership> WalletOwnerships => Set<WalletOwnership>();
    public DbSet<PrincipalChainDefault> PrincipalChainDefaults => Set<PrincipalChainDefault>();

    public override string ModuleName => "identity";

    public IdentityWriteDbContext(
        DbContextOptions<IdentityWriteDbContext> options,
        ILogger<IdentityWriteDbContext>? logger = null) 
        : base(options, logger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AxonPrincipalConfiguration());
        modelBuilder.ApplyConfiguration(new WalletConfiguration());
        modelBuilder.ApplyConfiguration(new CredentialConfiguration());
        modelBuilder.ApplyConfiguration(new WalletOwnershipConfiguration());
        modelBuilder.ApplyConfiguration(new PrincipalChainDefaultConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}