using Axon.Modules.Identity.Application.Tests._TestInfrastructure.Fixtures;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.Extensions.Time.Testing;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.PrincipalResolution;

/// <summary>
/// Base class for Identity Resolution tests implementing TDD approach from section B.
/// Provides mock infrastructure for testing resolution algorithm without external dependencies.
/// </summary>
[TestFixture]
public abstract class PrincipalResolutionTestBase
{
    #region Test Infrastructure

    protected IAxonPrincipalWriteRepository MockPrincipalRepository { get; private set; } = null!;
    protected IWalletWriteRepository MockWalletRepository { get; private set; } = null!;
    protected ICurrentUserService MockCurrentUserService { get; private set; } = null!;
    protected IMemoryCache MockMemoryCache { get; private set; } = null!;
    protected IHttpContextAccessor MockHttpContextAccessor { get; private set; } = null!;
    protected ILogger<ExchangeCredentialHandler> MockLogger { get; private set; } = null!;
    protected FakeTimeProvider MockTimeProvider { get; private set; } = null!;

    protected ExchangeCredentialHandler ExchangeHandler { get; private set; } = null!;

    #endregion

    #region Test Setup

    [SetUp]
    public void SetUp()
    {
        // Initialize mocks for Domain-level tests
        InitializeMocks();

        // Create handler with mocked dependencies for new simplified constructor
        var mockOrchestrator = Substitute.For<IAuthenticationOrchestrator>();
        var mockJwtTokenService = Substitute.For<IJwtTokenService>();

        // Setup mock orchestrator to return valid AuthenticationResponse with AdditionalData
        var mockResponse = new AuthenticationResponse(
            AccessToken: "mock-access-token",
            UserId: Guid.NewGuid(),
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 1,
                ["wallets_linked"] = 1,
                ["defaults_applied"] = 1,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        mockOrchestrator.ExchangeDynamicTokenAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        ExchangeHandler = new ExchangeCredentialHandler(
            MockCurrentUserService,
            mockOrchestrator,
            mockJwtTokenService,
            MockLogger);
    }

    private void InitializeMocks()
    {
        MockPrincipalRepository = Substitute.For<IAxonPrincipalWriteRepository>();
        MockWalletRepository = Substitute.For<IWalletWriteRepository>();
        MockCurrentUserService = Substitute.For<ICurrentUserService>();
        MockMemoryCache = Substitute.For<IMemoryCache>();
        MockHttpContextAccessor = Substitute.For<IHttpContextAccessor>();
        MockLogger = Substitute.For<ILogger<ExchangeCredentialHandler>>();
        var testTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        MockTimeProvider = new FakeTimeProvider(testTime);

        // Configure default mock behavior
        ConfigureDefaultMockBehavior();
    }

    private void ConfigureDefaultMockBehavior()
    {
        // MockTimeProvider is already set in InitializeMocks

        // Configure memory cache to behave normally
        MockMemoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()).Returns(false);

        // Configure HTTP context
        var mockHttpContext = Substitute.For<HttpContext>();
        mockHttpContext.Items.Returns(new Dictionary<object, object?>());
        MockHttpContextAccessor.HttpContext.Returns(mockHttpContext);
    }

    #endregion

    #region Test Data Builders for Resolution Scenarios

    /// <summary>
    /// Creates an ExchangeCredentialCommand for Dynamic JWT resolution testing.
    /// </summary>
    protected static ExchangeCredentialCommand CreateDynamicExchangeCommand(
        string axonUserId = "test_user_123")
    {
        return new ExchangeCredentialCommand($"bearer-token-{axonUserId}");
    }

    /// <summary>
    /// Creates wallet exchange data for testing.
    /// </summary>
    protected static ExchangeWalletData CreateWalletExchangeData(
        string address,
        string chain = TestDataFixtures.SolanaMainnetChain)
    {
        return new ExchangeWalletData(chain, address);
    }

    /// <summary>
    /// Sets up mock to return existing credential-based principal.
    /// </summary>
    protected void MockCredentialResolution(AxonPrincipal principal)
    {
        MockPrincipalRepository
            .FindByCredentialAsync(
                ProviderType.Dynamic,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(principal);
    }

    /// <summary>
    /// Sets up mock to return no credential match (forces wallet resolution).
    /// </summary>
    protected void MockNoCredentialMatch()
    {
        MockPrincipalRepository
            .FindByCredentialAsync(
                ProviderType.Dynamic,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);
    }

    /// <summary>
    /// Sets up mock to return verified signing wallet owners.
    /// </summary>
    protected void MockWalletOwnerResolution(Dictionary<WalletId, AxonPrincipal> walletOwners)
    {
        MockPrincipalRepository
            .FindVerifiedSigningOwnersAsync(
                Arg.Any<IEnumerable<WalletId>>(),
                Arg.Any<CancellationToken>())
            .Returns(walletOwners);
    }

    /// <summary>
    /// Sets up mock to return no wallet owners (forces new principal creation).
    /// </summary>
    protected void MockNoWalletOwners()
    {
        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>());
    }

    /// <summary>
    /// Sets up mock wallet repository to return wallet lookups.
    /// </summary>
    protected void MockWalletLookup(Dictionary<(string chainId, Address address), WalletId> walletLookup)
    {
        MockWalletRepository
            .EnsureManyByChainAndAddressAsync(
                Arg.Any<IEnumerable<(string chainId, Address address)>>(),
                Arg.Any<CancellationToken>())
            .Returns(walletLookup);
    }

    /// <summary>
    /// Sets up mock to simulate credential uniqueness check.
    /// </summary>
    protected void MockCredentialUniqueness(bool isTaken = false)
    {
        MockPrincipalRepository
            .IsCredentialTakenAsync(
                ProviderType.Dynamic,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(isTaken);
    }

    #endregion

    #region Resolution Test Helpers

    /// <summary>
    /// Creates a scenario with existing credential for testing credential-first resolution.
    /// </summary>
    protected (AxonPrincipal principal, ExchangeCredentialCommand command) SetupCredentialFirstScenario()
    {
        // Create principal with existing Dynamic credential
        var principal = TestDataFixtures.CreatePrincipalA();

        // Create command that should resolve to this principal via credential
        var command = CreateDynamicExchangeCommand(TestDataFixtures.DynA_Subject);

        // Mock repository to return this principal for credential lookup
        MockCredentialResolution(principal);
        MockCredentialUniqueness(false);

        return (principal, command);
    }

    /// <summary>
    /// Creates a scenario with verified signing wallet for testing wallet resolution.
    /// </summary>
    protected (AxonPrincipal principal, ExchangeCredentialCommand command) SetupWalletVerifiedScenario()
    {
        // Create principal with verified signing wallet
        // Create principal with verified wallet ownership
        var principal = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();
        var ownership = TestDataFixtures.CreateVerifiedSigningOwnership(principal.Id, wallet.Id);
        principal.LinkWalletOwnership(ownership, (_, _, _) => Result.Success<bool, Error>(false));

        // Create command with wallet data
        var walletData = CreateWalletExchangeData(TestDataFixtures.W1MainAddress);
        var command = CreateDynamicExchangeCommand("test_user_123");

        // Mock no credential match (force wallet resolution)
        MockNoCredentialMatch();
        MockCredentialUniqueness(false);

        // Mock wallet lookup
        var address = Address.Create(TestDataFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), wallet.Id }
        });

        // Mock wallet ownership
        MockWalletOwnerResolution(new Dictionary<WalletId, AxonPrincipal>
        {
            { wallet.Id, principal }
        });

        return (principal, command);
    }

    /// <summary>
    /// Creates a scenario with ambiguous ownership for testing tie-breaking.
    /// </summary>
    protected (AxonPrincipal principalA, AxonPrincipal principalB, ExchangeCredentialCommand command)
        SetupAmbiguousOwnershipScenario()
    {
        // Create two principals with non-verified ownership of same wallet
        // Create ambiguous ownership scenario manually
        var principalA = AxonPrincipal.CreateHuman();
        var principalB = AxonPrincipal.CreateHuman();
        var wallet = TestDataFixtures.CreateW1Main();

        // Create non-verified ownerships for tie-breaking logic
        var ownershipA = WalletOwnership.Create(
            principalA.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Pending);

        var ownershipB = WalletOwnership.Create(
            principalB.Id,
            wallet.Id,
            AccessMode.WatchOnly,
            OwnershipStatus.Verified);

        // Link ownerships
        principalA.LinkWalletOwnership(ownershipA, (_, _, _) => Result.Success<bool, Error>(false));
        principalB.LinkWalletOwnership(ownershipB, (_, _, _) => Result.Success<bool, Error>(false));

        // Create command with wallet data
        var walletData = CreateWalletExchangeData(TestDataFixtures.W1MainAddress);
        var command = CreateDynamicExchangeCommand("test_user_123");

        // Mock no credential and no verified owners (will need domain logic for tie-breaking)
        MockNoCredentialMatch();
        MockNoWalletOwners(); // No verified signing owners
        MockCredentialUniqueness(false);

        // Mock wallet lookup
        var address = Address.Create(TestDataFixtures.W1MainAddress).Value;
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), wallet.Id }
        });

        return (principalA, principalB, command);
    }

    /// <summary>
    /// Creates a scenario for testing new principal creation.
    /// </summary>
    protected ExchangeCredentialCommand SetupNewPrincipalScenario()
    {
        // Create command with unknown credential and wallet
        var command = CreateDynamicExchangeCommand("unknown_user_789");

        // Mock no matches anywhere
        MockNoCredentialMatch();
        MockNoWalletOwners();
        MockCredentialUniqueness(false);

        // Mock wallet creation for new wallet
        var address = Address.Create("UnknownWalletAddress123").Value;
        var newWalletId = WalletId.New();
        MockWalletLookup(new Dictionary<(string, Address), WalletId>
        {
            { (TestDataFixtures.SolanaMainnetChain, address), newWalletId }
        });

        return command;
    }

    #endregion

    #region Cleanup

    [TearDown]
    public void TearDown()
    {
        MockMemoryCache?.Dispose();
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Asserts that the resolution resulted in credential-first path.
    /// </summary>
    protected static void AssertCredentialResolution(ExchangeOutcome outcome, AxonUserId expectedPrincipalId)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        outcome.AxonUserId.ShouldBe(expectedPrincipalId);
        outcome.Created.ShouldBeFalse(); // Existing principal
    }

    /// <summary>
    /// Asserts that the resolution resulted in wallet-based path.
    /// </summary>
    protected static void AssertWalletResolution(ExchangeOutcome outcome, AxonUserId expectedPrincipalId)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        outcome.AxonUserId.ShouldBe(expectedPrincipalId);
        outcome.Created.ShouldBeFalse(); // Existing principal
        outcome.WalletsLinked.ShouldBeGreaterThan(0); // Wallet was processed
    }

    /// <summary>
    /// Asserts that the resolution resulted in new principal creation.
    /// </summary>
    protected static void AssertNewPrincipalCreation(ExchangeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        outcome.Created.ShouldBeTrue(); // New principal
        outcome.AxonUserId.ShouldNotBe(default(AxonUserId)); // Principal was created
    }

    /// <summary>
    /// Asserts that resolution failed with expected conflict.
    /// </summary>
    protected static void AssertResolutionConflict(Result<ExchangeOutcome, Error> result, string expectedErrorCode)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedErrorCode);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    #endregion
}

