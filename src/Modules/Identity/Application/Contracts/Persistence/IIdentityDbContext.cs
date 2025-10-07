using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Unified database context for Identity module.
/// Supports both read and write operations with appropriate optimizations.
/// </summary>
public interface IIdentityDbContext : IDbContext
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }

    // Aggregate roots
    DbSet<AxonPrincipal> Principals { get; }
    DbSet<Wallet> Wallets { get; }

    // Owned entities - exposed for specific use cases (e.g., querying, owned entity access)
    DbSet<IdentityCredential> Credentials { get; }
    DbSet<WalletOwnership> WalletOwnerships { get; }
    DbSet<PrincipalChainDefault> PrincipalChainDefaults { get; }

    /// <summary>
    /// No-tracking queryable for read operations (optimized for queries).
    /// Use this for read-only operations to improve performance.
    /// </summary>
    IQueryable<TEntity> Query<TEntity>() where TEntity : class;
}
