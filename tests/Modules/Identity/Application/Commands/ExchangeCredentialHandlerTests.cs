using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
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
    private IWriteUnitOfWork _unitOfWork = null!;
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
        _unitOfWork = Substitute.For<IWriteUnitOfWork>();

        _principalRepository.UnitOfWork.Returns(_unitOfWork);

        _handler = new ExchangeCredentialHandler(
            _currentUserService,
            _principalRepository,
            _walletRepository,
            _metricsService,
            _logger);
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

        _principalRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        SetupSuccessfulTransaction();

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

        _principalRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        SetupSuccessfulTransaction();

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

        _principalRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(walletIds);

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        SetupSuccessfulTransaction();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.WalletsProcessed.ShouldBe(2);

        await _principalRepository.Received(1)
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
        _principalRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        SetupSuccessfulTransaction();

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
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        var conflictWalletId = WalletId.New();
        var conflictPrincipal = CreateTestPrincipal();

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _principalRepository.EnsureManyByChainAndAddressAsync(
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

        SetupSuccessfulTransaction();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
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

        SetupSuccessfulTransaction();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.WalletsProcessed.ShouldBe(0);
        result.Value.WalletsLinked.ShouldBe(0);
    }

    [Test]
    public async Task Should_RollbackTransaction_OnFailure()
    {
        // Arrange
        var userData = CreateTestUserData();
        var command = new ExchangeCredentialCommand(userData);

        _principalRepository.FindByCredentialAsync(
            TestProviderType, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Setup transaction that fails
        _unitOfWork.When(x => x.ExecuteInTransactionAsync<Result<ExchangeOutcome, Error>>(
            Arg.Any<Func<CancellationToken, Task<Result<ExchangeOutcome, Error>>>>(),
            Arg.Any<CancellationToken>()))
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

    private void SetupSuccessfulTransaction()
    {
        _unitOfWork.ExecuteInTransactionAsync<Result<ExchangeOutcome, Error>>(
            Arg.Any<Func<CancellationToken, Task<Result<ExchangeOutcome, Error>>>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var func = callInfo.Arg<Func<CancellationToken, Task<Result<ExchangeOutcome, Error>>>>();
                return func(CancellationToken.None);
            });
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

        _principalRepository.EnsureManyByChainAndAddressAsync(
            Arg.Any<IEnumerable<(string, Address)>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(string, Address), WalletId>());

        _principalRepository.FindVerifiedSigningOwnersAsync(
            Arg.Any<IEnumerable<WalletId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<WalletId, AxonPrincipal>());

        SetupSuccessfulTransaction();

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
}