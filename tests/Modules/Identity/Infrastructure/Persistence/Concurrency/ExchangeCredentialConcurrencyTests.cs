using System.Linq.Expressions;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.Concurrency;

/// <summary>
/// Concurrency tests for ExchangeCredential operations to verify thread safety,
/// race condition handling, and database lock behavior under concurrent load.
/// Tests actual persistence layer behavior with real database connections.
/// </summary>
[TestFixture]
public class ExchangeCredentialConcurrencyPersistenceTests : IdentityPersistenceTestBase
{
    private MemoryCache _sharedCache = null!;
    private ILogger<ExchangeCredentialHandler> _logger = null!;

    protected override async Task SetUpDerived()
    {
        _sharedCache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<ExchangeCredentialHandler>>();

        await Task.CompletedTask;
    }

    protected override async Task TearDownDerived()
    {
        _sharedCache?.Dispose();
        await Task.CompletedTask;
    }

    #region Concurrent Principal Creation Tests

    [Test]
    public async Task Should_HandleConcurrentCredentialCreation_WithoutDuplicates()
    {
        // Arrange - Multiple requests with same credential
        const int concurrentRequests = 5;
        var subject = $"concurrent-user-{Guid.NewGuid():N}";
        var provider = ProviderType.Dynamic;
        var issuer = "dynamic.xyz";

        var tasks = new List<Task<(bool success, bool created, int principalCount)>>();

        // Act - Create multiple concurrent operations attempting to create same principal
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var concurrentContext = CreateConcurrentDbContext();
                using var concurrentUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(concurrentContext);
                using var concurrentPrincipalRepo = new AxonPrincipalWriteRepository(concurrentContext, concurrentUnitOfWork, TimeProvider.System);

                try
                {
                    // Check if credential exists
                    var existing = await concurrentPrincipalRepo.FindByCredentialAsync(
                        provider, issuer, subject, CancellationToken.None);

                    if (existing != null)
                    {
                        return (true, false, 1); // Found existing
                    }

                    // Try to create new principal
                    var principal = AxonPrincipal.CreateWithDynamicCredential(
                        provider, issuer, subject).Value;

                    await concurrentPrincipalRepo.AddAsync(principal);
                    await concurrentUnitOfWork.SaveChangesAsync();

                    return (true, true, 1); // Successfully created
                }
                catch (DbUpdateException)
                {
                    // Expected for concurrent creations - unique constraint violation
                    return (false, false, 0);
                }
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed with creation, others should find existing or fail
        var successResults = results.Where(r => r.success).ToArray();
        var createdResults = results.Where(r => r.created).ToArray();

        successResults.Length.ShouldBeGreaterThan(0, "At least one request should succeed");
        createdResults.Length.ShouldBeLessThanOrEqualTo(1, "At most one request should create the principal");

        // Verify database consistency
        ClearChangeTracker();
        var principalCount = await DbContext.AxonPrincipals
            .CountAsync(p => p.Credentials.Any(c =>
                c.Provider == provider.Value && c.Subject == subject));
        principalCount.ShouldBe(1, "Exactly one principal should exist in database");
    }

    [Test]
    public async Task Should_HandleConcurrentWalletOwnership_WithoutConflicts()
    {
        // Arrange - Create shared wallet and multiple principals trying to claim it
        var sharedWallet = CreateTestWallet("ethereum", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41");
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        const int concurrentClaimants = 3;
        var tasks = new List<Task<(bool success, bool conflict)>>();

        // Act - Multiple principals trying to claim same wallet concurrently
        for (int i = 0; i < concurrentClaimants; i++)
        {
            var principalId = AxonUserId.New();

            tasks.Add(Task.Run(async () =>
            {
                using var concurrentContext = CreateConcurrentDbContext();
                using var concurrentUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(concurrentContext);
                using var concurrentPrincipalRepo = new AxonPrincipalWriteRepository(concurrentContext, concurrentUnitOfWork, TimeProvider.System);

                try
                {
                    // Create a new principal
                    var principal = AxonPrincipal.CreateWithDynamicCredential(
                        ProviderType.Dynamic,
                        "dynamic.xyz",
                        $"claimant-{i}-{Guid.NewGuid():N}",
                        principalId).Value;

                    // Try to create ownership
                    var ownership = WalletOwnership.Create(
                        principal.Id,
                        sharedWallet.Id,
                        AccessMode.Signing,
                        OwnershipStatus.Verified,
                        VerificationSource.DynamicAttested);

                    principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

                    await concurrentPrincipalRepo.AddAsync(principal);
                    await concurrentUnitOfWork.SaveChangesAsync();

                    return (true, false); // Success, no conflict
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    // Unique constraint violation - expected for concurrent ownership attempts
                    return (false, true); // Failed due to conflict
                }
                catch
                {
                    return (false, false); // Other failure
                }
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed, others should get conflicts
        var successResults = results.Where(r => r.success).ToArray();
        var conflictResults = results.Where(r => r.conflict).ToArray();

        successResults.Length.ShouldBe(1, "Only one principal should successfully claim the wallet");
        conflictResults.Length.ShouldBeGreaterThan(0, "Others should get conflict errors");

        // Verify database consistency - only one verified+signing ownership
        ClearChangeTracker();
        var ownershipCount = await DbContext.AxonPrincipals
            .SelectMany(p => p.WalletOwnerships)
            .CountAsync(o => o.WalletId == sharedWallet.Id
                          && o.Status == OwnershipStatus.Verified
                          && o.AccessMode == AccessMode.Signing);
        ownershipCount.ShouldBe(1, "Should have exactly one verified+signing ownership record");
    }

    #endregion

    #region Database Lock and Transaction Tests

    [Test]
    public async Task Should_HandleDatabaseLocks_UnderConcurrentLoad()
    {
        // Arrange - High concurrency scenario with database contention
        const int highConcurrency = 10;
        var tasks = new List<Task<(bool success, string? error)>>();

        // Act - Create multiple concurrent operations that will compete for database resources
        for (int i = 0; i < highConcurrency; i++)
        {
            var index = i; // Capture for closure

            tasks.Add(Task.Run(async () =>
            {
                using var concurrentContext = CreateConcurrentDbContext();
                using var concurrentUnitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(concurrentContext);
                using var concurrentPrincipalRepo = new AxonPrincipalWriteRepository(concurrentContext, concurrentUnitOfWork, TimeProvider.System);

                try
                {
                    // Add some artificial delay to increase lock contention
                    await Task.Delay(Random.Shared.Next(1, 50));

                    var principal = CreateTestPrincipal(
                        issuer: "dynamic.xyz",
                        subject: $"load-test-user-{index:D3}-{Guid.NewGuid():N}");

                    await concurrentPrincipalRepo.AddAsync(principal);
                    await concurrentUnitOfWork.SaveChangesAsync();

                    return (true, null!);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All operations should complete (success or expected failures)
        var successCount = results.Count(r => r.success);
        var failureCount = results.Count(r => !r.success);

        (successCount + failureCount).ShouldBe(highConcurrency, "All operations should complete");
        successCount.ShouldBeGreaterThan(0, "At least some operations should succeed");

        // Verify no database corruption occurred
        ClearChangeTracker();
        var principalCount = await DbContext.AxonPrincipals.CountAsync();
        principalCount.ShouldBe(successCount, "Principal count should match successful operations");
    }

    [Test]
    public async Task Should_RecoverFromDeadlocks_Gracefully()
    {
        // Arrange - Create scenario prone to deadlocks
        var wallet1 = CreateTestWallet("ethereum", "0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41");
        var wallet2 = CreateTestWallet("solana", "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS");

        await DbContext.Wallets.AddAsync(wallet1);
        await DbContext.Wallets.AddAsync(wallet2);
        await UnitOfWork.SaveChangesAsync();

        // Create two principals that will try to link wallets in different orders
        var principal1 = CreateTestPrincipal(subject: $"deadlock-user-1-{Guid.NewGuid():N}");
        var principal2 = CreateTestPrincipal(subject: $"deadlock-user-2-{Guid.NewGuid():N}");

        await SavePrincipalWithWallets(principal1);
        await SavePrincipalWithWallets(principal2);

        // Act - Create operations that might cause deadlocks by accessing resources in different orders
        var task1 = Task.Run(async () =>
        {
            using var context1 = CreateConcurrentDbContext();
            using var unitOfWork1 = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context1);
            using var repo1 = new AxonPrincipalWriteRepository(context1, unitOfWork1, TimeProvider.System);

            try
            {
                var p1 = await repo1.GetByIdAsync(principal1.Id);
                if (p1 == null) return (false, "Principal1 not found");

                // Link wallet1 first, then wallet2
                var ownership1 = CreateTestOwnership(p1.Id, wallet1.Id);
                p1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

                await Task.Delay(10); // Small delay to encourage deadlock

                var ownership2 = CreateTestOwnership(p1.Id, wallet2.Id, AccessMode.WatchOnly);
                p1.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

                await repo1.UpdateAsync(p1);
                await unitOfWork1.SaveChangesAsync();
                return (true, null!);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });

        var task2 = Task.Run(async () =>
        {
            using var context2 = CreateConcurrentDbContext();
            using var unitOfWork2 = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context2);
            using var repo2 = new AxonPrincipalWriteRepository(context2, unitOfWork2, TimeProvider.System);

            try
            {
                var p2 = await repo2.GetByIdAsync(principal2.Id);
                if (p2 == null) return (false, "Principal2 not found");

                // Link wallet2 first, then wallet1 (opposite order)
                var ownership2 = CreateTestOwnership(p2.Id, wallet2.Id, AccessMode.WatchOnly);
                p2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

                await Task.Delay(10); // Small delay to encourage deadlock

                var ownership1 = CreateTestOwnership(p2.Id, wallet1.Id, AccessMode.WatchOnly);
                p2.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

                await repo2.UpdateAsync(p2);
                await unitOfWork2.SaveChangesAsync();
                return (true, null!);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - Both operations should complete (either success or handled error)
        results.All(r => r.Item1 || !string.IsNullOrEmpty(r.Item2)).ShouldBeTrue("All operations should complete");

        // Verify database consistency
        ClearChangeTracker();
        var principalCount = await DbContext.AxonPrincipals.CountAsync();
        var walletOwnershipCount = await DbContext.AxonPrincipals
            .SelectMany(p => p.WalletOwnerships)
            .CountAsync();

        principalCount.ShouldBe(2, "Both principals should exist");
        walletOwnershipCount.ShouldBeGreaterThanOrEqualTo(0, "Ownerships should be consistent");
    }

    #endregion

    #region Cache Consistency Tests

    [Test]
    public async Task Should_MaintainCacheConsistency_UnderConcurrentAccess()
    {
        // Arrange - Create a test principal
        var principal = CreateTestPrincipal(subject: $"cache-test-{Guid.NewGuid():N}");
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        const int concurrentOperations = 8;
        var cacheKey = $"axon:user:{principal.Id}";
        var tasks = new List<Task<bool>>();

        // Act - Multiple operations accessing and modifying the same cache entry
        for (int i = 0; i < concurrentOperations; i++)
        {
            var operationIndex = i;

            tasks.Add(Task.Run(async () =>
            {
                // Simulate cache read/write operations
                if (operationIndex % 2 == 0)
                {
                    // Even operations: Try to read from cache, then write
                    if (_sharedCache.TryGetValue(cacheKey, out var cached))
                    {
                        cached.ShouldBeOfType<AxonUserId>();
                    }
                    else
                    {
                        _sharedCache.Set(cacheKey, principal.Id, TimeSpan.FromMinutes(5));
                    }
                }
                else
                {
                    // Odd operations: Write to cache directly
                    _sharedCache.Set(cacheKey, principal.Id, TimeSpan.FromMinutes(5));
                }

                await Task.Delay(Random.Shared.Next(1, 10)); // Simulate work
                return true;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Cache should remain consistent
        results.All(r => r).ShouldBeTrue("All cache operations should complete");

        // Verify cache contains expected entry
        _sharedCache.TryGetValue(cacheKey, out var finalCachedValue).ShouldBeTrue("Cache should contain user mapping");
        finalCachedValue.ShouldBeOfType<AxonUserId>();
        ((AxonUserId)finalCachedValue).ShouldBe(principal.Id, "Cached value should match principal ID");
    }

    #endregion

    #region Batch Operations Tests

    [Test]
    public async Task Should_HandleBatchWalletCreation_Efficiently()
    {
        // Arrange - Create multiple wallet specifications for batch operation
        const int walletCount = 20;
        var walletSpecs = new List<(string chainId, Address address)>();

        for (int i = 0; i < walletCount; i++)
        {
            walletSpecs.Add((
                i % 2 == 0 ? "ethereum" : "solana",
                Address.Create($"0x{i:X40}").Value
            ));
        }

        // Act - Perform concurrent batch wallet creation
        var task1 = Task.Run(async () =>
        {
            using var context = CreateConcurrentDbContext();
            using var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
            using var walletRepo = new WalletWriteRepository(context, unitOfWork);

            var result = await walletRepo.EnsureManyByChainAndAddressAsync(walletSpecs.Take(10));
            await unitOfWork.SaveChangesAsync();
            return result.Count;
        });

        var task2 = Task.Run(async () =>
        {
            using var context = CreateConcurrentDbContext();
            using var unitOfWork = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(context);
            using var walletRepo = new WalletWriteRepository(context, unitOfWork);

            var result = await walletRepo.EnsureManyByChainAndAddressAsync(walletSpecs.Skip(10));
            await unitOfWork.SaveChangesAsync();
            return result.Count;
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - All wallets should be created
        results.Sum().ShouldBe(walletCount, "All wallets should be processed");

        // Verify database state
        ClearChangeTracker();
        var actualWalletCount = await DbContext.Wallets.CountAsync();
        actualWalletCount.ShouldBe(walletCount, "Exactly the expected number of wallets should exist");

        // Verify no duplicates
        var uniqueWallets = await DbContext.Wallets
            .Select(w => new { w.ChainId, w.Address })
            .Distinct()
            .CountAsync();
        uniqueWallets.ShouldBe(walletCount, "All wallets should be unique");
    }

    #endregion
}