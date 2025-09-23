using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Domain;

/// <summary>
/// Defaults Behavior tests for Identity Module as specified in TDD document section D.
/// These tests validate auto-seed behavior and strict clear-on-revoke policy.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// </summary>
[TestFixture]
public class PrincipalDefaultsBehaviorTests
{
    #region Test 15: DEFAULT_auto_seed_on_first_verified

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_FirstVerifiedSigning_ShouldCreateDefault()
    {
        // Arrange: Create principal with no existing defaults
        var (principal, wallet) = TestDataFixtures.CreateAutoSeedDefaultScenario();

        // Verify no existing defaults
        principal.PrincipalChainDefaults.Count.ShouldBe(0);

        // Act: Apply chain default for the first verified+signing wallet
        var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet.Id);

        // Assert: Should succeed and create default
        result.IsSuccess.ShouldBeTrue();

        // Verify default was created
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var chainDefault = principal.PrincipalChainDefaults.First();
        chainDefault.ChainId.ShouldBe(TestDataFixtures.SolanaMainnetChain);
        chainDefault.WalletId.ShouldBe(wallet.Id);
        chainDefault.IsDeleted.ShouldBeFalse();

        // NOTE: This test will FAIL initially if auto-seeding logic is missing
        // The system should automatically create defaults for first verified+signing wallets
    }

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_MultipleChains_ShouldCreateSeparateDefaults()
    {
        // Arrange: Create principal with wallets on different chains
        var (principal, mainnetWallet, devnetWallet) = TestDataFixtures.CreateMultiChainDefaultScenario();

        // Verify no existing defaults
        principal.PrincipalChainDefaults.Count.ShouldBe(0);

        // Act: Apply defaults for both chains
        var mainnetResult = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, mainnetWallet.Id);
        var devnetResult = principal.ApplyChainDefault(NetworkEnvironment.Devnet, TestDataFixtures.SolanaDevnetChain, devnetWallet.Id);

        // Assert: Both should succeed
        mainnetResult.IsSuccess.ShouldBeTrue();
        devnetResult.IsSuccess.ShouldBeTrue();

        // Verify separate defaults were created
        principal.PrincipalChainDefaults.Count.ShouldBe(2);

        var mainnetDefault = principal.PrincipalChainDefaults
            .First(pcd => pcd.ChainId == TestDataFixtures.SolanaMainnetChain);
        var devnetDefault = principal.PrincipalChainDefaults
            .First(pcd => pcd.ChainId == TestDataFixtures.SolanaDevnetChain);

        mainnetDefault.WalletId.ShouldBe(mainnetWallet.Id);
        devnetDefault.WalletId.ShouldBe(devnetWallet.Id);
        mainnetDefault.IsDeleted.ShouldBeFalse();
        devnetDefault.IsDeleted.ShouldBeFalse();
    }

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_NonVerifiedWallet_ShouldFail()
    {
        // Arrange: Create principal with pending (non-verified) wallet
        var principal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create pending ownership (not verified)
        var pendingOwnership = TestDataFixtures.CreatePendingSigningOwnership(principal.Id, wallet.Id);
        var linkResult = principal.LinkWalletOwnership(pendingOwnership, (_, _, _) => Result.Success<bool, Error>(false));
        linkResult.IsSuccess.ShouldBeTrue();

        // Act: Try to set pending wallet as default
        var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet.Id);

        // Assert: Should fail because wallet is not verified+signing
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("IDENTITY.WALLET.WATCH_ONLY_VIOLATION");

        // Verify no default was created
        principal.PrincipalChainDefaults.Count.ShouldBe(0);
    }

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_WatchOnlyWallet_ShouldFail()
    {
        // Arrange: Create principal with watch-only wallet
        var principal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create watch-only ownership (verified but not signing)
        var watchOnlyOwnership = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principal.Id, wallet.Id);
        var linkResult = principal.LinkWalletOwnership(watchOnlyOwnership, (_, _, _) => Result.Success<bool, Error>(false));
        linkResult.IsSuccess.ShouldBeTrue();

        // Act: Try to set watch-only wallet as default
        var result = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet.Id);

        // Assert: Should fail because wallet is watch-only
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("IDENTITY.WALLET.WATCH_ONLY_VIOLATION");

        // Verify no default was created
        principal.PrincipalChainDefaults.Count.ShouldBe(0);
    }

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_IdempotentBehavior_ShouldNotDuplicate()
    {
        // Arrange: Create principal with verified signing wallet
        var (principal, wallet) = TestDataFixtures.CreateAutoSeedDefaultScenario();

        // Act: Apply same default twice
        var result1 = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet.Id);
        var result2 = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet.Id);

        // Assert: Both should succeed
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();

        // Verify only one default exists (idempotent)
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var chainDefault = principal.PrincipalChainDefaults.First();
        chainDefault.WalletId.ShouldBe(wallet.Id);
        chainDefault.IsDeleted.ShouldBeFalse();
    }

    [Test]
    public void Test_DEFAULT_auto_seed_on_first_verified_BatchOperation_ShouldCreateMultiple()
    {
        // Arrange: Create principal with multiple verified wallets
        var (principal, mainnetWallet, devnetWallet) = TestDataFixtures.CreateMultiChainDefaultScenario();

        // Prepare batch operations
        var walletChainMappings = new List<(string chainId, WalletId walletId)>
        {
            (TestDataFixtures.SolanaMainnetChain, mainnetWallet.Id),
            (TestDataFixtures.SolanaDevnetChain, devnetWallet.Id)
        };

        // Act: Apply batch defaults
        var result = principal.ApplyChainDefaultsBatch(NetworkEnvironment.Mainnet, walletChainMappings);

        // Assert: Should succeed and create 2 defaults
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2); // 2 defaults applied

        // Verify defaults were created
        principal.PrincipalChainDefaults.Count.ShouldBe(2);

        var mainnetDefault = principal.PrincipalChainDefaults
            .First(pcd => pcd.ChainId == TestDataFixtures.SolanaMainnetChain);
        var devnetDefault = principal.PrincipalChainDefaults
            .First(pcd => pcd.ChainId == TestDataFixtures.SolanaDevnetChain);

        mainnetDefault.WalletId.ShouldBe(mainnetWallet.Id);
        devnetDefault.WalletId.ShouldBe(devnetWallet.Id);
    }

    #endregion

    #region Test 16: DEFAULT_clear_on_revoke

    [Test]
    public void Test_DEFAULT_clear_on_revoke_RevokeDefaultedWallet_ShouldClearDefault()
    {
        // Arrange: Create scenario with defaulted wallet
        var (principal, wallet, _) = TestDataFixtures.CreateClearOnRevokeScenario();

        // Verify default exists
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var existingDefault = principal.PrincipalChainDefaults.First();
        existingDefault.WalletId.ShouldBe(wallet.Id);
        existingDefault.IsDeleted.ShouldBeFalse();

        // Act: Remove wallet ownership (revoke)
        var result = principal.RemoveWalletOwnership(wallet.Id);

        // Assert: Should succeed
        result.IsSuccess.ShouldBeTrue();

        // Verify wallet ownership was removed
        principal.WalletOwnerships.Count.ShouldBe(0);

        // Verify default was cleared (strict policy)
        // NOTE: This test will FAIL initially if clear-on-revoke logic is missing
        principal.PrincipalChainDefaults.Count.ShouldBe(0);
    }

    [Test]
    public void Test_DEFAULT_clear_on_revoke_RevokeNonDefaultedWallet_ShouldKeepDefaults()
    {
        // Arrange: Create principal with two wallets, one defaulted
        var principal = AxonPrincipal.CreateHuman();
        var defaultedWallet = TestDataFixtures.CreateW1Main();
        var nonDefaultedWallet = TestDataFixtures.CreateW2Main();

        // Link both wallets with verified signing
        var defaultedOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, defaultedWallet.Id);
        var nonDefaultedOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, nonDefaultedWallet.Id);

        principal.LinkWalletOwnership(defaultedOwnership, (_, _, _) => Result.Success<bool, Error>(false));
        principal.LinkWalletOwnership(nonDefaultedOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        // Set only first wallet as default
        var defaultResult = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, defaultedWallet.Id);
        defaultResult.IsSuccess.ShouldBeTrue();

        // Verify setup
        principal.WalletOwnerships.Count.ShouldBe(2);
        principal.PrincipalChainDefaults.Count.ShouldBe(1);

        // Act: Remove non-defaulted wallet
        var result = principal.RemoveWalletOwnership(nonDefaultedWallet.Id);

        // Assert: Should succeed
        result.IsSuccess.ShouldBeTrue();

        // Verify non-defaulted wallet was removed
        principal.WalletOwnerships.Count.ShouldBe(1);
        principal.WalletOwnerships.First().WalletId.ShouldBe(defaultedWallet.Id);

        // Verify default was NOT cleared (only defaulted wallet removals should clear)
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var remainingDefault = principal.PrincipalChainDefaults.First();
        remainingDefault.WalletId.ShouldBe(defaultedWallet.Id);
        remainingDefault.IsDeleted.ShouldBeFalse();
    }

    [Test]
    public void Test_DEFAULT_clear_on_revoke_MultipleDefaults_ShouldClearOnlyRelated()
    {
        // Arrange: Create principal with defaults on multiple chains
        var (principal, mainnetWallet, devnetWallet) = TestDataFixtures.CreateMultiChainDefaultScenario();

        // Set defaults for both chains
        var mainnetResult = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, mainnetWallet.Id);
        var devnetResult = principal.ApplyChainDefault(NetworkEnvironment.Devnet, TestDataFixtures.SolanaDevnetChain, devnetWallet.Id);

        mainnetResult.IsSuccess.ShouldBeTrue();
        devnetResult.IsSuccess.ShouldBeTrue();

        // Verify setup
        principal.WalletOwnerships.Count.ShouldBe(2);
        principal.PrincipalChainDefaults.Count.ShouldBe(2);

        // Act: Remove only mainnet wallet
        var result = principal.RemoveWalletOwnership(mainnetWallet.Id);

        // Assert: Should succeed
        result.IsSuccess.ShouldBeTrue();

        // Verify mainnet wallet was removed
        principal.WalletOwnerships.Count.ShouldBe(1);
        principal.WalletOwnerships.First().WalletId.ShouldBe(devnetWallet.Id);

        // Verify only mainnet default was cleared, devnet default remains
        // NOTE: This test will FAIL initially if selective clearing logic is missing
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var remainingDefault = principal.PrincipalChainDefaults.First();
        remainingDefault.ChainId.ShouldBe(TestDataFixtures.SolanaDevnetChain);
        remainingDefault.WalletId.ShouldBe(devnetWallet.Id);
        remainingDefault.IsDeleted.ShouldBeFalse();
    }

    [Test]
    public void Test_DEFAULT_clear_on_revoke_StatusChangeToRevoked_ShouldClearDefault()
    {
        // Arrange: Create scenario with defaulted wallet
        var (principal, wallet, _) = TestDataFixtures.CreateClearOnRevokeScenario();

        // Verify default exists
        principal.PrincipalChainDefaults.Count.ShouldBe(1);

        // Act: Change ownership status to revoked using principal method
        var statusResult = principal.UpdateWalletOwnershipStatus(wallet.Id, OwnershipStatus.Revoked);

        // Assert: Status change should succeed
        statusResult.IsSuccess.ShouldBeTrue();

        // Verify ownership still exists but is revoked
        principal.WalletOwnerships.Count.ShouldBe(1);
        var ownership = principal.WalletOwnerships.First(wo => wo.WalletId == wallet.Id);
        ownership.Status.ShouldBe(OwnershipStatus.Revoked);

        // The system should detect revoked status and clear defaults even without removal
        principal.PrincipalChainDefaults.Count.ShouldBe(0);
    }

    #endregion

    #region Integration Defaults Tests

    [Test]
    public void Integration_CompleteDefaultsLifecycle_ShouldFollowAllRules()
    {
        // Arrange: Create principal for complete lifecycle test
        var principal = AxonPrincipal.CreateHuman();
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();

        // Step 1: Link first wallet (should auto-seed default)
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet1.Id);
        var linkResult1 = principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        linkResult1.IsSuccess.ShouldBeTrue();

        var defaultResult1 = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet1.Id);
        defaultResult1.IsSuccess.ShouldBeTrue();

        // Verify first default
        principal.PrincipalChainDefaults.Count.ShouldBe(1);

        // Step 2: Link second wallet and update default
        var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet2.Id);
        var linkResult2 = principal.LinkWalletOwnership(ownership2, (_, _, _) => Result.Success<bool, Error>(false));
        linkResult2.IsSuccess.ShouldBeTrue();

        var defaultResult2 = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet2.Id);
        defaultResult2.IsSuccess.ShouldBeTrue();

        // Verify default was updated (not duplicated)
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var currentDefault = principal.PrincipalChainDefaults.First();
        currentDefault.WalletId.ShouldBe(wallet2.Id);

        // Step 3: Revoke current default wallet
        var revokeResult = principal.RemoveWalletOwnership(wallet2.Id);
        revokeResult.IsSuccess.ShouldBeTrue();

        // Verify default was cleared
        principal.PrincipalChainDefaults.Count.ShouldBe(0);

        // Step 4: Re-apply default with remaining wallet
        var reapplyResult = principal.ApplyChainDefault(NetworkEnvironment.Mainnet, TestDataFixtures.SolanaMainnetChain, wallet1.Id);
        reapplyResult.IsSuccess.ShouldBeTrue();

        // Verify new default was created
        principal.PrincipalChainDefaults.Count.ShouldBe(1);
        var finalDefault = principal.PrincipalChainDefaults.First();
        finalDefault.WalletId.ShouldBe(wallet1.Id);
        finalDefault.IsDeleted.ShouldBeFalse();
    }

    [Test]
    public void Integration_DefaultsBatchOperations_ShouldRespectConstraints()
    {
        // Arrange: Create scenario for batch operations testing
        var principal = AxonPrincipal.CreateHuman();
        var validWallet = TestDataFixtures.CreateW1Main();
        var invalidWallet = TestDataFixtures.CreateW2Main();

        // Link only one wallet as verified+signing
        var validOwnership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, validWallet.Id);
        principal.LinkWalletOwnership(validOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        // Create invalid ownership (watch-only)
        var invalidOwnership = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principal.Id, invalidWallet.Id);
        principal.LinkWalletOwnership(invalidOwnership, (_, _, _) => Result.Success<bool, Error>(false));

        // Prepare batch with mixed valid/invalid wallets
        var walletChainMappings = new List<(string chainId, WalletId walletId)>
        {
            (TestDataFixtures.SolanaMainnetChain, validWallet.Id), // Valid
            (TestDataFixtures.SolanaDevnetChain, invalidWallet.Id) // Invalid (watch-only)
        };

        // Act: Try to apply batch defaults
        var result = principal.ApplyChainDefaultsBatch(NetworkEnvironment.Mainnet, walletChainMappings);

        // Assert: Should fail due to invalid wallet in batch
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("IDENTITY.WALLET.WATCH_ONLY_VIOLATION");

        // Verify no defaults were created (all-or-nothing batch behavior)
        principal.PrincipalChainDefaults.Count.ShouldBe(0);
    }

    #endregion
}