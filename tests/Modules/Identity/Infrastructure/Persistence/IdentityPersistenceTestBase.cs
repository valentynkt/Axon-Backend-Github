using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;
using Npgsql;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Base class for all Identity persistence tests providing shared infrastructure and utilities.
/// Uses PostgreSQL with Testcontainers for realistic database testing.
/// Ensures proper test isolation and provides common assertion helpers.
/// </summary>
public abstract class IdentityPersistenceTestBase : PostgreSqlTestBase
{
    protected IdentityDbContext DbContext { get; set; } = null!;
    protected AxonPrincipalWriteRepository PrincipalRepository { get; set; } = null!;
    protected WalletWriteRepository WalletRepository { get; set; } = null!;
    protected EfUnitOfWork<IdentityDbContext, IdentityModule> UnitOfWork { get; set; } = null!;

    [SetUp]
    public async Task SetUpBase()
    {
        var options = CreateDbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            })
            .Options;

        DbContext = new IdentityDbContext(options);
        UnitOfWork = new EfUnitOfWork<IdentityDbContext, IdentityModule>(DbContext);
        PrincipalRepository = new AxonPrincipalWriteRepository(DbContext, UnitOfWork, TimeProvider.System);
        WalletRepository = new WalletWriteRepository(DbContext, UnitOfWork);

        // Create schema using EF model configuration for tests
        await DbContext.Database.EnsureCreatedAsync();

        // Allow child classes to perform additional setup
        await SetUpDerived();
    }

    [TearDown]
    public async Task TearDownBase()
    {
        try
        {
            // Allow child classes to perform cleanup
            await TearDownDerived();
        }
        finally
        {
            // Clean up database state for next test
            await CleanupDatabaseAsync(DbContext);

            // Ensure resources are cleaned up even if child cleanup fails
            UnitOfWork?.Dispose();
            PrincipalRepository?.Dispose();
            WalletRepository?.Dispose();
            await DbContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Override this method to perform additional setup in derived test classes.
    /// </summary>
    protected virtual Task SetUpDerived() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform additional cleanup in derived test classes.
    /// </summary>
    protected virtual Task TearDownDerived() => Task.CompletedTask;

    /// <summary>
    /// Cleans up Identity database state between tests.
    /// </summary>
    protected override async Task CleanupDatabaseAsync(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE identity.principal_chain_default CASCADE;
                TRUNCATE TABLE identity.wallet_ownership CASCADE;
                TRUNCATE TABLE identity.credential CASCADE;
                TRUNCATE TABLE identity.wallet CASCADE;
                TRUNCATE TABLE identity.axon_principal CASCADE;
            ");
        }
        catch
        {
            // If truncate fails, try dropping and recreating with EF model
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }

    #region Test Data Creation Helpers

    /// <summary>
    /// Creates a test principal with Dynamic credential.
    /// </summary>
    protected static AxonPrincipal CreateTestPrincipal(
        string? issuer = null,
        string? subject = null,
        AxonUserId? id = null)
    {
        var providerType = ProviderType.Create("dynamic").Value;
        return AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            issuer ?? "https://app.dynamic.xyz/test",
            subject ?? $"test-user-{Guid.NewGuid():N}",
            id).Value;
    }

    /// <summary>
    /// Creates a test wallet with the specified chain and address.
    /// </summary>
    protected static Wallet CreateTestWallet(
        string chainId = "ethereum",
        string? address = null,
        WalletId? id = null)
    {
        return Wallet.Create(
            id,
            chainId,
            Address.Create(address ?? $"0x{Guid.NewGuid():N}").Value);
    }

    /// <summary>
    /// Creates a test wallet ownership relationship.
    /// </summary>
    protected static WalletOwnership CreateTestOwnership(
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Verified)
    {
        return WalletOwnership.Create(principalId, walletId, accessMode, status);
    }

    /// <summary>
    /// Creates multiple test wallets for different chains.
    /// </summary>
    protected static (Wallet eth, Wallet polygon, Wallet bsc) CreateMultiChainWallets()
    {
        return (
            CreateTestWallet("ethereum", "0x1111111111111111111111111111111111111111"),
            CreateTestWallet("polygon", "0x2222222222222222222222222222222222222222"),
            CreateTestWallet("binance", "0x3333333333333333333333333333333333333333")
        );
    }

    #endregion

    #region Persistence Helpers

    /// <summary>
    /// Saves a principal and associated wallets to the database.
    /// </summary>
    protected async Task<AxonPrincipal> SavePrincipalWithWallets(
        AxonPrincipal principal,
        params Wallet[] wallets)
    {
        ArgumentNullException.ThrowIfNull(wallets);

        // Save wallets first
        foreach (var wallet in wallets)
        {
            await DbContext.Wallets.AddAsync(wallet);
        }

        // Save principal
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        return principal;
    }

    /// <summary>
    /// Creates and saves a complete test scenario with principal, wallets, and ownerships.
    /// </summary>
    protected async Task<(AxonPrincipal principal, Wallet[] wallets)> CreateCompleteTestScenario()
    {
        var principal = CreateTestPrincipal();
        var (eth, polygon, bsc) = CreateMultiChainWallets();
        var wallets = new[] { eth, polygon, bsc };

        await SavePrincipalWithWallets(principal, wallets);

        // Add wallet ownerships
        var ownership1 = CreateTestOwnership(principal.Id, eth.Id);
        var ownership2 = CreateTestOwnership(principal.Id, polygon.Id);
        var ownership3 = CreateTestOwnership(principal.Id, bsc.Id, AccessMode.WatchOnly);

        var linkResult1 = principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
        var linkResult2 = principal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
        var linkResult3 = principal.LinkWalletOwnership(ownership3, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        linkResult1.IsSuccess.ShouldBeTrue();
        linkResult2.IsSuccess.ShouldBeTrue();
        linkResult3.IsSuccess.ShouldBeTrue();

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        return (principal, wallets);
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Verifies that entities are properly persisted to the database.
    /// </summary>
    protected async Task AssertEntityPersistedAsync<T>() where T : class
    {
        DbContext.ChangeTracker.Clear();
        var dbSet = DbContext.Set<T>();
        var count = await dbSet.CountAsync();
        count.ShouldBeGreaterThan(0, $"Entity of type {typeof(T).Name} should be persisted");
    }

    /// <summary>
    /// Verifies that a principal and all its navigation properties are properly loaded.
    /// </summary>
    protected async Task AssertPrincipalCompletelyLoadedAsync(AxonUserId principalId)
    {
        DbContext.ChangeTracker.Clear();
        var principal = await PrincipalRepository.GetByIdAsync(principalId);

        principal.ShouldNotBeNull();
        principal.Credentials.ShouldNotBeNull();
        principal.WalletOwnerships.ShouldNotBeNull();
        principal.PrincipalChainDefaults.ShouldNotBeNull();
    }

    /// <summary>
    /// Asserts that a concurrency exception should be thrown.
    /// PostgreSQL properly enforces concurrency control with optimistic concurrency.
    /// </summary>
    protected static void AssertConcurrencyConflict(Func<Task> action)
    {
        Should.Throw<DbUpdateConcurrencyException>(action);
    }

    /// <summary>
    /// Asserts that a PostgreSQL unique constraint violation occurs.
    /// Validates the specific constraint name that was violated.
    /// </summary>
    protected static async Task AssertUniqueConstraintViolation(Func<Task> action, string? expectedConstraintName = null)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(action);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL constraint violation");

        // PostgreSQL unique violation error code
        postgresException.SqlState.ShouldBe("23505", "Expected unique constraint violation");

        // Validate specific constraint name if provided
        if (!string.IsNullOrEmpty(expectedConstraintName))
        {
            postgresException.ConstraintName.ShouldBe(expectedConstraintName,
                $"Expected constraint '{expectedConstraintName}' to be violated");
        }
    }

    #endregion

    #region Database State Helpers

    /// <summary>
    /// Clears the change tracker to ensure fresh reads from database.
    /// </summary>
    protected void ClearChangeTracker()
    {
        DbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Forces a fresh database query by clearing the change tracker first.
    /// </summary>
    protected async Task<T?> QueryFreshAsync<T>(Func<Task<T?>> query) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);

        ClearChangeTracker();
        return await query();
    }

    /// <summary>
    /// Gets the current change tracker state for debugging.
    /// </summary>
    protected string GetChangeTrackerState()
    {
        var entries = DbContext.ChangeTracker.Entries().ToList();
        return $"Tracked entities: {entries.Count}, " +
               $"Added: {entries.Count(e => e.State == EntityState.Added)}, " +
               $"Modified: {entries.Count(e => e.State == EntityState.Modified)}, " +
               $"Deleted: {entries.Count(e => e.State == EntityState.Deleted)}";
    }

    /// <summary>
    /// Creates a separate DbContext for concurrent transaction testing.
    /// </summary>
    protected IdentityDbContext CreateConcurrentDbContext()
    {
        var options = CreateDbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            })
            .Options;
        return new IdentityDbContext(options);
    }

    /// <summary>
    /// Updates an entity with proper concurrency handling and retry logic.
    /// Preserves the original Version for optimistic concurrency control.
    /// </summary>
    protected async Task<TAggregate> UpdateWithConcurrencyHandling<TAggregate, TId>(
        TAggregate aggregate,
        IWriteRepository<TAggregate, TId> repository,
        IWriteUnitOfWork unitOfWork,
        int maxRetries = 3)
        where TAggregate : class, IAggregateRoot<TId>
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        var retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                // Preserve the entity's original version for concurrency control
                var entry = DbContext.Entry(aggregate);
                if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                {
                    // Ensure the original version is preserved for concurrency checks
                    if (entry.Property("Version").OriginalValue == null)
                    {
                        entry.Property("Version").OriginalValue = entry.Property("Version").CurrentValue;
                    }
                }

                await repository.UpdateAsync(aggregate);
                await unitOfWork.SaveChangesAsync();
                return aggregate;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (retryCount >= maxRetries - 1)
                    throw; // Rethrow on final attempt

                retryCount++;

                // Refresh entity and retry
                var entry = DbContext.Entry(aggregate);
                await entry.ReloadAsync();
            }
        }

        return aggregate;
    }

    #endregion
}