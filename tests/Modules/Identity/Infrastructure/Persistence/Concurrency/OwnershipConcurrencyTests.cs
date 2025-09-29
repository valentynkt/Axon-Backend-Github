using System.Threading.Tasks;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.Concurrency;

/// <summary>
/// Advanced concurrent ownership tests focusing on auto-revocation and batch operations.
/// Tests complex scenarios with multiple principals competing for wallet ownership.
/// These tests WILL FAIL initially until auto-revocation and exclusivity enforcement is implemented.
/// </summary>
[TestFixture]
public class ConcurrentOwnershipTests : IdentityDbInvariantsTestBase
{
    #region Multi-Principal Auto-Revocation Tests

    [Test]
    public async Task ConcurrentOwnership_ThreePendingOneVerifies_ShouldAutoRevokeAllOthers()
    {
        // Arrange: Create three principals with pending ownership of same wallet
        var principals = new[]
        {
            AxonPrincipal.CreateHuman(), // Winner
            AxonPrincipal.CreateHuman(), // Loser 1
            AxonPrincipal.CreateHuman()  // Loser 2
        };

        var contestedWallet = TestDataFixtures.CreateW1Main();

        // Save all entities
        foreach (var principal in principals)
        {
            await PrincipalRepository.AddAsync(principal, CancellationToken.None);
        }
        await WalletRepository.AddAsync(contestedWallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create pending ownerships for all three
        var pendingOwnerships = principals.Select(p =>
            TestDataFixtures.CreatePendingSigningOwnership(p.Id, contestedWallet.Id)).ToList();

        for (int i = 0; i < principals.Length; i++)
        {
            var linkResult = principals[i].LinkWalletOwnership(
                pendingOwnerships[i],
                (_, _, _) => Result.Success<bool, Error>(false)); // Allow multiple pending

            linkResult.IsSuccess.ShouldBeTrue($"Principal {i} should be able to add pending ownership");
        }

        // Save pending state
        foreach (var principal in principals)
        {
            await PrincipalRepository.UpdateAsync(principal, CancellationToken.None);
        }
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: First principal verifies (should trigger auto-revocation of others)
        var winnerOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(principals[0].Id, contestedWallet.Id);
        var verificationResult = principals[0].LinkWalletOwnership(
            winnerOwnership,
            (walletId, accessMode, status) => CheckExistingOwnershipAsync(walletId, accessMode, status).GetAwaiter().GetResult());

        verificationResult.IsSuccess.ShouldBeTrue("Winner should successfully verify ownership");

        await PrincipalRepository.UpdateAsync(principals[0], CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);


        // Assert: Winner has verified ownership, others are revoked
        await VerifyAutoRevocationResults(contestedWallet.Id, principals[0].Id, principals.Skip(1).Select(p => p.Id));

    }

    [Test]
    public async Task ConcurrentOwnership_BatchRevocation_ShouldBeAtomic()
    {
        // Arrange: Create scenario with many competing pending ownerships
        const int competitorCount = 5;
        var competitors = Enumerable.Range(0, competitorCount)
            .Select(_ => AxonPrincipal.CreateHuman())
            .ToList();

        var winner = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        // Save entities
        await PrincipalRepository.AddAsync(winner, CancellationToken.None);
        foreach (var competitor in competitors)
        {
            await PrincipalRepository.AddAsync(competitor, CancellationToken.None);
        }
        await WalletRepository.AddAsync(wallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create competing pending ownerships
        foreach (var competitor in competitors)
        {
            var pendingOwnership = TestDataFixtures.CreatePendingSigningOwnership(competitor.Id, wallet.Id);
            competitor.LinkWalletOwnership(pendingOwnership, (_, _, _) => Result.Success<bool, Error>(false));
            await PrincipalRepository.UpdateAsync(competitor, CancellationToken.None);
        }
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Winner verifies in transaction that should atomically revoke all others
        using var transaction = await DbContext.Database.BeginTransactionAsync();
        try
        {
            var winnerOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(winner.Id, wallet.Id);
            var verificationResult = winner.LinkWalletOwnership(
                winnerOwnership,
                (walletId, accessMode, status) => CheckExistingOwnershipAsync(walletId, accessMode, status).GetAwaiter().GetResult());

            verificationResult.IsSuccess.ShouldBeTrue("Winner verification should succeed");

            await PrincipalRepository.UpdateAsync(winner, CancellationToken.None);


            await UnitOfWork.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Assert: All operations completed atomically
        await VerifyAtomicRevocationResults(wallet.Id, winner.Id, competitors.Select(c => c.Id));

        // NOTE: This test ensures that auto-revocation is atomic and doesn't leave partial state
    }

    [Test]
    public async Task ConcurrentOwnership_CrossWalletRevocation_ShouldOnlyAffectTargetWallet()
    {
        // Arrange: Create scenario with multiple wallets and cross-ownership
        var principalA = AxonPrincipal.CreateHuman();
        var principalB = AxonPrincipal.CreateHuman();
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();

        await PrincipalRepository.AddAsync(principalA, CancellationToken.None);
        await PrincipalRepository.AddAsync(principalB, CancellationToken.None);
        await WalletRepository.AddAsync(wallet1, CancellationToken.None);
        await WalletRepository.AddAsync(wallet2, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Create pending ownerships on both wallets for both principals
        var pendingA1 = TestDataFixtures.CreatePendingSigningOwnership(principalA.Id, wallet1.Id);
        var pendingA2 = TestDataFixtures.CreatePendingSigningOwnership(principalA.Id, wallet2.Id);
        var pendingB1 = TestDataFixtures.CreatePendingSigningOwnership(principalB.Id, wallet1.Id);
        var pendingB2 = TestDataFixtures.CreatePendingSigningOwnership(principalB.Id, wallet2.Id);

        principalA.LinkWalletOwnership(pendingA1, (_, _, _) => Result.Success<bool, Error>(false));
        principalA.LinkWalletOwnership(pendingA2, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(pendingB1, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(pendingB2, (_, _, _) => Result.Success<bool, Error>(false));

        await PrincipalRepository.UpdateAsync(principalA, CancellationToken.None);
        await PrincipalRepository.UpdateAsync(principalB, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Principal A verifies wallet1 (should only affect wallet1 ownerships)
        var verifiedA1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, wallet1.Id);
        var verificationResult = principalA.LinkWalletOwnership(
            verifiedA1,
            (walletId, accessMode, status) => CheckExistingOwnershipAsync(walletId, accessMode, status).GetAwaiter().GetResult());

        verificationResult.IsSuccess.ShouldBeTrue("Principal A should verify wallet1");
        await PrincipalRepository.UpdateAsync(principalA, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert: Only wallet1 ownerships affected, wallet2 ownerships unchanged
        await VerifyScopedRevocation(wallet1.Id, wallet2.Id, principalA.Id, principalB.Id);

        // NOTE: This ensures auto-revocation is scoped to specific wallets only
    }

    #endregion

    #region Stress Testing with High Concurrency

    [Test]
    public async Task ConcurrentOwnership_HighContention_ShouldMaintainDataIntegrity()
    {
        // Arrange: Create high-contention scenario with many competing principals
        const int principalCount = 10;
        var principals = Enumerable.Range(0, principalCount)
            .Select(_ => AxonPrincipal.CreateHuman())
            .ToList();

        var hotWallet = TestDataFixtures.CreateW1Main();

        // Save all entities
        foreach (var principal in principals)
        {
            await PrincipalRepository.AddAsync(principal, CancellationToken.None);
        }
        await WalletRepository.AddAsync(hotWallet, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Create high-contention scenario where all principals try to verify simultaneously
        var tasks = principals.Select(async (principal, index) =>
        {
            try
            {
                await Task.Delay(Random.Shared.Next(0, 100)); // Random delay to increase contention

                using var separateContext = CreateConcurrentDbContext();
                using var repo = new AxonPrincipalWriteRepository(separateContext, UnitOfWork);

                var freshPrincipal = await repo.GetByIdAsync(principal.Id, CancellationToken.None);
                var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, hotWallet.Id);

                var linkResult = freshPrincipal!.LinkWalletOwnership(
                    ownership,
                    (walletId, accessMode, status) => CheckExistingOwnershipWithContextAsync(
                        separateContext, walletId, accessMode, status).GetAwaiter().GetResult());

                if (linkResult.IsSuccess)
                {
                    await repo.UpdateAsync(freshPrincipal, CancellationToken.None);
                    await separateContext.SaveChangesAsync();
                    return (Success: true, PrincipalId: principal.Id, Error: (string?)null);
                }

                return (Success: false, PrincipalId: principal.Id, Error: linkResult.Error.Message);
            }
            catch (Exception ex)
            {
                return (Success: false, PrincipalId: principal.Id, Error: ex.Message);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactly one should succeed, data integrity maintained
        var successes = results.Where(r => r.Success).ToList();
        var failures = results.Where(r => !r.Success).ToList();

        successes.Count.ShouldBe(1, "Exactly one principal should succeed under high contention");
        failures.Count.ShouldBe(principalCount - 1, "All other principals should fail");

        // Verify database integrity after high contention
        await VerifyDatabaseIntegrityAfterHighContention(hotWallet.Id, successes[0].PrincipalId);

        // NOTE: This test validates that the system maintains data integrity under extreme load
    }

    [Test]
    public async Task ConcurrentOwnership_DeadlockResistance_ShouldHandleDeadlockGracefully()
    {
        // Arrange: Create scenario that could cause deadlocks with competing transactions
        var principalA = AxonPrincipal.CreateHuman();
        var principalB = AxonPrincipal.CreateHuman();
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();

        await PrincipalRepository.AddAsync(principalA, CancellationToken.None);
        await PrincipalRepository.AddAsync(principalB, CancellationToken.None);
        await WalletRepository.AddAsync(wallet1, CancellationToken.None);
        await WalletRepository.AddAsync(wallet2, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act: Create cross-locking scenario that could cause deadlocks
        (Exception? exception1, Exception? exception2) = await ExecuteConcurrentOperations(
            async context1 =>
            {
                // Transaction 1: A→wallet1, then A→wallet2
                using var repo1 = new AxonPrincipalWriteRepository(context1, UnitOfWork);
                var principal = await repo1.GetByIdAsync(principalA.Id, CancellationToken.None);

                var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, wallet1.Id);
                principal!.LinkWalletOwnership(ownership1, (walletId, accessMode, status) =>
                    CheckExistingOwnershipWithContextAsync(context1, walletId, accessMode, status).GetAwaiter().GetResult());

                await repo1.UpdateAsync(principal, CancellationToken.None);
                await context1.SaveChangesAsync();

                await Task.Delay(50); // Increase deadlock potential

                var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, wallet2.Id);
                principal.LinkWalletOwnership(ownership2, (walletId, accessMode, status) =>
                    CheckExistingOwnershipWithContextAsync(context1, walletId, accessMode, status).GetAwaiter().GetResult());

                await repo1.UpdateAsync(principal, CancellationToken.None);
                await context1.SaveChangesAsync();
            },
            async context2 =>
            {
                // Transaction 2: B→wallet2, then B→wallet1 (reverse order)
                using var repo2 = new AxonPrincipalWriteRepository(context2, UnitOfWork);
                var principal = await repo2.GetByIdAsync(principalB.Id, CancellationToken.None);

                var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, wallet2.Id);
                principal!.LinkWalletOwnership(ownership2, (walletId, accessMode, status) =>
                    CheckExistingOwnershipWithContextAsync(context2, walletId, accessMode, status).GetAwaiter().GetResult());

                await repo2.UpdateAsync(principal, CancellationToken.None);
                await context2.SaveChangesAsync();

                await Task.Delay(50); // Increase deadlock potential

                var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, wallet1.Id);
                principal.LinkWalletOwnership(ownership1, (walletId, accessMode, status) =>
                    CheckExistingOwnershipWithContextAsync(context2, walletId, accessMode, status).GetAwaiter().GetResult());

                await repo2.UpdateAsync(principal, CancellationToken.None);
                await context2.SaveChangesAsync();
            });

        // Assert: System should handle deadlocks gracefully (not both fail due to deadlock)
        var isDeadlock = exception1 != null && exception2 != null &&
                        (exception1.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) || exception2.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase));

        if (isDeadlock)
        {
            // If deadlock occurred, verify retry mechanisms work
            await VerifyDeadlockRecovery(wallet1.Id, wallet2.Id);
        }
        else
        {
            // Normal completion - verify exclusivity maintained
            await VerifyExclusivityAfterConcurrentOperations(wallet1.Id, wallet2.Id);
        }

        // NOTE: This test ensures the system handles database deadlocks gracefully
    }

    #endregion

    #region Helper Methods

    private async Task<Result<bool, Error>> CheckExistingOwnershipAsync(
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status)
    {
        return await CheckExistingOwnershipWithContextAsync(DbContext, walletId, accessMode, status);
    }

    private static async Task<Result<bool, Error>> CheckExistingOwnershipWithContextAsync(
        IdentityWriteDbContext context,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status)
    {
        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await context.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        // Check if any principal has the specified ownership
        var existingOwnership = principals
            .SelectMany(p => p.WalletOwnerships)
            .Any(wo => wo.WalletId == walletId &&
                      wo.AccessMode == accessMode &&
                      wo.Status == status &&
                      !wo.IsDeleted);

        return Result.Success<bool, Error>(existingOwnership);
    }

    private async Task VerifyAutoRevocationResults(
        WalletId walletId,
        AxonUserId winnerId,
        IEnumerable<AxonUserId> loserIds)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == walletId && !wo.IsDeleted)
            .ToList();

        // Winner should have verified ownership
        var winnerOwnerships = allOwnerships.Where(wo => wo.PrincipalId == winnerId).ToList();
        winnerOwnerships.ShouldHaveSingleItem("Winner should have exactly one ownership");
        winnerOwnerships[0].Status.ShouldBe(OwnershipStatus.Verified);
        winnerOwnerships[0].AccessMode.ShouldBe(AccessMode.Signing);

        // All losers should have revoked ownerships
        foreach (var loserId in loserIds)
        {
            var loserOwnerships = allOwnerships.Where(wo => wo.PrincipalId == loserId).ToList();
            loserOwnerships.ShouldHaveSingleItem($"Loser {loserId} should have exactly one ownership");
            loserOwnerships[0].Status.ShouldBe(OwnershipStatus.Revoked,
                $"Loser {loserId} ownership should be revoked");
        }
    }

    private async Task VerifyAtomicRevocationResults(
        WalletId walletId,
        AxonUserId winnerId,
        IEnumerable<AxonUserId> competitorIds)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == walletId && !wo.IsDeleted)
            .ToList();

        // Verify atomic operation - either all revoked or none
        var verifiedCount = allOwnerships.Count(wo => wo.Status == OwnershipStatus.Verified);
        var revokedCount = allOwnerships.Count(wo => wo.Status == OwnershipStatus.Revoked);

        verifiedCount.ShouldBe(1, "Exactly one verified ownership should exist");
        revokedCount.ShouldBe(competitorIds.Count(), "All competitors should be revoked");

        // Winner should be the verified one
        var winnerOwnership = allOwnerships.FirstOrDefault(wo => wo.PrincipalId == winnerId);
        winnerOwnership.ShouldNotBeNull("Winner should have ownership");
        winnerOwnership!.Status.ShouldBe(OwnershipStatus.Verified);
    }

    private async Task VerifyScopedRevocation(
        WalletId affectedWalletId,
        WalletId unaffectedWalletId,
        AxonUserId principalAId,
        AxonUserId principalBId)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => !wo.IsDeleted)
            .ToList();

        // Affected wallet: A should be verified, B should be revoked
        var affectedOwnerships = allOwnerships
            .Where(wo => wo.WalletId == affectedWalletId)
            .ToList();

        var aAffected = affectedOwnerships.FirstOrDefault(wo => wo.PrincipalId == principalAId);
        var bAffected = affectedOwnerships.FirstOrDefault(wo => wo.PrincipalId == principalBId);

        aAffected.ShouldNotBeNull();
        aAffected!.Status.ShouldBe(OwnershipStatus.Verified);
        bAffected.ShouldNotBeNull();
        bAffected!.Status.ShouldBe(OwnershipStatus.Revoked);

        // Unaffected wallet: Both should still be pending
        var unaffectedOwnerships = allOwnerships
            .Where(wo => wo.WalletId == unaffectedWalletId)
            .ToList();

        var aUnaffected = unaffectedOwnerships.FirstOrDefault(wo => wo.PrincipalId == principalAId);
        var bUnaffected = unaffectedOwnerships.FirstOrDefault(wo => wo.PrincipalId == principalBId);

        aUnaffected.ShouldNotBeNull();
        aUnaffected!.Status.ShouldBe(OwnershipStatus.Pending);
        bUnaffected.ShouldNotBeNull();
        bUnaffected!.Status.ShouldBe(OwnershipStatus.Pending);
    }

    private async Task VerifyDatabaseIntegrityAfterHighContention(WalletId walletId, AxonUserId winnerId)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        // Verify no orphaned or inconsistent ownership records
        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => wo.WalletId == walletId && !wo.IsDeleted)
            .ToList();

        var verifiedOwnerships = allOwnerships.Where(wo => wo.Status == OwnershipStatus.Verified && wo.AccessMode == AccessMode.Signing).ToList();

        verifiedOwnerships.Count.ShouldBe(1, "Exactly one verified+signing ownership should exist");
        verifiedOwnerships[0].PrincipalId.ShouldBe(winnerId);

        // Verify referential integrity
        var principalExists = await DbContext.Set<AxonPrincipal>()
            .AnyAsync(p => p.Id == winnerId && !p.IsDeleted);
        principalExists.ShouldBeTrue("Winner principal should exist");

        var walletExists = await DbContext.Set<Wallet>()
            .AnyAsync(w => w.Id == walletId && !w.IsDeleted);
        walletExists.ShouldBeTrue("Wallet should exist");
    }

    private async Task VerifyDeadlockRecovery(WalletId wallet1Id, WalletId wallet2Id)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        // After deadlock recovery, verify system state is consistent
        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => (wo.WalletId == wallet1Id || wo.WalletId == wallet2Id) && !wo.IsDeleted)
            .ToList();

        // Each wallet should have at most one verified+signing ownership
        var wallet1Verified = allOwnerships.Count(wo => wo.WalletId == wallet1Id && wo.Status == OwnershipStatus.Verified && wo.AccessMode == AccessMode.Signing);
        var wallet2Verified = allOwnerships.Count(wo => wo.WalletId == wallet2Id && wo.Status == OwnershipStatus.Verified && wo.AccessMode == AccessMode.Signing);

        wallet1Verified.ShouldBeLessThanOrEqualTo(1, "Wallet1 should have at most one verified+signing ownership");
        wallet2Verified.ShouldBeLessThanOrEqualTo(1, "Wallet2 should have at most one verified+signing ownership");

        // No partial state should remain
        allOwnerships.ShouldAllBe(wo => wo.Status != OwnershipStatus.Pending,
            "No pending ownerships should remain after deadlock recovery");
    }

    private async Task VerifyExclusivityAfterConcurrentOperations(WalletId wallet1Id, WalletId wallet2Id)
    {
        ClearChangeTracker();

        // Query through AxonPrincipal since WalletOwnership is an owned entity
        var principals = await DbContext.Set<AxonPrincipal>()
            .Include(p => p.WalletOwnerships)
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        var allOwnerships = principals
            .SelectMany(p => p.WalletOwnerships)
            .Where(wo => !wo.IsDeleted)
            .ToList();

        var wallet1Verified = allOwnerships
            .Count(wo => wo.WalletId == wallet1Id && wo.Status == OwnershipStatus.Verified && wo.AccessMode == AccessMode.Signing);

        var wallet2Verified = allOwnerships
            .Count(wo => wo.WalletId == wallet2Id && wo.Status == OwnershipStatus.Verified && wo.AccessMode == AccessMode.Signing);

        wallet1Verified.ShouldBeLessThanOrEqualTo(1, "Wallet1 should have at most one verified+signing ownership");
        wallet2Verified.ShouldBeLessThanOrEqualTo(1, "Wallet2 should have at most one verified+signing ownership");
    }

    #endregion
}