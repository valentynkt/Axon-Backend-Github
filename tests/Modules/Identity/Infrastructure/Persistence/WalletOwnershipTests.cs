using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for wallet ownership persistence focusing on unique constraints and business rules.
/// Validates the critical constraint: only one verified signing owner per wallet.
/// </summary>
[TestFixture]
public class WalletOwnershipPersistenceTests : IdentityPersistenceTestBase
{
    #region Unique Constraint Tests

    [Test]
    public async Task Should_EnforceOnlyOneVerifiedSigningOwnerPerWallet()
    {
        // Arrange: Create two principals and one shared wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var sharedWallet = CreateTestWallet("1", "0x1234567890abcdef1234567890abcdef12345678");

        // Save the shared wallet first
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Save both principals
        await PrincipalRepository.AddAsync(principal1);
        await PrincipalRepository.AddAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Give first principal verified signing ownership
        var ownership1 = CreateTestOwnership(
            principal1.Id,
            sharedWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified);

        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Try to give second principal also verified signing ownership
        var reloadedPrincipal2 = await PrincipalRepository.GetByIdAsync(principal2.Id);
        reloadedPrincipal2.ShouldNotBeNull();

        var ownership2 = CreateTestOwnership(
            principal2.Id,
            sharedWallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified);

        reloadedPrincipal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));
        await PrincipalRepository.UpdateAsync(reloadedPrincipal2);

        // Assert: Should fail due to unique partial index constraint
        await AssertUniqueConstraintViolation(async () => await UnitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task Should_AllowMultipleWatchOnlyOwnersPerWallet()
    {
        // Arrange: Create three principals and one shared wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var principal3 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user3");
        var sharedWallet = CreateTestWallet("1", "0x1234567890abcdef1234567890abcdef12345678");

        // Save the shared wallet first
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Save all principals
        await PrincipalRepository.AddAsync(principal1);
        await PrincipalRepository.AddAsync(principal2);
        await PrincipalRepository.AddAsync(principal3);
        await UnitOfWork.SaveChangesAsync();

        // Act: Give all three principals watch-only ownership
        var ownership1 = CreateTestOwnership(principal1.Id, sharedWallet.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);
        var ownership2 = CreateTestOwnership(principal2.Id, sharedWallet.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);
        var ownership3 = CreateTestOwnership(principal3.Id, sharedWallet.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);

        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));
        principal3.LinkWalletOwnership(ownership3, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        await PrincipalRepository.UpdateAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        await PrincipalRepository.UpdateAsync(principal3);
        await UnitOfWork.SaveChangesAsync();

        // Verify all ownerships were saved
        ClearChangeTracker();
        var principals = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .ToListAsync();

        var savedOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == sharedWallet.Id)
            .ToList();

        savedOwnerships.Count.ShouldBe(3);
        savedOwnerships.ShouldAllBe(wo => wo.AccessMode == AccessMode.WatchOnly);
        savedOwnerships.ShouldAllBe(wo => wo.Status == OwnershipStatus.Verified);
    }

    [Test]
    public async Task Should_AllowMultiplePendingSigningOwnersPerWallet()
    {
        // Arrange: Create two principals and one shared wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var sharedWallet = CreateTestWallet("1", "0x1234567890abcdef1234567890abcdef12345678");

        // Save the shared wallet first
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Save both principals
        await PrincipalRepository.AddAsync(principal1);
        await PrincipalRepository.AddAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Give both principals pending signing ownership
        var ownership1 = CreateTestOwnership(principal1.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Pending);
        var ownership2 = CreateTestOwnership(principal2.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Pending);

        var linkResult1 = principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        var linkResult2 = principal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        linkResult1.IsSuccess.ShouldBeTrue($"First ownership link should succeed");
        linkResult2.IsSuccess.ShouldBeTrue($"Second ownership link should succeed");

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        await PrincipalRepository.UpdateAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        ClearChangeTracker();
        var principals = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .ToListAsync();

        var savedOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == sharedWallet.Id)
            .ToList();

        savedOwnerships.Count.ShouldBe(2);
        savedOwnerships.ShouldAllBe(wo => wo.AccessMode == AccessMode.Signing);
        savedOwnerships.ShouldAllBe(wo => wo.Status == OwnershipStatus.Pending);
    }

    [Test]
    public async Task Should_PreventDuplicatePrincipalWalletCombination()
    {
        // Arrange: Create principal and wallet
        var principal = CreateTestPrincipal();
        var wallet = CreateTestWallet("1", "0xabcdef1234567890abcdef1234567890abcdef12");
        await SavePrincipalWithWallets(principal, wallet);

        // Add initial ownership
        var ownership1 = CreateTestOwnership(principal.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to add another ownership for same principal-wallet combination
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        var ownership2 = CreateTestOwnership(principal.Id, wallet.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);
        var linkResult = reloadedPrincipal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        // Assert: Should fail at domain level
        linkResult.IsFailure.ShouldBeTrue();
        linkResult.Error.Code.ShouldContain("WalletOwnership.AlreadyExists");
    }

    #endregion

    #region State Transition Tests

    [Test]
    public async Task Should_HandleOwnershipStatusTransitions_FromPendingToVerified()
    {
        // Arrange: Create principal with pending ownership
        var principal = CreateTestPrincipal();
        var wallet = CreateTestWallet("1", "0xabcdef1234567890abcdef1234567890abcdef12");
        await SavePrincipalWithWallets(principal, wallet);

        var pendingOwnership = CreateTestOwnership(principal.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Pending);
        principal.LinkWalletOwnership(pendingOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Update ownership status to verified
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        var existingOwnership = reloadedPrincipal.WalletOwnerships.First();
        existingOwnership.UpdateStatus(OwnershipStatus.Verified);

        await PrincipalRepository.UpdateAsync(reloadedPrincipal);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Status should be updated in database
        ClearChangeTracker();
        var reloadedPrincipalCheck = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .FirstAsync(p => p.Id == principal.Id);

        var savedOwnership = reloadedPrincipalCheck.WalletOwnerships
            .First(wo => wo.WalletId == wallet.Id);

        savedOwnership.Status.ShouldBe(OwnershipStatus.Verified);
    }

    [Test]
    public async Task Should_UpdatePartialIndexCorrectly_When_StatusChanges()
    {
        // Arrange: Create two principals with same wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var sharedWallet = CreateTestWallet("1", "0x1234567890abcdef1234567890abcdef12345678");

        // Save the shared wallet first
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Save both principals
        await PrincipalRepository.AddAsync(principal1);
        await PrincipalRepository.AddAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Principal1 gets verified signing ownership
        var ownership1 = CreateTestOwnership(principal1.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        // Principal2 gets pending signing ownership
        var ownership2 = CreateTestOwnership(principal2.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Pending);
        principal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        await PrincipalRepository.UpdateAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to verify principal2's ownership (should fail due to partial index)
        var reloadedPrincipal2 = await PrincipalRepository.GetByIdAsync(principal2.Id);
        reloadedPrincipal2.ShouldNotBeNull();

        var existingOwnership = reloadedPrincipal2.WalletOwnerships.First();
        existingOwnership.UpdateStatus(OwnershipStatus.Verified);

        await PrincipalRepository.UpdateAsync(reloadedPrincipal2);

        // Assert: Should fail due to partial unique index
        await AssertUniqueConstraintViolation(async () => await UnitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task Should_AllowVerificationTransfer_When_FirstOwnershipIsRevoked()
    {
        // Arrange: Create two principals with same wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var sharedWallet = CreateTestWallet("1", "0x1234567890abcdef1234567890abcdef12345678");

        // Save the shared wallet first
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Save both principals
        await PrincipalRepository.AddAsync(principal1);
        await PrincipalRepository.AddAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Both get ownership, but only principal1 is verified
        var ownership1 = CreateTestOwnership(principal1.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        var ownership2 = CreateTestOwnership(principal2.Id, sharedWallet.Id, AccessMode.Signing, OwnershipStatus.Pending);

        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        await PrincipalRepository.UpdateAsync(principal2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Revoke principal1's ownership and verify principal2's
        var reloadedPrincipal1 = await PrincipalRepository.GetByIdAsync(principal1.Id);
        var reloadedPrincipal2 = await PrincipalRepository.GetByIdAsync(principal2.Id);

        reloadedPrincipal1.ShouldNotBeNull();
        reloadedPrincipal2.ShouldNotBeNull();

        // Remove principal1's ownership
        reloadedPrincipal1.RemoveWalletOwnership(sharedWallet.Id);
        await PrincipalRepository.UpdateAsync(reloadedPrincipal1);
        await UnitOfWork.SaveChangesAsync();

        // Now verify principal2's ownership (should succeed)
        var ownership2ToVerify = reloadedPrincipal2.WalletOwnerships.First();
        ownership2ToVerify.UpdateStatus(OwnershipStatus.Verified);

        await PrincipalRepository.UpdateAsync(reloadedPrincipal2);

        // Assert: Should succeed now that principal1's ownership is removed
        await UnitOfWork.SaveChangesAsync();

        ClearChangeTracker();
        var principals = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .ToListAsync();

        var verifiedOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == sharedWallet.Id &&
                        wo.Status == OwnershipStatus.Verified &&
                        wo.AccessMode == AccessMode.Signing)
            .ToList();

        verifiedOwnerships.Count.ShouldBe(1);
        verifiedOwnerships.Single().PrincipalId.ShouldBe(principal2.Id);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Should_HandleOwnershipTransferBetweenPrincipals()
    {
        // Arrange: Create two principals and a wallet
        var principal1 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user1");
        var principal2 = CreateTestPrincipal("https://app.dynamic.xyz/test", "user2");
        var wallet = CreateTestWallet("1", "0xabcdef1234567890abcdef1234567890abcdef12");

        await SavePrincipalWithWallets(principal1, wallet);
        await SavePrincipalWithWallets(principal2);

        // Principal1 initially owns the wallet
        var ownership1 = CreateTestOwnership(principal1.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        principal1.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Act: Transfer ownership from principal1 to principal2
        var reloadedPrincipal1 = await PrincipalRepository.GetByIdAsync(principal1.Id);
        var reloadedPrincipal2 = await PrincipalRepository.GetByIdAsync(principal2.Id);

        reloadedPrincipal1.ShouldNotBeNull();
        reloadedPrincipal2.ShouldNotBeNull();

        // Remove from principal1
        reloadedPrincipal1.RemoveWalletOwnership(wallet.Id);
        await PrincipalRepository.UpdateAsync(reloadedPrincipal1);
        await UnitOfWork.SaveChangesAsync();

        // Add to principal2
        var ownership2 = CreateTestOwnership(principal2.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Verified);
        reloadedPrincipal2.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));
        await PrincipalRepository.UpdateAsync(reloadedPrincipal2);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Ownership should be transferred
        ClearChangeTracker();
        var principals = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .ToListAsync();

        var ownerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == wallet.Id)
            .ToList();

        ownerships.Count.ShouldBe(1);
        ownerships.Single().PrincipalId.ShouldBe(principal2.Id);
    }

    [Test]
    public async Task Should_CleanupOrphanedOwnerships_After_PrincipalDelete()
    {
        // Arrange: Create principal with ownerships
        var principal = CreateTestPrincipal();
        var (eth, polygon, bsc) = CreateMultiChainWallets();
        await SavePrincipalWithWallets(principal, eth, polygon, bsc);

        // Add ownerships for all wallets
        var ownership1 = CreateTestOwnership(principal.Id, eth.Id, AccessMode.Signing, OwnershipStatus.Verified);
        var ownership2 = CreateTestOwnership(principal.Id, polygon.Id, AccessMode.Signing, OwnershipStatus.Verified);
        var ownership3 = CreateTestOwnership(principal.Id, bsc.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);

        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(ownership3, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Verify ownerships exist
        ClearChangeTracker();
        var principalWithOwnerships = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .FirstOrDefaultAsync(p => p.Id == principal.Id);

        principalWithOwnerships.ShouldNotBeNull();
        var ownershipCount = principalWithOwnerships.WalletOwnerships.Count;
        ownershipCount.ShouldBe(3);

        // Act: Delete the principal
        var principalToDelete = await DbContext.Principals.FindAsync(principal.Id);
        if (principalToDelete != null)
        {
            DbContext.Principals.Remove(principalToDelete);
        }
        await UnitOfWork.SaveChangesAsync();


        // Assert: All ownerships should be cascade deleted
        ClearChangeTracker();
        var remainingPrincipal = await DbContext.Principals
            .AsNoTracking()
            .Include(p => p.WalletOwnerships)
            .FirstOrDefaultAsync(p => p.Id == principal.Id);

        // Principal should be deleted
        remainingPrincipal.ShouldBeNull();

        // Wallets should still exist
        var walletCount = await DbContext.Wallets.CountAsync();
        walletCount.ShouldBe(3);
    }

    [Test]
    public async Task Should_HandleConcurrentOwnershipUpdates()
    {
        // Arrange: Create principal with ownership
        var principal = CreateTestPrincipal();
        var wallet = CreateTestWallet("1", "0xabcdef1234567890abcdef1234567890abcdef12");
        await SavePrincipalWithWallets(principal, wallet);

        var ownership = CreateTestOwnership(principal.Id, wallet.Id, AccessMode.Signing, OwnershipStatus.Pending);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Create separate contexts for concurrent access
        using var concurrentContext = CreateConcurrentDbContext();
        using var concurrentUow = new EfUnitOfWork<IdentityWriteDbContext, IdentityModule>(concurrentContext);
        using var concurrentRepo = new AxonPrincipalWriteRepository(concurrentContext, concurrentUow);

        // Load principal in two separate contexts
        // Clear tracker to ensure clean load
        ClearChangeTracker();
        var principal1 = await PrincipalRepository.GetByIdAsync(principal.Id);

        // Load in concurrent context
        var principal2 = await concurrentRepo.GetByIdAsync(principal.Id);

        principal1.ShouldNotBeNull();
        principal2.ShouldNotBeNull();

        // Act: Both contexts try to modify the principal
        // Modifying child entities (owned entities) doesn't trigger concurrency checks by themselves
        // We need to ensure the aggregate root is actually modified

        // Verify initial state
        principal1.RiskTier.ShouldBe(RiskTier.Low, "Principal1 should start with Low risk tier");
        principal2.RiskTier.ShouldBe(RiskTier.Low, "Principal2 should start with Low risk tier");

        // Both principals start with RiskTier.Low, so update to different values
        var result1 = principal1.UpdateRiskTier(RiskTier.Medium);
        var result2 = principal2.UpdateRiskTier(RiskTier.High);

        result1.IsSuccess.ShouldBeTrue("Principal1 risk tier update should succeed");
        result2.IsSuccess.ShouldBeTrue("Principal2 risk tier update should succeed");

        principal1.RiskTier.ShouldBe(RiskTier.Medium, "Principal1 should have Medium risk tier after update");
        principal2.RiskTier.ShouldBe(RiskTier.High, "Principal2 should have High risk tier after update");

        // Also modify ownership status to simulate realistic concurrent updates
        var ownership1 = principal1.WalletOwnerships.First();
        var ownership2 = principal2.WalletOwnerships.First();
        ownership1.UpdateStatus(OwnershipStatus.Verified);
        ownership2.UpdateStatus(OwnershipStatus.Revoked);

        // Save first context
        await PrincipalRepository.UpdateAsync(principal1);
        await UnitOfWork.SaveChangesAsync();

        // Clear change tracker to ensure fresh read
        ClearChangeTracker();

        // Verify first update was actually saved
        var verifyPrincipal = await DbContext.Principals
            .AsNoTracking()
            .FirstAsync(p => p.Id == principal.Id);
        verifyPrincipal.RiskTier.ShouldBe(RiskTier.Medium, "First update should be persisted");

        // Try to save second context (which has stale version)
        await concurrentRepo.UpdateAsync(principal2);

        // Assert: Should fail due to concurrency conflict
        // The WriteDbContextBase wraps DbUpdateConcurrencyException in a ConcurrencyException
        var exception = await Should.ThrowAsync<ConcurrencyException>(
            async () => await concurrentUow.SaveChangesAsync());

        // Verify the exception details
        exception.Message.ShouldContain("has been modified by another user");
        exception.Message.ShouldContain(principal.Id.ToString());
    }

    [Test]
    public async Task Should_RespectAccessModeConstraintsForChainDefaults()
    {
        // Arrange: Create principal with watch-only ownership
        var principal = CreateTestPrincipal();
        var wallet = CreateTestWallet("1", "0xabcdef1234567890abcdef1234567890abcdef12");
        await SavePrincipalWithWallets(principal, wallet);

        var watchOnlyOwnership = CreateTestOwnership(principal.Id, wallet.Id, AccessMode.WatchOnly, OwnershipStatus.Verified);
        principal.LinkWalletOwnership(watchOnlyOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to apply chain default with watch-only wallet
        var reloadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        reloadedPrincipal.ShouldNotBeNull();

        var chainDefaultResult = reloadedPrincipal.ApplyChainDefaultsBatch(new[] { ("ethereum-mainnet", wallet.Id) });

        // Assert: Should fail at domain level
        chainDefaultResult.IsFailure.ShouldBeTrue();
        chainDefaultResult.Error.Code.ShouldContain("IDENTITY.WALLET.WATCH_ONLY_VIOLATION");
    }

    #endregion
}