using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests specifically for PrincipalChainDefault persistence issues.
/// These tests validate that navigation property changes are properly tracked and saved.
/// Uses PostgreSQL with Testcontainers for realistic testing.
/// </summary>
[TestFixture]
public class PrincipalChainDefaultPersistenceTests : IdentityPersistenceTestBase
{
    [Test]
    public async Task UpdateAsync_Should_PersistNewPrincipalChainDefaults_When_AddedToTrackedAggregate()
    {
        // Arrange: Create a principal with some wallets
        var principal = CreateTestPrincipal();
        var wallet1 = CreateTestWallet("1", "0x1234567890123456789012345678901234567890");
        var wallet2 = CreateTestWallet("137", "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd");

        // Add initial data and save
        await DbContext.Wallets.AddAsync(wallet1);
        await DbContext.Wallets.AddAsync(wallet2);
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Step 1: Reload the principal (simulating the exchange flow where principal is loaded from DB)
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();
        reloadedPrincipal.PrincipalChainDefaults.ShouldBeEmpty(); // Should start empty

        // Step 2: Add wallet ownerships (simulating the exchange flow)
        var ownership1 = WalletOwnership.Create(
            reloadedPrincipal.Id, wallet1.Id, AccessMode.Signing, OwnershipStatus.Verified);
        var ownership2 = WalletOwnership.Create(
            reloadedPrincipal.Id, wallet2.Id, AccessMode.Signing, OwnershipStatus.Verified);

        var linkResult1 = reloadedPrincipal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
        var linkResult2 = reloadedPrincipal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        linkResult1.IsSuccess.ShouldBeTrue();
        linkResult2.IsSuccess.ShouldBeTrue();

        // Step 3: Apply chain defaults (this is the critical part that was failing)
        var chainMappings = new[]
        {
            ("1", wallet1.Id),  // Use numeric chain IDs to match the wallet chain IDs
            ("137", wallet2.Id)
        };

        var batchResult = reloadedPrincipal.ApplyChainDefaultsBatch(chainMappings, TimeProvider.System);
        batchResult.IsSuccess.ShouldBeTrue();
        batchResult.Value.ShouldBe(2); // Should apply 2 defaults

        // Verify in-memory state before persistence
        reloadedPrincipal.PrincipalChainDefaults.Count.ShouldBe(2);
        reloadedPrincipal.PrincipalChainDefaults.ShouldContain(pcd => pcd.ChainId == "1" && pcd.WalletId == wallet1.Id);
        reloadedPrincipal.PrincipalChainDefaults.ShouldContain(pcd => pcd.ChainId == "137" && pcd.WalletId == wallet2.Id);

        // Step 4: Update and save (fixed tracking issue by ensuring proper state management)
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        var rowsAffected = await UnitOfWork.SaveChangesAsync();

        // Step 5: Verify the data was actually persisted to database
        rowsAffected.ShouldBeGreaterThan(0); // Should have affected some rows

        // Clear context to ensure we're reading from database, not cache
        ClearChangeTracker();

        // Step 6: Query database directly to verify PrincipalChainDefaults were saved
        // Access through aggregate root as these are owned entities
        var principalWithDefaults = await DbContext.Principals
            .Include(p => p.PrincipalChainDefaults)
            .FirstOrDefaultAsync(p => p.Id == principal.Id);

        var savedDefaults = principalWithDefaults?.PrincipalChainDefaults ?? new List<PrincipalChainDefault>();

        // CRITICAL ASSERTION: This is what was failing before the fix
        savedDefaults.ShouldNotBeEmpty("PrincipalChainDefaults should be saved to database");
        savedDefaults.Count.ShouldBe(2, "Should have 2 chain defaults saved");
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "1" && pcd.WalletId == wallet1.Id);
        savedDefaults.ShouldContain(pcd => pcd.ChainId == "137" && pcd.WalletId == wallet2.Id);

        // Step 7: Verify a fresh load also includes the defaults
        var freshPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        freshPrincipal.ShouldNotBeNull();
        freshPrincipal.PrincipalChainDefaults.Count.ShouldBe(2);
    }

    [Test]
    public async Task UpdateAsync_Should_PersistUpdatedPrincipalChainDefaults_When_ExistingDefaultIsChanged()
    {
        // Arrange: Create principal with existing chain default
        var principal = CreateTestPrincipal();
        var wallet1 = CreateTestWallet("1", "0x1234567890123456789012345678901234567890");
        var wallet2 = CreateTestWallet("1", "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd"); // Same chain, different wallet

        await DbContext.Wallets.AddAsync(wallet1);
        await DbContext.Wallets.AddAsync(wallet2);

        // Add initial default
        var ownership1 = WalletOwnership.Create(principal.Id, wallet1.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);
        principal.ApplyChainDefaultsBatch(new[] { ("1", wallet1.Id) }, TimeProvider.System);

        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Reload and change the default
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        var ownership2 = WalletOwnership.Create(reloadedPrincipal.Id, wallet2.Id, AccessMode.Signing, OwnershipStatus.Verified);
        reloadedPrincipal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false), TimeProvider.System);

        // Change the default to wallet2
        var updateResult = reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("1", wallet2.Id) }, TimeProvider.System);
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Value.ShouldBe(1); // Should update 1 default

        // Persist the change
        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Verify the change was persisted
        ClearChangeTracker();
        // Access through aggregate root as these are owned entities
        var principalWithDefaults = await DbContext.Principals
            .Include(p => p.PrincipalChainDefaults)
            .FirstOrDefaultAsync(p => p.Id == principal.Id);

        var savedDefaults = principalWithDefaults?.PrincipalChainDefaults ?? new List<PrincipalChainDefault>();

        savedDefaults.Count.ShouldBe(1);
        savedDefaults.Single().WalletId.ShouldBe(wallet2.Id);
    }

    private static Wallet CreateTestWallet(string chainId, string address)
    {
        return Wallet.Create(
            null,
            chainId,
            Address.Create(address).Value);
    }
}