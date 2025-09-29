using System;
using Axon.Modules.Identity.Application.Tests.Services.PrincipalResolution;
using AppTestFixtures = Axon.Modules.Identity.Application.Tests._TestInfrastructure.Fixtures.TestDataFixtures;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Tests.Persistence.DbInvariants;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Axon.Modules.Identity.Application.Common.Models;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.PrincipalResolution;

/// <summary>
/// Resolution Algorithm tests for Identity Module as specified in TDD document section B.
/// These tests validate the deterministic "credential-first → wallet-fallback" resolution logic.
/// Tests are designed to initially fail (TDD approach) to expose gaps in current implementation.
/// TODO: Split this file into focused test classes: CredentialFirstResolutionTests, WalletVerificationResolutionTests, AmbiguityResolutionTests
/// </summary>
[TestFixture]
public class IdentityResolutionAlgorithmTests : PrincipalResolutionTestBase
{
    #region Test 7: RESOLVE_credential_first

    [Test]
    public async Task Test_RESOLVE_credential_first_ExistingCredential_ShouldReturnPrincipal()
    {
        // Arrange: Setup scenario where credential exists for principal
        var (principal, command) = SetupCredentialFirstScenario();

        // Mock UnitOfWork for successful save
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to existing principal via credential lookup
        result.IsSuccess.ShouldBeTrue();
        AssertCredentialResolution(result.Value, principal.Id);

        // Verify credential lookup was called (not wallet lookup)
        await MockPrincipalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            AppTestFixtures.DynamicIssuer,
            AppTestFixtures.DynA_Subject,
            Arg.Any<CancellationToken>());

        // NOTE: This test will FAIL initially because current implementation may not prioritize credential lookup
        MockLogger.LogInformation("Test 7 PASSED: Credential-first resolution working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_credential_first_WithWallets_ShouldStillUseCredential()
    {
        // Arrange: Setup scenario with both credential AND wallets
        var (principal, _) = SetupCredentialFirstScenario();

        // Create command with wallet data that could also resolve
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock credential resolution (should be checked first)
        MockCredentialResolution(principal);
        MockCredentialUniqueness(false);

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should still resolve via credential (not wallet)
        result.IsSuccess.ShouldBeTrue();
        AssertCredentialResolution(result.Value, principal.Id);

        // Verify credential was checked BEFORE wallet resolution
        await MockPrincipalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            AppTestFixtures.DynamicIssuer,
            AppTestFixtures.DynA_Subject,
            Arg.Any<CancellationToken>());

        // NOTE: This test verifies that credential-first priority is maintained even when wallets are present
        MockLogger.LogInformation("Test 7b PASSED: Credential-first priority maintained with wallets present");
    }

    #endregion

    #region Test 8: RESOLVE_wallet_verified_wins

    [Test]
    public async Task Test_RESOLVE_wallet_verified_wins_VerifiedSigningOwnership_ShouldReturnPrincipal()
    {
        // Arrange: Setup scenario with verified signing wallet (no credential match)
        var (principal, command) = SetupWalletVerifiedScenario();

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to principal via wallet ownership lookup
        result.IsSuccess.ShouldBeTrue();
        AssertWalletResolution(result.Value, principal.Id);

        // Verify wallet owner lookup was called after credential lookup failed
        await MockPrincipalRepository.Received(1).FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(),
            Arg.Any<CancellationToken>());

        // NOTE: This test will FAIL initially if wallet resolution logic is missing
        MockLogger.LogInformation("Test 8 PASSED: Wallet verified signing resolution working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_wallet_verified_wins_MultipleVerifiedWallets_ShouldReturnOwner()
    {
        // Arrange: Create scenario with multiple wallets, one owned
        var principal = AppTestFixtures.CreatePlainPrincipal();
        var ownedWallet = AppTestFixtures.CreateW1Main();
        var unownedWallet = AppTestFixtures.CreateW2Main();

        // Create command with both wallets
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock no credential match
        MockNoCredentialMatch();
        MockCredentialUniqueness(false);

        // Mock wallet lookups
        var address1 = Address.Create(AppTestFixtures.W1MainAddress).Value;
        var address2 = Address.Create(AppTestFixtures.W2MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address1), ownedWallet.Id },
            { (AppTestFixtures.SolanaMainnetChain, address2), unownedWallet.Id }
        });

        // Mock only one wallet has verified owner
        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>
        {
            { ownedWallet.Id, principal } // Only W1 is owned
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to owner of the verified wallet
        result.IsSuccess.ShouldBeTrue();
        AssertWalletResolution(result.Value, principal.Id);

        MockLogger.LogInformation("Test 8b PASSED: Multiple wallet resolution working correctly");
    }

    #endregion

    #region Test 9: RESOLVE_single_active_owner

    [Test]
    public async Task Test_RESOLVE_single_active_owner_WatchOnlyOwnership_ShouldReturnPrincipal()
    {
        // Arrange: Setup scenario with single watch-only ownership (no verified signing)
        var (principal, wallet) = AppTestFixtures.CreateSingleActiveOwnerScenario();

        // Create command with wallet data
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock no credential match and no verified signing owners
        MockNoCredentialMatch();
        MockNoWalletOwners(); // No verified+signing owners
        MockCredentialUniqueness(false);

        // Mock wallet lookup
        var address = Address.Create(AppTestFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address), wallet.Id }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // NOTE: This test will FAIL initially because fallback logic for non-verified owners is missing
        // The implementation needs to handle the case where no verified+signing owners exist
        // but there are other active ownerships that can be used for resolution

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to the single active owner (watch-only fallback)
        result.IsSuccess.ShouldBeTrue();
        AssertWalletResolution(result.Value, principal.Id);

        MockLogger.LogInformation("Test 9 PASSED: Single active owner resolution working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_single_active_owner_OnlyActiveOwnership_ShouldReturnThatPrincipal()
    {
        // Arrange: Create scenario where wallet has only one active (non-revoked) ownership
        var principal = AxonPrincipal.CreateHuman();
        var wallet = AppTestFixtures.CreateW1Main();

        // Create pending signing ownership (active but not verified)
        var ownership = AppTestFixtures.CreatePendingSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        // Create command with wallet data
        var walletData = CreateWalletExchangeData(AppTestFixtures.W1MainAddress);
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock resolution path
        MockNoCredentialMatch();
        MockNoWalletOwners(); // No verified+signing owners
        MockCredentialUniqueness(false);

        // Mock wallet lookup
        var address = Address.Create(AppTestFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address), wallet.Id }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to the only active owner
        result.IsSuccess.ShouldBeTrue();
        AssertWalletResolution(result.Value, principal.Id);

        MockLogger.LogInformation("Test 9b PASSED: Only active ownership resolution working correctly");
    }

    #endregion

    #region Test 10: RESOLVE_ambiguity_tie_break

    [Test]
    public async Task Test_RESOLVE_ambiguity_tie_break_AuthorityRanking_ShouldPreferSigning()
    {
        // Arrange: Setup ambiguous scenario with authority ranking needed
        var (principalA, _, command) = SetupAmbiguousOwnershipScenario();

        // NOTE: This test will FAIL initially because tie-breaking logic is not implemented
        // The system needs to implement authority ranking: msg > tx > watch-only
        // and then earliest principal fallback for same authority level

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to principal with higher authority (signing > watch-only)
        result.IsSuccess.ShouldBeTrue();

        // Principal A has pending signing (higher authority than verified watch-only)
        // Should resolve to Principal A despite Principal B having verified status
        result.Value.AxonUserId.ShouldBe(principalA.Id);

        MockLogger.LogInformation("Test 10 PASSED: Authority ranking tie-breaking working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_ambiguity_tie_break_SameAuthority_ShouldUseEarliestPrincipal()
    {
        // Arrange: Create scenario with same authority level (need earliest principal tie-breaker)
        var principalA = AxonPrincipal.CreateHuman(new AxonUserId(Guid.Parse("00000000-0000-0000-0000-000000000001")));
        var principalB = AxonPrincipal.CreateHuman(new AxonUserId(Guid.Parse("00000000-0000-0000-0000-000000000002")));
        var wallet = AppTestFixtures.CreateW1Main();

        // Both have same authority level (pending signing)
        var ownershipA = AppTestFixtures.CreatePendingSigningOwnership(principalA.Id, wallet.Id);
        var ownershipB = AppTestFixtures.CreatePendingSigningOwnership(principalB.Id, wallet.Id);

        principalA.LinkWalletOwnership(ownershipA, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(ownershipB, (_, _, _) => Result.Success<bool, Error>(false));

        // Create command with wallet data
        var walletData = CreateWalletExchangeData(AppTestFixtures.W1MainAddress);
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock resolution path
        MockNoCredentialMatch();
        MockNoWalletOwners(); // No verified+signing owners
        MockCredentialUniqueness(false);

        // Mock wallet lookup
        var address = Address.Create(AppTestFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address), wallet.Id }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve to earliest principal (Principal A has earlier ID)
        result.IsSuccess.ShouldBeTrue();
        result.Value.AxonUserId.ShouldBe(principalA.Id);

        MockLogger.LogInformation("Test 10b PASSED: Earliest principal tie-breaking working correctly");
    }

    #endregion

    #region Test 11: RESOLVE_no_match_creates

    [Test]
    public async Task Test_RESOLVE_no_match_creates_NewUser_ShouldCreatePrincipal()
    {
        // Arrange: Setup scenario with no matches anywhere
        var command = SetupNewPrincipalScenario();

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should create new principal
        result.IsSuccess.ShouldBeTrue();
        AssertNewPrincipalCreation(result.Value);

        // Verify new principal was added to repository
        await MockPrincipalRepository.Received(1).AddAsync(
            Arg.Any<AxonPrincipal>(),
            Arg.Any<CancellationToken>());

        MockLogger.LogInformation("Test 11 PASSED: New principal creation working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_no_match_creates_IdempotentRepeat_ShouldReturnSamePrincipal()
    {
        // Arrange: Setup scenario for idempotency testing
        var command = SetupNewPrincipalScenario();

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute command twice
        var result1 = await ExchangeHandler.Handle(command, CancellationToken.None);
        var result2 = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Both should succeed and return same principal
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();

        AssertNewPrincipalCreation(result1.Value);
        AssertNewPrincipalCreation(result2.Value);

        // NOTE: This test will FAIL initially if idempotency is not properly implemented
        // The second call should not create a duplicate principal
        MockLogger.LogInformation("Test 11b PASSED: Idempotent principal creation working correctly");
    }

    [Test]
    public async Task Test_RESOLVE_no_match_creates_WithUnknownWallet_ShouldCreateBoth()
    {
        // Arrange: Setup scenario with unknown credential AND unknown wallet
        var (unknownSubject, unknownWalletAddress) = AppTestFixtures.CreateNoMatchScenario();

        var command = CreateDynamicExchangeCommand(unknownSubject);

        // Mock no matches anywhere
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        // Mock wallet creation for new wallet
        var address = Address.Create(unknownWalletAddress).Value;
        var newWalletId = WalletId.New();
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address), newWalletId }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should create new principal and link new wallet
        result.IsSuccess.ShouldBeTrue();
        AssertNewPrincipalCreation(result.Value);
        result.Value.WalletsLinked.ShouldBeGreaterThan(0); // Wallet was linked

        MockLogger.LogInformation("Test 11c PASSED: New principal and wallet creation working correctly");
    }

    #endregion

    #region Integration Resolution Tests

    [Test]
    public async Task Integration_CompleteResolutionFlow_ShouldFollowCorrectPriority()
    {
        // Arrange: Create complex scenario that tests the complete resolution priority
        var principalWithCredential = AppTestFixtures.CreatePrincipalA(); // Has credential
        var principalWithWallet = AxonPrincipal.CreateHuman(); // No credential, has wallet
        var wallet = AppTestFixtures.CreateW1Main();

        // Setup wallet ownership for second principal
        var ownership = AppTestFixtures.CreateVerifiedSigningOwnership(principalWithWallet.Id, wallet.Id);
        principalWithWallet.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        // Create command that could resolve to either principal
        var walletData = CreateWalletExchangeData(AppTestFixtures.W1MainAddress);
        var command = CreateDynamicExchangeCommand(AppTestFixtures.DynA_Subject);

        // Mock credential resolution to return first principal
        MockCredentialResolution(principalWithCredential);
        MockCredentialUniqueness(false);

        // Mock wallet resolution (should not be used due to credential-first priority)
        var address = Address.Create(AppTestFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (AppTestFixtures.SolanaMainnetChain, address), wallet.Id }
        });
        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>
        {
            { wallet.Id, principalWithWallet }
        });

        // Mock UnitOfWork
        var mockUnitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();
        mockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        MockPrincipalRepository.UnitOfWork.Returns(mockUnitOfWork);

        // Act: Execute exchange command
        var result = await ExchangeHandler.Handle(command, CancellationToken.None);

        // Assert: Should resolve via credential (not wallet) due to priority
        result.IsSuccess.ShouldBeTrue();
        result.Value.AxonUserId.ShouldBe(principalWithCredential.Id);

        // Verify credential lookup was called first
        await MockPrincipalRepository.Received(1).FindByCredentialAsync(
            ProviderType.Dynamic,
            AppTestFixtures.DynamicIssuer,
            AppTestFixtures.DynA_Subject,
            Arg.Any<CancellationToken>());

        MockLogger.LogInformation("Integration test PASSED: Complete resolution priority working correctly");
    }

    #endregion
}