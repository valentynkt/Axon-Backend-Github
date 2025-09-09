using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

public interface IIdentityReadDbContext : IReadDbContext<IdentityModule>
{
    DbSet<AxonPrincipal> Principals { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<IdentityCredential> Credentials { get; }
    DbSet<WalletOwnership> WalletOwnerships { get; }
    DbSet<PrincipalChainDefault> PrincipalChainDefaults { get; }
}