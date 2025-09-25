using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests;

/// <summary>
/// Concurrency tests for Wallet aggregate.
/// Tests concurrent access mode changes and ownership status updates
/// which are critical for security boundaries.
/// </summary>
[TestFixture]
public class WalletConcurrencyTests : ConcurrencyTestBase<IdentityWriteDbContext>
{
    private IdentityWriteDbContext _setupContext = null!;
    private EfUnitOfWork<IdentityWriteDbContext, IdentityModule> _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        _setupContext = CreateContext(CreateContextOptions());
        _unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(_setupContext);

        await _setupContext.Database.EnsureDeletedAsync();
        await _setupContext.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _unitOfWork?.Dispose();
        await _setupContext.DisposeAsync();
    }

    protected override DbContextOptions<IdentityWriteDbContext> CreateContextOptions()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEntityFrameworkNpgsql();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        return CreateDbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(IdentityWriteDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
            })
            .UseInternalServiceProvider(serviceProvider)
            .Options;
    }

    protected override IdentityWriteDbContext CreateContext(DbContextOptions<IdentityWriteDbContext> options)
    {
        return new IdentityWriteDbContext(options);
    }

    protected override Task<TId> CreateAndSaveTestAggregateAsync<TAggregate, TId>()
    {
        throw new NotImplementedException("Use specific test wallet creation methods");
    }

    #region Critical Security Boundary Tests


    [Test]
    public async Task UpdateOwnershipStatus_ConcurrentStatusChanges_ShouldThrowConcurrencyException()
    {
        // Arrange - Verification process vs. revocation happening simultaneously
        var wallet = await CreateAndSaveWalletAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<Wallet, WalletId>(
            wallet.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
                return new WalletWriteRepository(context, unitOfWork);
            },
            w1 => w1.UpdateOwnershipStatus(OwnershipStatus.Verified),
            w2 => w2.UpdateOwnershipStatus(OwnershipStatus.Revoked)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task UpdateAccessMode_WhileChangingOwnership_ShouldThrowConcurrencyException()
    {
        // Arrange - Complex scenario: Access mode change while ownership is being updated
        var wallet = await CreateAndSaveWalletAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<Wallet, WalletId>(
            wallet.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
                return new WalletWriteRepository(context, unitOfWork);
            },
            w1 => w1.UpdateAccessMode(AccessMode.WatchOnly),
            w2 => w2.UpdateOwnershipStatus(OwnershipStatus.Verified)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task DetachedWallet_ConcurrentUpdates_ShouldThrowConcurrencyException()
    {
        // Arrange - Test with detached entities (API scenario)
        var wallet = await CreateAndSaveWalletAsync();

        // Act & Assert
        var result = await SimulateConcurrentDetachedUpdatesAsync<Wallet, WalletId>(
            wallet.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
                return new WalletWriteRepository(context, unitOfWork);
            },
            w1 => w1.UpdateAccessMode(AccessMode.Signing),
            w2 => w2.UpdateAccessMode(AccessMode.WatchOnly)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task RapidAccessModeChanges_ShouldMaintainConsistency()
    {
        // Arrange
        var wallet = await CreateAndSaveWalletAsync();
        var accessModes = new[]
        {
            AccessMode.WatchOnly,
            AccessMode.Signing,
            AccessMode.WatchOnly,
            AccessMode.WatchOnly,
            AccessMode.Signing
        };

        // Act - Simulate rapid changes from same admin session
        using var context = CreateContext(CreateContextOptions());
        using var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
        using var repo = new WalletWriteRepository(context, unitOfWork);

        var loaded = await repo.GetByIdAsync(wallet.Id);
        loaded.ShouldNotBeNull();

        foreach (var mode in accessModes)
        {
            var result = loaded.UpdateAccessMode(mode);
            result.IsSuccess.ShouldBeTrue();

            await repo.UpdateAsync(loaded);
            await context.SaveChangesAsync();
        }

        // Note: Wallet no longer exposes AccessMode directly - it's managed through WalletOwnership

        // Now try concurrent update with stale version - should fail
        var staleWallet = await CreateStaleWalletInstanceAsync(wallet.Id);

        using var staleContext = CreateContext(CreateContextOptions());
        using var staleUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(staleContext);
        using var staleRepo = new WalletWriteRepository(staleContext, staleUnitOfWork);

        await staleRepo.UpdateAsync(staleWallet);

        AssertConcurrencyException(async () =>
            await staleContext.SaveChangesAsync());
    }

    [Test]
    public async Task MultipleWallets_ConcurrentBatchUpdates_ShouldDetectConflicts()
    {
        // Arrange - Create multiple wallets for same chain
        var wallets = await CreateMultipleWalletsAsync(3);

        // Create two contexts for concurrent batch operations
        var (context1, context2) = await CreateConcurrentContextsAsync();

        try
        {
            using var unitOfWork1 = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context1);
            using var unitOfWork2 = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context2);
            using var repo1 = new WalletWriteRepository(context1, unitOfWork1);
            using var repo2 = new WalletWriteRepository(context2, unitOfWork2);

            // Load all wallets in both contexts
            var batch1 = new List<Wallet>();
            var batch2 = new List<Wallet>();

            foreach (var wallet in wallets)
            {
                var loaded1 = await repo1.GetByIdAsync(wallet.Id);
                var loaded2 = await repo2.GetByIdAsync(wallet.Id);

                loaded1.ShouldNotBeNull();
                loaded2.ShouldNotBeNull();

                batch1.Add(loaded1);
                batch2.Add(loaded2);
            }

            // Batch 1: Set all to ReadOnly
            foreach (var w in batch1)
            {
                w.UpdateAccessMode(AccessMode.WatchOnly);
            }

            // Batch 2: Set all to FullControl
            foreach (var w in batch2)
            {
                w.UpdateAccessMode(AccessMode.Signing);
            }

            // Update and save first batch
            await repo1.UpdateRangeAsync(batch1);
            await context1.SaveChangesAsync();

            // Try to update second batch - should fail
            await repo2.UpdateRangeAsync(batch2);

            AssertConcurrencyException(async () =>
                await context2.SaveChangesAsync());
        }
        finally
        {
            await context1.DisposeAsync();
            await context2.DisposeAsync();
        }
    }

    #endregion

    #region Helper Methods

    private async Task<Wallet> CreateAndSaveWalletAsync()
    {
        var wallet = Wallet.Create(
            WalletId.New(),
            "1", // Ethereum mainnet
            Address.Create("0x742d35Cc6634C0532925a3b844Bc0e7E31BE6E38").Value,
            DateTime.UtcNow);

        using var repository = new WalletWriteRepository(_setupContext, _unitOfWork);
        await repository.AddAsync(wallet);
        await _setupContext.SaveChangesAsync();

        return wallet;
    }

    private async Task<List<Wallet>> CreateMultipleWalletsAsync(int count)
    {
        var wallets = new List<Wallet>();
        using var repository = new WalletWriteRepository(_setupContext, _unitOfWork);

        for (int i = 0; i < count; i++)
        {
            var address = $"0x{i:D40}"; // Generate unique addresses
            var wallet = Wallet.Create(
                WalletId.New(),
                "1", // All on Ethereum mainnet
                Address.Create(address).Value,
                DateTime.UtcNow);

            wallets.Add(wallet);
            await repository.AddAsync(wallet);
        }

        await _setupContext.SaveChangesAsync();
        return wallets;
    }

    private async Task<Wallet> CreateStaleWalletInstanceAsync(WalletId id)
    {
        // Load wallet in a temporary context and immediately detach
        using var tempContext = CreateContext(CreateContextOptions());
        using var tempUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(tempContext);
        using var tempRepo = new WalletWriteRepository(tempContext, tempUnitOfWork);

        var loaded = await tempRepo.GetByIdAsync(id);
        loaded.ShouldNotBeNull();

        // Detach to simulate a stale instance
        tempContext.Entry(loaded).State = EntityState.Detached;

        // Make a change without saving (simulating stale client state)
        loaded.UpdateAccessMode(AccessMode.WatchOnly);

        return loaded;
    }

    #endregion
}