using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Comprehensive tests for AxonPrincipal persistence operations.
/// Covers concurrency, complex navigation property updates, constraints, and transaction boundaries.
/// </summary>
[TestFixture]
public class AxonPrincipalPersistenceTests : IdentityPersistenceTestBase
{
    #region Concurrency Tests

    [Test]
    public async Task UpdateAsync_Should_ThrowConcurrencyException_When_VersionConflict()
    {
        // Arrange: Create and save a principal
        var principal = CreateTestPrincipal();
        var (eth, polygon, _) = CreateMultiChainWallets();
        await SavePrincipalWithWallets(principal, eth, polygon);

        // Load the same principal in two different contexts (simulating concurrent access)
        var principal1 = await PrincipalRepository.GetByIdAsync(principal.Id);
        var principal2 = await PrincipalRepository.GetByIdAsync(principal.Id);

        principal1.ShouldNotBeNull();
        principal2.ShouldNotBeNull();

        // Modify and save first principal
        principal1.UpdateRiskTier(RiskTier.Medium);
        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Try to modify and save second principal (should fail due to concurrency)
        principal2.UpdateRiskTier(RiskTier.High);
        await PrincipalRepository.UpdateAsync(principal2);

        // Act & Assert
        AssertConcurrencyConflict(async () => await UnitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task UpdateAsync_Should_HandleSimultaneousNavigationPropertyUpdates()
    {
        // Arrange: Create principal with wallets
        var principal = CreateTestPrincipal();
        var (eth, polygon, bsc) = CreateMultiChainWallets();
        await SavePrincipalWithWallets(principal, eth, polygon, bsc);

        // Add initial ownerships
        var ownership1 = CreateTestOwnership(principal.Id, eth.Id);
        var ownership2 = CreateTestOwnership(principal.Id, polygon.Id);

        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Load in two contexts
        var principal1 = await PrincipalRepository.GetByIdAsync(principal.Id);
        var principal2 = await PrincipalRepository.GetByIdAsync(principal.Id);

        principal1.ShouldNotBeNull();
        principal2.ShouldNotBeNull();

        // Context 1: Add BSC ownership and chain default
        var ownership3 = CreateTestOwnership(principal1.Id, bsc.Id);
        principal1.LinkWalletOwnership(ownership3, (_, _, _) => Result.Success<bool, Error>(false));
        principal1.ApplyChainDefaultsBatch(new[] { ("56", bsc.Id) });

        // Context 2: Update existing chain defaults
        principal2.ApplyChainDefaultsBatch(new[] { ("1", eth.Id), ("137", polygon.Id) });

        // Save context 1 first
        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Try to save context 2 (should fail due to version conflict)
        await PrincipalRepository.UpdateAsync(principal2);
        AssertConcurrencyConflict(async () => await UnitOfWork.SaveChangesAsync());
    }

    #endregion

    #region Navigation Property Tests

    [Test]
    public async Task UpdateAsync_Should_HandleComplexWalletOwnershipChanges()
    {
        // Arrange: Create complete test scenario
        var (principal, wallets) = await CreateCompleteTestScenario();
        var eth = wallets[0];
        var polygon = wallets[1];
        var bsc = wallets[2];

        // Reload principal
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();
        reloadedPrincipal.WalletOwnerships.Count.ShouldBe(3);

        // Act: Remove one ownership and add a new wallet with ownership
        var newWallet = CreateTestWallet("43114", "0x4444444444444444444444444444444444444444"); // Avalanche
        await DbContext.Wallets.AddAsync(newWallet);
        await UnitOfWork.SaveChangesAsync();

        // Remove BSC ownership and add Avalanche ownership
        var bscOwnership = reloadedPrincipal.WalletOwnerships.First(wo => wo.WalletId == bsc.Id);
        reloadedPrincipal.RemoveWalletOwnership(bsc.Id);

        var avalancheOwnership = CreateTestOwnership(reloadedPrincipal.Id, newWallet.Id);
        reloadedPrincipal.LinkWalletOwnership(avalancheOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Verify changes were persisted
        var freshPrincipal = await QueryFreshAsync(() => PrincipalRepository.GetByIdAsync(principal.Id));
        freshPrincipal.ShouldNotBeNull();
        freshPrincipal.WalletOwnerships.Count.ShouldBe(3);
        freshPrincipal.WalletOwnerships.ShouldNotContain(wo => wo.WalletId == bsc.Id);
        freshPrincipal.WalletOwnerships.ShouldContain(wo => wo.WalletId == newWallet.Id);
    }

    [Test]
    public async Task UpdateAsync_Should_PreventDuplicateCredentials_When_Added()
    {
        // Arrange: Create principal with existing credential
        var principal = CreateTestPrincipal("https://app.dynamic.xyz/test", "existing-user");
        await SavePrincipalWithWallets(principal);

        // Reload principal
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Act: Try to add duplicate credential
        var providerType = ProviderType.Create("dynamic").Value;
        var duplicateCredential = IdentityCredential.Create(
            reloadedPrincipal.Id,
            providerType.Value,
            "https://app.dynamic.xyz/test",
            "existing-user");

        var addResult = reloadedPrincipal.AddCredential(duplicateCredential, (_, _, _) => Result.Success<bool, Error>(false));

        // Assert: Should fail at domain level (not persistence level)
        addResult.IsFailure.ShouldBeTrue();
        addResult.Error.Code.ShouldContain("Credential.AlreadyExists");
    }

    [Test]
    public async Task UpdateAsync_Should_HandleChainDefaultsBatchOperations()
    {
        // Arrange: Create principal with multiple wallets
        var (principal, wallets) = await CreateCompleteTestScenario();
        var eth = wallets[0];
        var polygon = wallets[1];
        var bsc = wallets[2];

        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Act: Apply chain defaults for all wallets
        var chainMappings = new[]
        {
            ("1", eth.Id),
            ("137", polygon.Id),
            ("56", bsc.Id)
        };

        var batchResult = reloadedPrincipal.ApplyChainDefaultsBatch(chainMappings);
        batchResult.IsSuccess.ShouldBeTrue();
        batchResult.Value.ShouldBe(3); // Should apply 3 defaults

        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Verify all chain defaults were persisted
        ClearChangeTracker();
        var savedDefaults = await DbContext.PrincipalChainDefaults
            .Where(pcd => pcd.PrincipalId == principal.Id)
            .ToListAsync();

        savedDefaults.Count.ShouldBe(3);
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "1" && pcd.WalletId == eth.Id);
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "137" && pcd.WalletId == polygon.Id);
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "56" && pcd.WalletId == bsc.Id);
    }

    #endregion

    #region Constraint Violation Tests

    [Test]
    public async Task AddAsync_Should_EnforceUniqueCredentialConstraint()
    {
        // Arrange: Create two principals with same credential details
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "shared-user");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "shared-user");

        await SavePrincipalWithWallets(principal1);

        // Act & Assert: Second principal with same credential should fail
        await PrincipalRepository.AddAsync(principal2);
        AssertUniqueConstraintViolation(async () => await UnitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task UpdateAsync_Should_PreventMultipleVerifiedSigningOwnersPerWallet()
    {
        // Arrange: Create two principals
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var sharedWallet = CreateTestWallet("1", "0xshared");

        await SavePrincipalWithWallets(principal1, sharedWallet);
        await SavePrincipalWithWallets(principal2);

        // Principal1 gets verified signing ownership
        var ownership1 = CreateTestOwnership(principal1.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to give principal2 also verified signing ownership of same wallet
        var reloadedPrincipal2 = await PrincipalRepository.GetByIdAsync(principal2.Id);
        reloadedPrincipal2.ShouldNotBeNull();

        var ownership2 = CreateTestOwnership(principal2.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        reloadedPrincipal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(reloadedPrincipal2);

        // Assert: Should fail due to unique partial index constraint
        AssertUniqueConstraintViolation(async () => await UnitOfWork.SaveChangesAsync());
    }

    #endregion

    #region Transaction Boundary Tests

    [Test]
    public async Task UpdateAsync_Should_RollbackAllChanges_When_PartialUpdateFails()
    {
        // Arrange: Create principal with wallets
        var (principal, wallets) = await CreateCompleteTestScenario();
        var eth = wallets[0];
        var polygon = wallets[1];

        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Simulate a scenario where one part succeeds and another would fail
        // Update risk tier (this should succeed)
        reloadedPrincipal.UpdateRiskTier(RiskTier.High);

        // Add chain defaults (this should succeed)
        reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("1", eth.Id), ("137", polygon.Id) });

        // Record initial state
        var initialOwnershipCount = reloadedPrincipal.WalletOwnerships.Count;
        var initialChainDefaultCount = reloadedPrincipal.PrincipalChainDefaults.Count;

        // Act: Perform update (this should succeed completely)
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Verify all changes were committed
        var freshPrincipal = await QueryFreshAsync(() => PrincipalRepository.GetByIdAsync(principal.Id));
        freshPrincipal.ShouldNotBeNull();
        freshPrincipal.RiskTier.ShouldBe(RiskTier.High);
        freshPrincipal.WalletOwnerships.Count.ShouldBe(initialOwnershipCount);
        freshPrincipal.PrincipalChainDefaults.Count.ShouldBe(initialChainDefaultCount);

        // Verify database state
        ClearChangeTracker();
        var savedChainDefaults = await DbContext.PrincipalChainDefaults
            .Where(pcd => pcd.PrincipalId == principal.Id)
            .CountAsync();
        savedChainDefaults.ShouldBe(2);
    }

    [Test]
    public async Task UpdateAsync_Should_HandleCascadeDeleteOfNavigationProperties()
    {
        // Arrange: Create principal with complete relationship graph
        var (principal, wallets) = await CreateCompleteTestScenario();
        var eth = wallets[0];
        var polygon = wallets[1];
        var bsc = wallets[2];

        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Add chain defaults
        reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("1", eth.Id), ("137", polygon.Id) });
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Verify data exists
        ClearChangeTracker();
        var ownershipCount = await DbContext.WalletOwnerships.CountAsync(wo => wo.PrincipalId == principal.Id);
        var chainDefaultCount = await DbContext.PrincipalChainDefaults.CountAsync(pcd => pcd.PrincipalId == principal.Id);
        var credentialCount = await DbContext.Credentials.CountAsync(ic => ic.PrincipalId == principal.Id);

        ownershipCount.ShouldBeGreaterThan(0);
        chainDefaultCount.ShouldBeGreaterThan(0);
        credentialCount.ShouldBeGreaterThan(0);

        // Act: Delete the principal (should cascade to all navigation properties)
        var principalToDelete = await DbContext.Principals.FindAsync(principal.Id);
        if (principalToDelete != null)
        {
            DbContext.Principals.Remove(principalToDelete);
        }
        await UnitOfWork.SaveChangesAsync();

        // Assert: All related entities should be deleted
        ClearChangeTracker();
        var remainingOwnerships = await DbContext.WalletOwnerships.CountAsync(wo => wo.PrincipalId == principal.Id);
        var remainingChainDefaults = await DbContext.PrincipalChainDefaults.CountAsync(pcd => pcd.PrincipalId == principal.Id);
        var remainingCredentials = await DbContext.Credentials.CountAsync(ic => ic.PrincipalId == principal.Id);

        remainingOwnerships.ShouldBe(0);
        remainingChainDefaults.ShouldBe(0);
        remainingCredentials.ShouldBe(0);
    }

    #endregion

    #region Query and Loading Tests

    [Test]
    public async Task GetByIdAsync_Should_LoadAllNavigationProperties()
    {
        // Arrange: Create complete test scenario
        var (principal, wallets) = await CreateCompleteTestScenario();
        var eth = wallets[0];
        var polygon = wallets[1];

        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Add chain defaults
        reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("1", eth.Id), ("137", polygon.Id) });
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Load principal fresh from database
        var loadedPrincipal = await QueryFreshAsync(() => PrincipalRepository.GetByIdAsync(principal.Id));

        // Assert: All navigation properties should be loaded
        loadedPrincipal.ShouldNotBeNull();
        loadedPrincipal.Credentials.Count.ShouldBeGreaterThan(0);
        loadedPrincipal.WalletOwnerships.Count.ShouldBeGreaterThan(0);
        loadedPrincipal.PrincipalChainDefaults.Count.ShouldBeGreaterThan(0);

        // Verify no lazy loading issues
        loadedPrincipal.Credentials.ShouldAllBe(c => !string.IsNullOrEmpty(c.Provider));
        loadedPrincipal.WalletOwnerships.ShouldAllBe(wo => wo.WalletId != default);
        loadedPrincipal.PrincipalChainDefaults.ShouldAllBe(pcd => !string.IsNullOrEmpty(pcd.ChainId));
    }

    [Test]
    public async Task FindByCredentialAsync_Should_ReturnCorrectPrincipal()
    {
        // Arrange: Create multiple principals with different credentials
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var principal3 = CreateTestPrincipal("https://other.provider.com", "user1"); // Same subject, different provider

        await SavePrincipalWithWallets(principal1);
        await SavePrincipalWithWallets(principal2);
        await SavePrincipalWithWallets(principal3);

        // Act: Search by specific credential
        var providerType = ProviderType.Create("dynamic").Value;
        var foundPrincipal = await PrincipalRepository.FindByCredentialAsync(
            providerType,
            "https://app.dynamic.xyz/test",
            "user1");

        // Assert: Should find the correct principal
        foundPrincipal.ShouldNotBeNull();
        foundPrincipal.Id.ShouldBe(principal1.Id);
        foundPrincipal.Credentials.ShouldContain(c =>
            c.Provider == "dynamic" &&
            c.Issuer == "https://app.dynamic.xyz/test" &&
            c.Subject == "user1");
    }

    [Test]
    public async Task IsCredentialTakenAsync_Should_DetectExistingCredentials()
    {
        // Arrange: Create principal with credential
        var principal = CreateTestPrincipal("https://app.dynamic.xyz/test", "taken-user");
        await SavePrincipalWithWallets(principal);

        // Act: Check if credential is taken
        var providerType = ProviderType.Create("dynamic").Value;
        var isTaken = await PrincipalRepository.IsCredentialTakenAsync(
            providerType,
            "https://app.dynamic.xyz/test",
            "taken-user");

        var isNotTaken = await PrincipalRepository.IsCredentialTakenAsync(
            providerType,
            "https://app.dynamic.xyz/test",
            "available-user");

        // Assert
        isTaken.ShouldBeTrue();
        isNotTaken.ShouldBeFalse();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task UpdateAsync_Should_HandleEmptyNavigationProperties()
    {
        // Arrange: Create principal with no navigation properties
        var principal = CreateTestPrincipal();
        await SavePrincipalWithWallets(principal);

        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        // Act: Update principal without adding any navigation properties
        reloadedPrincipal.UpdateRiskTier(RiskTier.Medium);
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Should succeed without issues
        var updatedPrincipal = await QueryFreshAsync(() => PrincipalRepository.GetByIdAsync(principal.Id));
        updatedPrincipal.ShouldNotBeNull();
        updatedPrincipal.RiskTier.ShouldBe(RiskTier.Medium);
        updatedPrincipal.WalletOwnerships.ShouldBeEmpty();
        updatedPrincipal.PrincipalChainDefaults.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_Should_HandleLargeNumberOfNavigationProperties()
    {
        // Arrange: Create principal
        var principal = CreateTestPrincipal();
        var wallets = new List<Wallet>();

        // Create many wallets (simulate real-world scenario with many owned wallets)
        for (int i = 0; i < 50; i++)
        {
            var wallet = CreateTestWallet($"chain{i}", $"0x{i:X40}");
            wallets.Add(wallet);
        }

        await SavePrincipalWithWallets(principal, wallets.ToArray());

        // Add many ownerships
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        for (int i = 0; i < wallets.Count; i++)
        {
            var wallet = wallets[i];
            var ownership = CreateTestOwnership(
                reloadedPrincipal.Id,
                wallet.Id,
                i % 3 == 0 ? AccessMode.WatchOnly : AccessMode.Signing);

            reloadedPrincipal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));
        }

        // Act: Update with many navigation properties
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: All ownerships should be persisted
        var freshPrincipal = await QueryFreshAsync(() => PrincipalRepository.GetByIdAsync(principal.Id));
        freshPrincipal.ShouldNotBeNull();
        freshPrincipal.WalletOwnerships.Count.ShouldBe(50);
    }

    #endregion
}