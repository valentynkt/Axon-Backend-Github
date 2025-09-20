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

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;

/// <summary>
/// DB Invariant tests for Identity Module as specified in TDD document section A.
/// These tests validate that database constraints alone prevent identity violations.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// </summary>
[TestFixture]
public class IdentityDbInvariantsTests : IdentityDbInvariantsTestBase
{
    #region Test 1: WALLET_UNIQUE_env_chain_address

    [Test]
    public async Task Test_WALLET_UNIQUE_env_chain_address_SameEnvironment_ShouldFail()
    {
        // Arrange: Create two wallets with same environment, chain, and address
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW1Main(); // Same environment/chain/address

        // Act: Insert first wallet (should succeed)
        await WalletRepository.AddAsync(wallet1);
        await UnitOfWork.SaveChangesAsync();

        // Try to insert duplicate
        await WalletRepository.AddAsync(wallet2);

        // Assert: Should violate unique constraint
        // NOTE: This test will FAIL initially because environment is not part of unique constraint yet
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_wallet_env_chain_address" // This constraint doesn't exist yet
        );
    }

    [Test]
    public async Task Test_WALLET_UNIQUE_env_chain_address_DifferentEnvironment_ShouldSucceed()
    {
        // Arrange: Create wallets with same chain/address but different environments
        var walletMainnet = TestDataFixtures.CreateW1Main();
        var walletDevnet = TestDataFixtures.CreateW1Dev(); // Same address, different environment

        // Act: Insert both wallets
        await WalletRepository.AddAsync(walletMainnet);
        await WalletRepository.AddAsync(walletDevnet);

        // Assert: Should succeed (no collision across environments)
        // NOTE: This test will FAIL initially because current constraint doesn't include environment
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 2: CREDENTIAL_UNIQUE_env_provider_issuer_subject

    [Test]
    public async Task Test_CREDENTIAL_UNIQUE_env_provider_issuer_subject_SameTuple_ShouldUpsert()
    {
        // Arrange: Create principal and credential
        var principal = TestDataFixtures.CreatePrincipalA();
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        var originalCredential = principal.Credentials.First();
        var originalLastSeen = originalCredential.LastSeenAt;

        // Act: Try to create same credential tuple
        var duplicateCredential = TestDataFixtures.CreateDynACredential(principal.Id);

        // Simulate credential upsert scenario - same environment/provider/issuer/subject
        await DbContext.Credentials.AddAsync(duplicateCredential);

        // Assert: Should violate unique constraint and trigger upsert logic
        // NOTE: This test will FAIL initially because environment is not part of credential constraint
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_credential_env_provider_issuer_subject" // This constraint doesn't exist yet
        );
    }

    [Test]
    public async Task Test_CREDENTIAL_UNIQUE_env_provider_issuer_subject_DifferentEnvironment_ShouldSucceed()
    {
        // Arrange: Create principal
        var principal = TestDataFixtures.CreatePrincipalA();
        await PrincipalRepository.AddAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Act: Create credentials for same provider/issuer/subject but different environments
        var mainnetCredential = TestDataFixtures.CreateDynACredential(principal.Id);
        var devnetCredential = TestDataFixtures.CreateDynACredential(principal.Id); // Same but different env

        await DbContext.Credentials.AddAsync(mainnetCredential);
        await DbContext.Credentials.AddAsync(devnetCredential);

        // Assert: Should succeed (different environments allowed)
        // NOTE: This test will FAIL initially because environment constraint doesn't exist
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 3: OWNERSHIP_PAIR_UNIQUE

    [Test]
    public async Task Test_OWNERSHIP_PAIR_UNIQUE_SamePrincipalWallet_ShouldFail()
    {
        // Arrange: Create principal and wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Create first ownership
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership1, (_, _, _) => Result.Success<bool, Error>(false));
        await PrincipalRepository.UpdateAsync(principal);
        await UnitOfWork.SaveChangesAsync();

        // Try to create duplicate ownership pair
        var ownership2 = TestDataFixtures.CreatePendingSigningOwnership(principal.Id, wallet.Id);

        await DbContext.WalletOwnerships.AddAsync(ownership2);

        // Assert: Should violate unique constraint for (principal_id, wallet_id)
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_ownership_principal_wallet"
        );
    }

    [Test]
    public async Task Test_OWNERSHIP_PAIR_UNIQUE_DifferentPairs_ShouldSucceed()
    {
        // Arrange: Create principals and wallets
        var (principalA, principalB, wallet) = TestDataFixtures.CreateExclusivityScenario();
        var wallet2 = TestDataFixtures.CreateW2Main();

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await DbContext.Wallets.AddAsync(wallet);
        await DbContext.Wallets.AddAsync(wallet2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Create different ownership pairs
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, wallet.Id);
        var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, wallet2.Id);
        var ownership3 = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principalA.Id, wallet2.Id);

        await DbContext.WalletOwnerships.AddAsync(ownership1);
        await DbContext.WalletOwnerships.AddAsync(ownership2);
        await DbContext.WalletOwnerships.AddAsync(ownership3);

        // Assert: Should succeed (different pairs)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 4: EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_TwoVerifiedSigning_ShouldFail()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Give first principal verified signing ownership
        var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
        await DbContext.WalletOwnerships.AddAsync(ownership1);
        await UnitOfWork.SaveChangesAsync();

        // Try to give second principal also verified signing ownership
        var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, sharedWallet.Id);
        await DbContext.WalletOwnerships.AddAsync(ownership2);

        // Assert: Should fail due to partial unique index constraint
        await AssertPartialUniqueIndexViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ux_wallet_verified_signing_owner"
        );
    }

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_VerifiedSigningAndWatchOnly_ShouldSucceed()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Give first principal verified signing, second principal verified watch-only
        var ownershipSigning = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
        var ownershipWatchOnly = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principalB.Id, sharedWallet.Id);

        await DbContext.WalletOwnerships.AddAsync(ownershipSigning);
        await DbContext.WalletOwnerships.AddAsync(ownershipWatchOnly);

        // Assert: Should succeed (partial index only applies to verified+signing)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    [Test]
    public async Task Test_EXCLUSIVITY_PARTIAL_UNIQUE_verified_signing_ConcurrentVerification_ShouldFail()
    {
        // Arrange: Create two principals and one shared wallet
        var (principalA, principalB, sharedWallet) = TestDataFixtures.CreateExclusivityScenario();

        await PrincipalRepository.AddAsync(principalA);
        await PrincipalRepository.AddAsync(principalB);
        await DbContext.Wallets.AddAsync(sharedWallet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to verify same wallet for two principals concurrently
        var (exception1, exception2) = await ExecuteConcurrentOperations(
            async context1 =>
            {
                var ownership1 = TestDataFixtures.CreateVerifiedSigningOwnership(principalA.Id, sharedWallet.Id);
                await context1.WalletOwnerships.AddAsync(ownership1);
                await context1.SaveChangesAsync();
            },
            async context2 =>
            {
                var ownership2 = TestDataFixtures.CreateVerifiedSigningOwnership(principalB.Id, sharedWallet.Id);
                await context2.WalletOwnerships.AddAsync(ownership2);
                await context2.SaveChangesAsync();
            });

        // Assert: Exactly one should succeed, the other should fail
        var exceptions = new[] { exception1, exception2 }.Where(e => e != null).ToArray();
        exceptions.Length.ShouldBe(1, "Exactly one concurrent operation should fail");

        var failedException = exceptions.First();
        failedException.ShouldBeOfType<DbUpdateException>();
    }

    #endregion

    #region Test 5: DEFAULT_UNIQUE_per_principal_env_chain

    [Test]
    public async Task Test_DEFAULT_UNIQUE_per_principal_env_chain_SamePrincipalChain_ShouldFail()
    {
        // Arrange: Create principal and two wallets for same chain
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet1);
        await DbContext.Wallets.AddAsync(wallet2);
        await UnitOfWork.SaveChangesAsync();

        // Act: Set first default
        var default1 = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet1.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(default1);
        await UnitOfWork.SaveChangesAsync();

        // Try to set second default for same principal/chain
        var default2 = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet2.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(default2);

        // Assert: Should fail due to unique constraint
        // NOTE: This test may FAIL initially if environment is not part of constraint
        await AssertPostgreSQLConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ix_principal_chain_default_unique"
        );
    }

    [Test]
    public async Task Test_DEFAULT_UNIQUE_per_principal_env_chain_DifferentChains_ShouldSucceed()
    {
        // Arrange: Create principal and wallets for different chains
        var principal = TestDataFixtures.CreatePrincipalA();
        var walletMainnet = TestDataFixtures.CreateW1Main();
        var walletDevnet = TestDataFixtures.CreateW1Dev();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(walletMainnet);
        await DbContext.Wallets.AddAsync(walletDevnet);
        await UnitOfWork.SaveChangesAsync();

        // Act: Set defaults for different chains
        var defaultMainnet = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, walletMainnet.Id);
        var defaultDevnet = TestDataFixtures.CreateDevnetSolanaDefault(principal.Id, walletDevnet.Id);

        await DbContext.PrincipalChainDefaults.AddAsync(defaultMainnet);
        await DbContext.PrincipalChainDefaults.AddAsync(defaultDevnet);

        // Assert: Should succeed (different chains)
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Test 6: DEFAULT_GUARD_verified_signing_only

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_WatchOnlyWallet_ShouldFail()
    {
        // Arrange: Create principal with watch-only wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Create watch-only ownership
        var ownership = TestDataFixtures.CreateVerifiedWatchOnlyOwnership(principal.Id, wallet.Id);
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to set watch-only wallet as default
        var defaultWallet = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(defaultWallet);

        // Assert: Should fail due to business rule check constraint
        // NOTE: This test will FAIL initially because check constraint doesn't exist yet
        await AssertPostgreSQLCheckConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ck_default_verified_signing_only" // This constraint doesn't exist yet
        );
    }

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_PendingWallet_ShouldFail()
    {
        // Arrange: Create principal with pending wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Create pending ownership
        var ownership = TestDataFixtures.CreatePendingSigningOwnership(principal.Id, wallet.Id);
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await UnitOfWork.SaveChangesAsync();

        // Act: Try to set pending wallet as default
        var defaultWallet = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(defaultWallet);

        // Assert: Should fail due to business rule check constraint
        // NOTE: This test will FAIL initially because check constraint doesn't exist yet
        await AssertPostgreSQLCheckConstraintViolation(
            async () => await UnitOfWork.SaveChangesAsync(),
            "ck_default_verified_signing_only" // This constraint doesn't exist yet
        );
    }

    [Test]
    public async Task Test_DEFAULT_GUARD_verified_signing_only_VerifiedSigningWallet_ShouldSucceed()
    {
        // Arrange: Create principal with verified signing wallet
        var principal = TestDataFixtures.CreatePrincipalA();
        var wallet = TestDataFixtures.CreateW1Main();

        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // Create verified signing ownership
        var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await UnitOfWork.SaveChangesAsync();

        // Act: Set verified signing wallet as default
        var defaultWallet = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(defaultWallet);

        // Assert: Should succeed
        await UnitOfWork.SaveChangesAsync(); // Should not throw
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task Integration_CompleteWalletLifecycle_ShouldRespectAllConstraints()
    {
        // Arrange: Create complete scenario
        var (principal, wallet, ownership) = TestDataFixtures.CreateMainnetScenario();

        // Act & Assert: Step-by-step validation of all constraints

        // 1. Save principal and wallet
        await PrincipalRepository.AddAsync(principal);
        await DbContext.Wallets.AddAsync(wallet);
        await UnitOfWork.SaveChangesAsync();

        // 2. Add ownership
        await DbContext.WalletOwnerships.AddAsync(ownership);
        await UnitOfWork.SaveChangesAsync();

        // 3. Set as default (should succeed - verified signing)
        var defaultWallet = TestDataFixtures.CreateMainnetSolanaDefault(principal.Id, wallet.Id);
        await DbContext.PrincipalChainDefaults.AddAsync(defaultWallet);
        await UnitOfWork.SaveChangesAsync();

        // 4. Verify all constraints are enforced in complete scenario
        var loadedPrincipal = await PrincipalRepository.GetByIdAsync(principal.Id);
        loadedPrincipal.ShouldNotBeNull();
        loadedPrincipal.WalletOwnerships.Count.ShouldBe(1);
        loadedPrincipal.PrincipalChainDefaults.Count.ShouldBe(1);
    }

    #endregion
}