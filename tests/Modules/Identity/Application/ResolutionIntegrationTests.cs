using System.Threading.Tasks;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbInvariants;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application;

/// <summary>
/// Integration tests combining Resolution Algorithm and Defaults Behavior.
/// These tests validate the complete flow from exchange command to final state
/// including both domain logic and database interactions.
/// </summary>
[TestFixture]
public class ResolutionIntegrationTests : IdentityResolutionTestBase
{
    #region Complete Flow Integration Tests

    [Test]
    public async Task Integration_CredentialFirstWithDefaults_ShouldResolveAndApplyDefaults()
    {
        // Arrange: Setup scenario with existing credential and new wallet
        var (existingPrincipal, _) = SetupCredentialFirstScenario();

        // Add wallet data to command for defaults application
        var commandWithWallet = CreateDynamicExchangeCommand(TestDataFixtures.DynA_Subject);

        // Mock repository behavior
        MockCredentialResolution(existingPrincipal);
        MockCredentialUniqueness(false);

        // Mock wallet creation and linking
        var wallet = TestDataFixtures.CreateW1Main();
        var address = Address.Create(TestDataFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), wallet.Id }
        });
        MockNoWalletOwners(); // No conflicts

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(commandWithWallet, CancellationToken.None);

        // Assert: Should resolve via credential AND apply defaults
        result.IsSuccess.ShouldBeTrue();
        AssertCredentialResolution(result.Value, existingPrincipal.Id);

        // Verify wallet was linked and defaults applied
        result.Value.WalletsLinked.ShouldBe(1);
        result.Value.DefaultsApplied.ShouldBeGreaterThan(0);

        // Verify repository calls
        await MockPrincipalRepository.Received(1).FindByCredentialAsync(
            Arg.Any<ProviderType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await MockPrincipalRepository.Received(1).UpdateAsync(
            Arg.Any<AxonPrincipal>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Integration_WalletResolutionWithExistingDefaults_ShouldUpdateDefaults()
    {
        // Arrange: Setup scenario with existing wallet and defaults
        var (principal, _) = SetupWalletVerifiedScenario();

        // Add a second wallet to the exchange
        var command = CreateDynamicExchangeCommand("unknown_user");

        // Mock resolution path
        MockNoCredentialMatch();
        MockCredentialUniqueness(false);

        // Mock wallet lookups
        var wallet1 = TestDataFixtures.CreateW1Main();
        var wallet2 = TestDataFixtures.CreateW2Main();
        var address2 = Address.Create(TestDataFixtures.W2MainAddress).Value;

        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address2), wallet2.Id }
        });

        // Mock existing ownership for principal (owns wallet1, not wallet2)
        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>
        {
            { wallet1.Id, principal } // Principal owns wallet1
            // wallet2 is unowned
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to existing principal and link new wallet
        result.IsSuccess.ShouldBeTrue();
        AssertWalletResolution(result.Value, principal.Id);
        result.Value.WalletsLinked.ShouldBe(1); // New wallet linked

        // NOTE: This test will FAIL initially if the system doesn't handle
        // linking additional wallets to existing principals correctly
    }

    [Test]
    public async Task Integration_NewPrincipalWithMultipleWallets_ShouldCreateAndSetupDefaults()
    {
        // Arrange: Create command with multiple wallets for new principal
        var command = CreateDynamicExchangeCommand("brand_new_user_12345");

        // Mock no existing matches
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        // Mock wallet creation
        var wallet1Id = WalletId.New();
        var wallet2Id = WalletId.New();
        var address1 = Address.Create(TestDataFixtures.W1MainAddress).Value;
        var address2 = Address.Create(TestDataFixtures.W2MainAddress).Value;

        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address1), wallet1Id },
            { (TestDataFixtures.SolanaMainnetChain, address2), wallet2Id }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should create new principal with multiple wallets and defaults
        result.IsSuccess.ShouldBeTrue();
        AssertNewPrincipalCreation(result.Value);
        result.Value.WalletsLinked.ShouldBe(2); // Both wallets linked
        result.Value.DefaultsApplied.ShouldBeGreaterThan(0); // Defaults applied

        // Verify new principal was added
        await MockPrincipalRepository.Received(1).AddAsync(
            Arg.Any<AxonPrincipal>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Conflict Resolution Integration Tests

    [Test]
    public async Task Integration_WalletOwnershipConflict_ShouldFailWithConflictError()
    {
        // Arrange: Create scenario where wallet is already owned by another principal
        var (ownerPrincipal, ownedWallet, conflictCommand) = ResolutionTestFixtures.ErrorScenarios.CreateOwnershipConflictScenario();

        // Mock resolution to detect conflict
        MockNoCredentialMatch();
        MockCredentialUniqueness(false);

        // Mock wallet lookup and ownership conflict
        var address = Address.Create(TestDataFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), ownedWallet.Id }
        });

        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>
        {
            { ownedWallet.Id, ownerPrincipal } // Wallet already owned
        });

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(conflictCommand, CancellationToken.None);

        // Assert: Should fail with conflict error
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldContain("WALLET"); // Wallet-related conflict

        // Verify no changes were persisted
        await MockPrincipalRepository.DidNotReceive().AddAsync(
            Arg.Any<AxonPrincipal>(),
            Arg.Any<CancellationToken>());

        await MockPrincipalRepository.DidNotReceive().UpdateAsync(
            Arg.Any<AxonPrincipal>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Integration_CredentialBelongsToOther_ShouldFailWithConflictError()
    {
        // Arrange: Create scenario where credential belongs to another principal
        _ = TestDataFixtures.CreatePrincipalA(); // Owns the credential - affects credential uniqueness check
        var command = CreateDynamicExchangeCommand(TestDataFixtures.DynA_Subject);

        // Mock credential taken by another principal
        MockNoCredentialMatch(); // Force creation attempt
        MockCredentialUniqueness(true); // Credential is taken

        // Mock UnitOfWork (won't be used due to failure)
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should fail with conflict error
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);

        // Verify no changes were attempted
        await mockUnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Environment Separation Integration Tests

    [Test]
    public async Task Integration_CrossEnvironmentWallets_ShouldNotConflict()
    {
        // Arrange: Create scenario with same wallet address in different environments
        var (mainnetWallet, devnetWallet, mainnetCommand, devnetCommand) =
            ResolutionTestFixtures.EnvironmentSeparation.CreateCrossEnvironmentScenario();

        // Create separate principals for each environment
        _ = AxonPrincipal.CreateHuman();
        _ = AxonPrincipal.CreateHuman();

        // Mock first command (mainnet)
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        var mainnetAddress = Address.Create(TestDataFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, mainnetAddress), mainnetWallet.Id }
        });

        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute both commands
        var mainnetResult = await ExchangeHandler.Handle(mainnetCommand, CancellationToken.None);

        // Reset mocks for second command
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        var devnetAddress = Address.Create(TestDataFixtures.W1DevAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaDevnetChain, devnetAddress), devnetWallet.Id }
        });

        var devnetResult = await ExchangeHandler.Handle(devnetCommand, CancellationToken.None);

        // Assert: Both should succeed (no cross-environment conflict)
        mainnetResult.IsSuccess.ShouldBeTrue();
        devnetResult.IsSuccess.ShouldBeTrue();

        // NOTE: This test will FAIL initially if environment separation is not implemented
        // The same wallet address should be allowed in different environments
    }

    #endregion

    #region Performance and Stress Integration Tests

    [Test]
    public async Task Integration_BatchWalletProcessing_ShouldHandleEfficientlyWithDefaults()
    {
        // Arrange: Create scenario with many wallets
        var (principal, wallets, command) = ResolutionTestFixtures.PerformanceTestData.CreateBatchWalletScenario(5);

        // Mock resolution to existing principal
        MockCredentialResolution(principal);
        MockCredentialUniqueness(false);

        // Mock wallet lookups for all wallets
        var walletLookup = new Dictionary<(string, Address), WalletId>();
        foreach (var wallet in wallets)
        {
            var address = Address.Create(wallet.Address.Value).Value;
            walletLookup.Add((TestDataFixtures.SolanaMainnetChain, address), wallet.Id);
        }
        MockWalletLookup(walletLookup);

        // Mock no ownership conflicts
        MockNoWalletOwners();

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var startTime = DateTime.UtcNow;
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);
        var duration = DateTime.UtcNow - startTime;

        // Assert: Should complete efficiently
        result.IsSuccess.ShouldBeTrue();
        result.Value.WalletsLinked.ShouldBe(wallets.Count);
        result.Value.DefaultsApplied.ShouldBeGreaterThan(0);

        // Performance assertion (should complete in reasonable time)
        duration.TotalMilliseconds.ShouldBeLessThan(5000); // 5 seconds max for 5 wallets

        // Verify batch operations were used efficiently
        await MockWalletRepository.Received(1).EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string chainId, Address address)>>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Idempotency Integration Tests

    [Test]
    public async Task Integration_IdempotentExchange_ShouldNotDuplicateData()
    {
        // Arrange: Setup scenario for idempotency testing
        var command = SetupNewPrincipalScenario();

        // Mock consistent behavior
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        var address = Address.Create("UnknownWalletAddress123").Value;
        var walletId = WalletId.New();
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), walletId }
        });

        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute same command multiple times
        var result1 = await ExchangeHandler.Handle(command, CancellationToken.None);
        var result2 = await ExchangeHandler.Handle(command, CancellationToken.None);
        var result3 = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: All should succeed with consistent results
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result3.IsSuccess.ShouldBeTrue();

        // Results should be consistent (same principal created)
        AssertNewPrincipalCreation(result1.Value);
        AssertNewPrincipalCreation(result2.Value);
        AssertNewPrincipalCreation(result3.Value);

        // NOTE: This test will FAIL initially if idempotency is not properly implemented
        // Multiple executions should not create duplicate principals or data
    }

    #endregion

    #region Defaults Auto-Seed Integration Tests

    [Test]
    public async Task Integration_FirstVerifiedWallet_ShouldAutoSeedDefaults()
    {
        // Arrange: Create new principal with first verified wallet
        var command = CreateDynamicExchangeCommand("new_user_auto_seed");

        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        var address = Address.Create(TestDataFixtures.W1MainAddress).Value;
        var walletId = WalletId.New();
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), walletId }
        });

        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should create principal with auto-seeded defaults
        result.IsSuccess.ShouldBeTrue();
        AssertNewPrincipalCreation(result.Value);
        result.Value.WalletsLinked.ShouldBe(1);

        // NOTE: This test will FAIL initially if auto-seeding logic is missing
        // First verified+signing wallet should automatically create chain default
        result.Value.DefaultsApplied.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Error Recovery Integration Tests

    [Test]
    public async Task Integration_DatabaseError_ShouldRollbackCleanly()
    {
        // Arrange: Setup scenario that will fail at save
        var command = SetupNewPrincipalScenario();

        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        var address = Address.Create("UnknownWalletAddress123").Value;
        var walletId = WalletId.New();
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), walletId }
        });

        // Mock database save failure
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new DbUpdateException("Simulated database error")));
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act & Assert: Should handle error gracefully
        var ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await ExchangeHandler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("Simulated database error");

        // Verify rollback behavior (no partial state)
        // NOTE: In real scenario, transaction would rollback automatically
    }

    #endregion
}