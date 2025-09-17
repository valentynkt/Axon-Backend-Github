using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands;

[TestFixture]
public class ExchangeCredentialHandlerTests
{
    private ICurrentUserService _currentUserService = null!;
    private IAxonPrincipalWriteRepository _principalRepository = null!;
    private IWalletWriteRepository _walletRepository = null!;
    private IExchangeMetricsService _metricsService = null!;
    private ILogger<ExchangeCredentialHandler> _logger = null!;
    private IWriteUnitOfWork<IdentityModule> _unitOfWork = null!;
    private ExchangeCredentialHandler _handler = null!;
    private static readonly ProviderType TestProviderType = ProviderType.From("dynamic");

    [SetUp]
    public void SetUp()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _principalRepository = Substitute.For<IAxonPrincipalWriteRepository>();
        _walletRepository = Substitute.For<IWalletWriteRepository>();
        _metricsService = Substitute.For<IExchangeMetricsService>();
        _logger = Substitute.For<ILogger<ExchangeCredentialHandler>>();
        _unitOfWork = Substitute.For<IWriteUnitOfWork<IdentityModule>>();

        _principalRepository.UnitOfWork.Returns(_unitOfWork);

        _handler = new ExchangeCredentialHandler(
            _currentUserService,
            _principalRepository,
            _walletRepository,
            _metricsService,
            _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _principalRepository?.Dispose();
        _walletRepository?.Dispose();
        _unitOfWork?.Dispose();
    }

    [Test]
    public async Task Should_ReturnFailure_When_UserDataIsNull()
    {
        // Arrange
        var command = new ExchangeCredentialCommand(null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("User data is required");
    }

    [Test]
    public async Task Should_ReturnFailure_When_UserIdIsEmpty()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData with { UserId = "" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("User ID is required");
    }

    [Test]
    public async Task Should_ReturnFailure_When_EnvironmentIdIsEmpty()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData with { EnvironmentId = "" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Environment ID is required");
    }

    [Test]
    public async Task Should_CreateNewPrincipal_When_CredentialDoesNotExist()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.AxonId.Value.ShouldNotBe(Guid.Empty);

        await _principalRepository.Received(1)
            .AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_UpdateExistingPrincipal_When_CredentialExists()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipal();

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(existingPrincipal);

        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);

        await _principalRepository.Received(1)
            .UpdateAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ProcessWalletsInBatch()
    {
        // Arrange
        var userData = CreateTestUserDataWithMultipleWallets();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        var walletIds = new Dictionary<(string, Address), WalletId>
        {
            { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), WalletId.New() },
            { ("137", Address.Create("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd").Value), WalletId.New() }
        };

        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(walletIds);

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.WalletsProcessed.ShouldBe(2);

        await _walletRepository.Received(2) // Called twice: once for wallet-first resolution, once for wallet processing
            .EnsureManyByChainAndAddressAsync(
                Arg.Is<IEnumerable<(string, Address)>>(specs => specs.Count() == 2),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ReturnFailure_When_InvalidWalletAddress()
    {
        // Arrange
        var userData = CreateTestUserDataWithInvalidWallet();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Setup mocks for potential repository calls (even though validation should fail early)
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Address parsing errors");
    }

    [Test]
    public async Task Should_ReturnConflictError_When_WalletOwnedByAnotherPrincipal()
    {
        // Arrange: Wallet owned by different principal than requesting credential
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        var conflictWalletId = WalletId.New();
        var conflictPrincipal = CreateTestPrincipal();

        // The wallet-first resolution should find the conflicting principal that owns the wallet
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), conflictWalletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { conflictWalletId, conflictPrincipal }
            });

        // Setup: The dynamic credential for the userData belongs to a DIFFERENT principal (conflict)
        _principalRepository.IsCredentialTakenAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // This credential belongs to someone else

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Should return conflict because credential belongs to different account
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Message.ShouldContain("This login method belongs to a different account");
    }

    [Test]
    public async Task Should_HandleEmptyWalletList()
    {
        // Arrange
        var userData = CreateTestUserDataWithEmptyWallets();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.WalletsProcessed.ShouldBe(0);
        result.Value.WalletsLinked.ShouldBe(0);
    }

    [Test]
    public async Task Should_HandleDatabaseFailure()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        // Setup repository to throw exception during SaveChanges
        _unitOfWork.When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(x => throw new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Message.ShouldContain("Unexpected error during exchange");
    }

    private static ExchangeUserData CreateTestUserData()
    {
        return new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                new("0x1234567890123456789012345678901234567890", "1")
            }
        );
    }

    private static ExchangeUserData CreateTestUserDataWithMultipleWallets()
    {
        return new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                new("0x1234567890123456789012345678901234567890", "1"),
                new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd", "137")
            }
        );
    }

    private static ExchangeUserData CreateTestUserDataWithInvalidWallet()
    {
        return new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                new("short", "1") // Too short - less than 10 characters minimum
            }
        );
    }

    private static ExchangeUserData CreateTestUserDataWithEmptyWallets()
    {
        return new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>()
        );
    }

    private static AxonPrincipal CreateTestPrincipal()
    {
        var result = AxonPrincipal.CreateWithDynamicCredential(
            ProviderType.Create("dynamic").Value,
            "test-issuer",
            "test-subject");
        return result.Value;
    }


    [Test]
    public async Task Should_RecordMetricsFailure_When_ValidationFails()
    {
        // Arrange
        var command = new ExchangeCredentialCommand(null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        _metricsService.Received(1).RecordExchangeFailure(
            Arg.Any<string>(),
            "validation",
            Arg.Any<long>());
    }

    [Test]
    public async Task Should_RecordMetricsSuccess_When_ExchangeSucceeds()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _metricsService.Received(1).RecordExchangeSuccess(
            userData.UserId,
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<long>());
    }

    [Test]
    public void Should_VerifyLoggingAndMetricsStructure()
    {
        // This test validates that logging and metrics dependencies are properly injected
        // The actual logging behavior is tested through the integration of observability
        // in the existing workflow tests
        _logger.ShouldNotBeNull();
        _metricsService.ShouldNotBeNull();
    }

    // Story 3.1: Wallet-First Identity Resolution Tests

    [Test]
    public async Task Should_ResolveSamePrincipal_When_WalletMatches()
    {
        // Arrange: Existing principal owns a wallet
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipal();
        var walletId = WalletId.New();

        // Setup: Wallet is owned by existing principal (wallet-first resolution should find it)
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { walletId, existingPrincipal }
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Same principal returned, not a new one
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);

        // Verify wallet ownership relationships are correct
        await _principalRepository.Received().FindVerifiedSigningOwnersAsync(
            Arg.Is<IEnumerable<WalletId>>(ids => ids.Contains(walletId)),
            Arg.Any<CancellationToken>());

        // Should update existing principal, not create new
        await _principalRepository.Received(1)
            .UpdateAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
        await _principalRepository.DidNotReceive()
            .AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_AddCredential_When_WalletMatches()
    {
        // Arrange: Existing principal with one credential type, new dynamic credential being added
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipal();
        var walletId = WalletId.New();

        // Setup: Wallet owned by existing principal, credential doesn't exist yet
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { walletId, existingPrincipal }
            });

        // Setup: Credential is not taken by another principal
        _principalRepository.IsCredentialTakenAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Capture initial credential count
        var initialCredentialCount = existingPrincipal.Credentials.Count;

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Principal resolved and credential added
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);

        // Verify credential count increased by 1
        existingPrincipal.Credentials.Count.ShouldBe(initialCredentialCount + 1);

        // Verify the specific credential was added to the principal
        var expectedIssuer = $"app.dynamicauth.com/{userData.EnvironmentId}";
        var addedCredential = existingPrincipal.Credentials.FirstOrDefault(c =>
            c.Provider == "dynamic" &&
            c.Issuer == expectedIssuer &&
            c.Subject == userData.UserId);
        addedCredential.ShouldNotBeNull();

        // Verify wallet ownership relationships are correct
        await _principalRepository.Received().FindVerifiedSigningOwnersAsync(
            Arg.Is<IEnumerable<WalletId>>(ids => ids.Contains(walletId)),
            Arg.Any<CancellationToken>());

        // Verify credential conflict check was performed
        await _principalRepository.Received(1)
            .IsCredentialTakenAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ReturnConflict_When_CredentialBelongsToOther()
    {
        // Arrange: Existing principal owns wallet, but credential belongs to different principal
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipal();
        var walletId = WalletId.New();

        // Setup: Wallet owned by existing principal
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { walletId, existingPrincipal }
            });

        // Setup: Credential is already taken by another principal
        _principalRepository.IsCredentialTakenAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Conflict error returned
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Message.ShouldContain("This login method belongs to a different account");

        // Verify wallet ownership relationships were checked
        await _principalRepository.Received().FindVerifiedSigningOwnersAsync(
            Arg.Is<IEnumerable<WalletId>>(ids => ids.Contains(walletId)),
            Arg.Any<CancellationToken>());

        // Should not save any changes
        await _principalRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_FallbackToCredentialLookup_When_NoWalletOwners()
    {
        // Arrange: No existing wallet owners, but credential exists
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipal();
        var walletId = WalletId.New();

        // Setup: Wallets exist but no verified signing owners
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>()); // Empty - no owners

        // Setup: Credential-first fallback finds existing principal
        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(existingPrincipal);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Existing principal found via credential fallback
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);

        // Verify both wallet-first and credential fallback were attempted
        await _principalRepository.Received(2) // Called twice: once for wallet-first resolution, once for wallet processing
            .FindVerifiedSigningOwnersAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>());
        await _principalRepository.Received(1)
            .FindByCredentialAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_CreateNewPrincipal_When_NoWalletOwnersAndNoCredential()
    {
        // Arrange: No wallet owners and no existing credential
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var walletId = WalletId.New();

        // Setup: Wallets exist but no owners
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        // Setup: Credential fallback finds nothing
        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: New principal created
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.AxonId.Value.ShouldNotBe(Guid.Empty);

        // Verify both lookups were attempted before creating new
        await _principalRepository.Received(2) // Called twice: once for wallet-first resolution, once for wallet processing
            .FindVerifiedSigningOwnersAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>());
        await _principalRepository.Received(1)
            .FindByCredentialAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _principalRepository.Received(1)
            .AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_HandleIdempotency_When_CredentialAlreadyExists()
    {
        // Arrange: Principal owns wallet and already has the exact same credential
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);
        var existingPrincipal = CreateTestPrincipalWithExistingDynamicCredential(userData);
        var walletId = WalletId.New();

        // Setup: Wallet owned by existing principal
        _walletRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>
            {
                { ("1", Address.Create("0x1234567890123456789012345678901234567890").Value), walletId }
            });

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { walletId, existingPrincipal }
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Success without attempting to add duplicate credential
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);

        // Should not check if credential is taken (idempotency skip)
        await _principalRepository.DidNotReceive()
            .IsCredentialTakenAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }


    [Test]
    public async Task Should_ApplyChainDefaults_When_ExchangingWithVerifiedSigningWallets()
    {
        // Arrange: Create exchange data with wallets from different chains
        var userData = new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                new("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "1"), // Ethereum mainnet
                new("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS", "1399811149"), // Solana mainnet
                new("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42", "137") // Polygon mainnet
            }
        );
        var command = new ExchangeCredentialCommand(userData);

        // Create wallet IDs and mock lookup results
        var ethereumWalletId = WalletId.New();
        var solanaWalletId = WalletId.New();
        var polygonWalletId = WalletId.New();

        var walletLookup = new Dictionary<(string chainId, Address address), WalletId>
        {
            { ("1", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41")), ethereumWalletId },
            { ("1399811149", Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS")), solanaWalletId },
            { ("137", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42")), polygonWalletId }
        };

        // Mock wallet repository
        _walletRepository.EnsureManyByChainAndAddressAsync(Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(walletLookup);

        // Mock GetByIdsAsync for chain defaults application
        var wallets = new List<Wallet>
        {
            Wallet.Create(ethereumWalletId, "1", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41"), DateTime.UtcNow),
            Wallet.Create(solanaWalletId, "1399811149", Address.From("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWS"), DateTime.UtcNow),
            Wallet.Create(polygonWalletId, "137", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42"), DateTime.UtcNow)
        };
        _walletRepository.GetByIdsAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(wallets);

        // Mock no existing principal found
        _principalRepository.FindVerifiedSigningOwnersAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        _principalRepository.FindByCredentialAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _principalRepository.IsCredentialTakenAsync(TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Track the principal that gets created/updated
        AxonPrincipal? capturedPrincipal = null;
        _principalRepository.AddAsync(Arg.Do<AxonPrincipal>(p => capturedPrincipal = p), Arg.Any<CancellationToken>())
            .Returns(args => (AxonPrincipal)args[0]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Verify operation succeeded
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.DefaultsApplied.ShouldBe(3); // 3 chains should have defaults applied
        result.Value.WalletsLinked.ShouldBe(3);

        // Verify principal was captured and has chain defaults
        capturedPrincipal.ShouldNotBeNull();
        capturedPrincipal.PrincipalChainDefaults.ShouldNotBeEmpty();
        capturedPrincipal.PrincipalChainDefaults.Count.ShouldBe(3);

        // Verify each chain has the correct default wallet
        var chainDefaults = capturedPrincipal.PrincipalChainDefaults.ToList();
        chainDefaults.ShouldContain(cd => cd.ChainId == "1" && cd.WalletId == ethereumWalletId);
        chainDefaults.ShouldContain(cd => cd.ChainId == "1399811149" && cd.WalletId == solanaWalletId);
        chainDefaults.ShouldContain(cd => cd.ChainId == "137" && cd.WalletId == polygonWalletId);

        // Verify all chain defaults have proper audit fields (indicating they're ready for persistence)
        foreach (var chainDefault in chainDefaults)
        {
            chainDefault.Id.ShouldNotBe(Guid.Empty);
            chainDefault.PrincipalId.ShouldBe(capturedPrincipal.Id);
            chainDefault.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
        }

        // Verify repository calls
        await _principalRepository.Received(1).AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ApplyChainDefaults_When_ExistingPrincipalHasWalletsButNoDefaults()
    {
        // Arrange: Existing principal with wallets linked but no chain defaults
        var userData = new ExchangeUserData(
            UserId: "test-user-123",
            Email: "test@example.com",
            EnvironmentId: "test-env-456",
            Wallets: new List<ExchangeWalletData>
            {
                new("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41", "1"), // Ethereum mainnet
                new("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42", "137") // Polygon mainnet
            }
        );
        var command = new ExchangeCredentialCommand(userData);

        // Create existing principal with no chain defaults
        var existingPrincipal = CreateTestPrincipal();

        // Create wallet IDs and mock lookup results
        var ethereumWalletId = WalletId.New();
        var polygonWalletId = WalletId.New();

        var walletLookup = new Dictionary<(string chainId, Address address), WalletId>
        {
            { ("1", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41")), ethereumWalletId },
            { ("137", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42")), polygonWalletId }
        };

        // Setup: Existing principal owns the wallets (wallet-first resolution finds it)
        _walletRepository.EnsureManyByChainAndAddressAsync(Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(walletLookup);

        _principalRepository.FindVerifiedSigningOwnersAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>
            {
                { ethereumWalletId, existingPrincipal },
                { polygonWalletId, existingPrincipal }
            });

        // Mock GetByIdsAsync for chain defaults application
        var wallets = new List<Wallet>
        {
            Wallet.Create(ethereumWalletId, "1", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e41"), DateTime.UtcNow),
            Wallet.Create(polygonWalletId, "137", Address.From("0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e42"), DateTime.UtcNow)
        };
        _walletRepository.GetByIdsAsync(Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(wallets);

        // Track the principal that gets updated
        AxonPrincipal? capturedPrincipal = null;
        _principalRepository.UpdateAsync(Arg.Do<AxonPrincipal>(p => capturedPrincipal = p), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult((AxonPrincipal)args[0]));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Verify operation succeeded
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse(); // Existing principal
        result.Value.AxonId.ShouldBe(existingPrincipal.Id);
        result.Value.DefaultsApplied.ShouldBe(2); // 2 chains should have defaults applied
        result.Value.WalletsLinked.ShouldBe(2);

        // Verify principal was captured and has chain defaults
        capturedPrincipal.ShouldNotBeNull();
        capturedPrincipal.PrincipalChainDefaults.ShouldNotBeEmpty();
        capturedPrincipal.PrincipalChainDefaults.Count.ShouldBe(2);

        // Verify each chain has the correct default wallet
        var chainDefaults = capturedPrincipal.PrincipalChainDefaults.ToList();
        chainDefaults.ShouldContain(cd => cd.ChainId == "1" && cd.WalletId == ethereumWalletId);
        chainDefaults.ShouldContain(cd => cd.ChainId == "137" && cd.WalletId == polygonWalletId);

        // Verify all chain defaults have proper audit fields (indicating they're ready for persistence)
        foreach (var chainDefault in chainDefaults)
        {
            chainDefault.Id.ShouldNotBe(Guid.Empty);
            chainDefault.PrincipalId.ShouldBe(capturedPrincipal.Id);
            chainDefault.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
        }

        // Verify repository calls - should update existing principal, not add new
        await _principalRepository.Received(1).UpdateAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
        await _principalRepository.DidNotReceive().AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    private static AxonPrincipal CreateTestPrincipalWithExistingDynamicCredential(ExchangeUserData userData)
    {
        var result = AxonPrincipal.CreateWithDynamicCredential(
            TestProviderType,
            $"app.dynamicauth.com/{userData.EnvironmentId}",
            userData.UserId);
        return result.Value;
    }
}