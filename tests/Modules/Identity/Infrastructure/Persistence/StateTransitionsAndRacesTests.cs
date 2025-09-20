using System.Threading.Tasks;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Tests for TDD Section C: State Transitions and Races (Integration + Concurrency).
/// Validates exclusivity under contention, auto-revocation, and state transitions.
/// These tests WILL FAIL initially until exclusivity and auto-revocation logic is implemented.
/// </summary>
[TestFixture]
public class StateTransitionsAndRacesTests : IdentityDbInvariantsTestBase
{
    #region Test Case C.12 - VERIFY_exclusivity_race_two_principals

    [Test]
    public async Task C12_ExclusivityRace_TwoPrincipalsVerifyingSameWallet_ShouldOnlyAllowOneWinner()
    {
        // Arrange: Create two principals and one shared wallet
        var principalA = AxonPrincipal.CreateHuman();
        var principalB = AxonPrincipal.CreateHuman();
        var sharedWallet = TestDataFixtures.CreateW1Main();

        // Save initial state
        await PrincipalRepository.AddAsync(principalA, CancellationToken.None);
        await PrincipalRepository.AddAsync(principalB, CancellationToken.None);
        await WalletRepository.AddAsync(sharedWallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create pending ownerships for both principals
        var ownershipA = TestDataFixtures.CreatePendingSigningOwnership(principalA.Id, sharedWallet.Id);
        var ownershipB = TestDataFixtures.CreatePendingSigningOwnership(principalB.Id, sharedWallet.Id);

        // Act: Execute concurrent verification operations
        var (exception1, exception2) = await ExecuteConcurrentOperations(
            async context1 =>
            {
                // Principal A tries to verify the wallet
                using var repo1 = new AxonPrincipalWriteRepository(context1, UnitOfWork);
                var principal1 = await repo1.GetByIdAsync(principalA.Id, CancellationToken.None);

                var linkResult = principal1!.LinkWalletOwnership(
                    ownershipA,
                    (walletId, accessMode, status) =>
                        CheckExistingOwnershipAsync(context1, walletId, accessMode, status).GetAwaiter().GetResult());

                if (linkResult.IsSuccess)
                {
                    await repo1.UpdateAsync(principal1, CancellationToken.None);
                    await context1.SaveChangesAsync();
                }
                else
                {
                    throw new InvalidOperationException($"Principal A verification failed: {linkResult.Error.Message}");
                }
            },
            async context2 =>
            {
                // Principal B tries to verify the same wallet
                using var repo2 = new AxonPrincipalWriteRepository(context2, UnitOfWork);
                var principal2 = await repo2.GetByIdAsync(principalB.Id, CancellationToken.None);

                var linkResult = principal2!.LinkWalletOwnership(
                    ownershipB,
                    (walletId, accessMode, status) =>
                        CheckExistingOwnershipAsync(context2, walletId, accessMode, status).GetAwaiter().GetResult());

                if (linkResult.IsSuccess)
                {
                    await repo2.UpdateAsync(principal2, CancellationToken.None);
                    await context2.SaveChangesAsync();
                }
                else
                {
                    throw new InvalidOperationException($"Principal B verification failed: {linkResult.Error.Message}");
                }
            });

        // Assert: Exactly one should succeed, one should fail
        var oneSucceeded = (exception1 == null) ^ (exception2 == null); // XOR - exactly one null
        oneSucceeded.ShouldBeTrue("Exactly one principal should succeed in verifying the wallet");

        // Verify the winner has stable ownership after retry
        await VerifyExclusivityWinnerStability(principalA.Id, principalB.Id, sharedWallet.Id);

        // NOTE: This test will FAIL initially until exclusivity enforcement is implemented
        // The system should prevent dual verified+signing ownership through either:
        // 1. Database partial unique constraint violations, or
        // 2. Domain-level conflict detection in LinkWalletOwnership
    }

    [Test]
    public async Task C12_ExclusivityRace_ConcurrentDatabaseConstraints_ShouldEnforcePartialUniqueIndex()
    {
        // Arrange: Create scenario to test database-level exclusivity enforcement
        var principalA = AxonPrincipal.CreateHuman();
        var principalB = AxonPrincipal.CreateHuman();
        var sharedWallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principalA, CancellationToken.None);
        await PrincipalRepository.AddAsync(principalB, CancellationToken.None);
        await WalletRepository.AddAsync(sharedWallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create verified+signing ownerships directly for database constraint testing
        var verifiedOwnershipA = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
        var verifiedOwnershipB = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, sharedWallet.Id);

        // Act & Assert: Concurrent insert should trigger constraint violation
        await AssertPartialUniqueIndexViolation(async () =>
        {
            await ExecuteConcurrentOperations(
                async context1 =>
                {
                    context1.Set<WalletOwnership>().Add(verifiedOwnershipA);
                    await context1.SaveChangesAsync();
                },
                async context2 =>
                {
                    context2.Set<WalletOwnership>().Add(verifiedOwnershipB);
                    await context2.SaveChangesAsync();
                });
        }, "idx_wallet_ownership_verified_signing_unique");

        // NOTE: This test validates that the database itself prevents dual verified+signing
        // ownership even if domain logic is bypassed
    }

    #endregion

    #region Test Case C.13 - AUTO_REVOKE_pending_competitors

    [Test]
    public async Task C13_AutoRevokePendingCompetitors_WhenPrincipalVerifies_ShouldRevokePendingOthers()
    {
        // Arrange: Create one wallet with multiple pending ownerships
        var winnerPrincipal = AxonPrincipal.CreateHuman();
        var loserPrincipalA = AxonPrincipal.CreateHuman();
        var loserPrincipalB = AxonPrincipal.CreateHuman();
        var contestedWallet = TestDataFixtures.CreateW1Main();

        // Save entities
        await PrincipalRepository.AddAsync(winnerPrincipal, CancellationToken.None);
        await PrincipalRepository.AddAsync(loserPrincipalA, CancellationToken.None);
        await PrincipalRepository.AddAsync(loserPrincipalB, CancellationToken.None);
        await WalletRepository.AddAsync(contestedWallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create multiple pending ownerships
        var pendingOwnershipA = TestDataFixtures.CreatePendingSigningOwnership(loserPrincipalA.Id, contestedWallet.Id);
        var pendingOwnershipB = TestDataFixtures.CreatePendingSigningOwnership(loserPrincipalB.Id, contestedWallet.Id);

        // Link pending ownerships first
        loserPrincipalA.LinkWalletOwnership(pendingOwnershipA, (_, _, _) => Result.Success<bool, Error>(false));
        loserPrincipalB.LinkWalletOwnership(pendingOwnershipB, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(loserPrincipalA, CancellationToken.None);
        await PrincipalRepository.UpdateAsync(loserPrincipalB, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Winner principal verifies the wallet (should trigger auto-revocation)
        var winnerOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(winnerPrincipal.Id, contestedWallet.Id);
        var linkResult = winnerPrincipal.LinkWalletOwnership(
            winnerOwnership,
            (walletId, accessMode, status) => CheckExistingOwnershipAsync(DbContext, walletId, accessMode, status).GetAwaiter().GetResult());

        linkResult.IsSuccess.ShouldBeTrue("Winner should successfully verify ownership");

        await PrincipalRepository.UpdateAsync(winnerPrincipal, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // TODO: Implement auto-revocation logic in the domain or infrastructure
        // This should automatically revoke all other pending ownerships for the same wallet

        // Assert: All other pending ownerships should be revoked with conflict_lost reason
        await VerifyAutoRevocationOccurred(contestedWallet.Id, winnerPrincipal.Id);

        // NOTE: This test will FAIL initially until auto-revocation logic is implemented
        // The system should automatically revoke competing pending ownerships when one verifies
    }

    [Test]
    public async Task C13_AutoRevoke_OnlyPendingAffected_VerifiedOthersShouldNotBeRevoked()
    {
        // Arrange: Create scenario with existing verified ownership and new pending
        var existingVerifiedPrincipal = AxonPrincipal.CreateHuman();
        var newPendingPrincipal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(existingVerifiedPrincipal, CancellationToken.None);
        await PrincipalRepository.AddAsync(newPendingPrincipal, CancellationToken.None);
        await WalletRepository.AddAsync(wallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create verified ownership first
        var verifiedOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(existingVerifiedPrincipal.Id, wallet.Id);
        existingVerifiedPrincipal.LinkWalletOwnership(
            verifiedOwnership,
            (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(existingVerifiedPrincipal, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Try to add pending ownership (should be rejected, not auto-revoked)
        var pendingOwnership = TestDataFixtures.CreatePendingSigningOwnership(newPendingPrincipal.Id, wallet.Id);
        var linkResult = newPendingPrincipal.LinkWalletOwnership(
            pendingOwnership,
            (walletId, accessMode, status) => CheckExistingOwnershipAsync(DbContext, walletId, accessMode, status).GetAwaiter().GetResult());

        // Assert: Should be rejected due to existing verified ownership
        linkResult.IsFailure.ShouldBeTrue("Adding pending ownership should fail when verified ownership exists");
        linkResult.Error.Type.ShouldBe(ErrorType.Conflict);

        // Existing verified ownership should remain unchanged
        ClearChangeTracker();
        var refreshedPrincipal = await PrincipalRepository.GetByIdAsync(existingVerifiedPrincipal.Id, CancellationToken.None);
        refreshedPrincipal.ShouldNotBeNull();
        refreshedPrincipal!.WalletOwnerships.ShouldHaveSingleItem();
        refreshedPrincipal.WalletOwnerships.First().Status.ShouldBe(OwnershipStatus.Verified);
    }

    #endregion

    #region Test Case C.14 - REVERIFY_revoked_to_verified

    [Test]
    public async Task C14_ReverifyRevokedToVerified_ShouldAllowTransitionAndMaintainExclusivity()
    {
        // Arrange: Create principal with revoked ownership
        var principal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principal, CancellationToken.None);
        await WalletRepository.AddAsync(wallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create and link revoked ownership
        var revokedOwnership = WalletOwnership.Create(
            principal.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Revoked);

        var linkResult = principal.LinkWalletOwnership(
            revokedOwnership,
            (_, _, _) => Result.Success<bool, Error>(false));

        linkResult.IsSuccess.ShouldBeTrue();
        await PrincipalRepository.UpdateAsync(principal, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Transition revoked ownership back to verified
        var existingOwnership = principal.WalletOwnerships.First();
        var updateResult = existingOwnership.UpdateStatus(OwnershipStatus.Verified);

        // Assert: Transition should succeed
        updateResult.IsSuccess.ShouldBeTrue("Revoked → Verified transition should be allowed");
        existingOwnership.Status.ShouldBe(OwnershipStatus.Verified);
        existingOwnership.VerifiedAt.ShouldNotBeNull();
        existingOwnership.RevokedAt.ShouldBeNull();

        // Save and verify persistence
        await PrincipalRepository.UpdateAsync(principal, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Verify exclusivity still holds after re-verification
        await VerifyExclusivityAfterReverification(principal.Id, wallet.Id);

        // NOTE: This test validates that ownership can move revoked → verified
        // while maintaining exclusivity constraints
    }

    [Test]
    public async Task C14_ReverifyWithCompetition_ShouldRespectExclusivityConstraints()
    {
        // Arrange: Create scenario with revoked ownership and competing verified ownership
        var revokedPrincipal = AxonPrincipal.CreateHuman();
        var competingPrincipal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(revokedPrincipal, CancellationToken.None);
        await PrincipalRepository.AddAsync(competingPrincipal, CancellationToken.None);
        await WalletRepository.AddAsync(wallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create revoked ownership for first principal
        var revokedOwnership = WalletOwnership.Create(
            revokedPrincipal.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Revoked);

        revokedPrincipal.LinkWalletOwnership(revokedOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        // Create verified ownership for competing principal
        var verifiedOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(competingPrincipal.Id, wallet.Id);
        competingPrincipal.LinkWalletOwnership(
            verifiedOwnership,
            (walletId, accessMode, status) => CheckExistingOwnershipAsync(DbContext, walletId, accessMode, status).GetAwaiter().GetResult());

        await PrincipalRepository.UpdateAsync(revokedPrincipal, CancellationToken.None);
        await PrincipalRepository.UpdateAsync(competingPrincipal, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Try to re-verify the revoked ownership (should fail due to exclusivity)
        var existingRevokedOwnership = revokedPrincipal.WalletOwnerships.First();
        var updateResult = existingRevokedOwnership.UpdateStatus(OwnershipStatus.Verified);

        // Assert: State transition itself should succeed (domain level)
        updateResult.IsSuccess.ShouldBeTrue("Revoked → Verified transition should be valid at domain level");

        // But saving should fail due to exclusivity constraints
        await Should.ThrowAsync<DbUpdateException>(async () =>
        {
            await PrincipalRepository.UpdateAsync(revokedPrincipal, CancellationToken.None);
            await UnitOfWork.SaveChangesAsync(CancellationToken.None);
        });

        // NOTE: This demonstrates that state transitions are valid at domain level,
        // but persistence-level constraints enforce exclusivity
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if another principal has verified+signing ownership of the wallet.
    /// This is the function passed to LinkWalletOwnership for conflict detection.
    /// </summary>
    private static async Task<Result<bool, Error>> CheckExistingOwnershipAsync(
        IdentityWriteDbContext context,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status)
    {
        var existingOwnership = await context.Set<WalletOwnership>()
            .Where(wo => wo.WalletId == walletId &&
                        wo.AccessMode == accessMode &&
                        wo.Status == status &&
                        !wo.IsDeleted)
            .FirstOrDefaultAsync();

        return Result.Success<bool, Error>(existingOwnership != null);
    }

    /// <summary>
    /// Verifies that the exclusivity winner has stable ownership after the race.
    /// </summary>
    private async Task VerifyExclusivityWinnerStability(
        AxonUserId principalAId,
        AxonUserId principalBId,
        WalletId walletId)
    {
        ClearChangeTracker();

        var principalA = await PrincipalRepository.GetByIdAsync(principalAId, CancellationToken.None);
        var principalB = await PrincipalRepository.GetByIdAsync(principalBId, CancellationToken.None);

        var verifiedOwnerships = new List<WalletOwnership>();

        if (principalA?.WalletOwnerships.Any(wo => wo.WalletId == walletId && wo.IsVerifiedSigning) == true)
            verifiedOwnerships.AddRange(principalA.WalletOwnerships.Where(wo => wo.WalletId == walletId && wo.IsVerifiedSigning));

        if (principalB?.WalletOwnerships.Any(wo => wo.WalletId == walletId && wo.IsVerifiedSigning) == true)
            verifiedOwnerships.AddRange(principalB.WalletOwnerships.Where(wo => wo.WalletId == walletId && wo.IsVerifiedSigning));

        verifiedOwnerships.Count.ShouldBe(1, "Exactly one verified+signing ownership should exist after race");
    }

    /// <summary>
    /// Verifies that auto-revocation occurred for competing pending ownerships.
    /// </summary>
    private async Task VerifyAutoRevocationOccurred(WalletId walletId, AxonUserId winnerPrincipalId)
    {
        ClearChangeTracker();

        var allOwnerships = await DbContext.Set<WalletOwnership>()
            .Where(wo => wo.WalletId == walletId && !wo.IsDeleted)
            .ToListAsync();

        var verifiedOwnerships = allOwnerships.Where(wo => wo.Status == OwnershipStatus.Verified).ToList();
        var revokedOwnerships = allOwnerships.Where(wo => wo.Status == OwnershipStatus.Revoked).ToList();

        verifiedOwnerships.Count.ShouldBe(1, "Exactly one verified ownership should exist");
        verifiedOwnerships[0].PrincipalId.ShouldBe(winnerPrincipalId, "Winner should have the verified ownership");

        // All other ownerships should be revoked
        var otherOwnerships = allOwnerships.Where(wo => wo.PrincipalId != winnerPrincipalId).ToList();
        otherOwnerships.ShouldAllBe(wo => wo.Status == OwnershipStatus.Revoked,
            "All non-winner ownerships should be revoked");

        // TODO: Verify revocation reason is "conflict_lost" when that feature is implemented
    }

    /// <summary>
    /// Verifies that exclusivity is maintained after re-verification.
    /// </summary>
    private async Task VerifyExclusivityAfterReverification(AxonUserId principalId, WalletId walletId)
    {
        ClearChangeTracker();

        var allVerifiedOwnerships = await DbContext.Set<WalletOwnership>()
            .Where(wo => wo.WalletId == walletId &&
                        wo.Status == OwnershipStatus.Verified &&
                        wo.AccessMode == AccessMode.Signing &&
                        !wo.IsDeleted)
            .ToListAsync();

        allVerifiedOwnerships.Count.ShouldBe(1, "Exactly one verified+signing ownership should exist after re-verification");
        allVerifiedOwnerships[0].PrincipalId.ShouldBe(principalId, "Re-verified ownership should belong to correct principal");
    }

    #endregion
}