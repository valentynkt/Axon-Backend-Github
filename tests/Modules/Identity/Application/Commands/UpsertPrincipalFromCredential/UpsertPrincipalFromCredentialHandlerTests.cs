using Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Requests;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands.UpsertPrincipalFromCredential;

[TestFixture]
public class UpsertPrincipalFromCredentialHandlerTests
{
    private ICurrentUserService _currentUserService = null!;
    private IAxonPrincipalWriteRepository _principalRepository = null!;
    private IWalletWriteRepository _walletRepository = null!;
    private IWalletOwnershipService _walletOwnershipService = null!;
    private TimeProvider _timeProvider = null!;
    private ILogger<UpsertPrincipalFromCredentialHandler> _logger = null!;
    private UpsertPrincipalFromCredentialHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _principalRepository = Substitute.For<IAxonPrincipalWriteRepository>();
        _walletRepository = Substitute.For<IWalletWriteRepository>();
        _walletOwnershipService = Substitute.For<IWalletOwnershipService>();
        _timeProvider = TimeProvider.System;
        _logger = Substitute.For<ILogger<UpsertPrincipalFromCredentialHandler>>();
        
        _handler = new UpsertPrincipalFromCredentialHandler(
            _currentUserService,
            _principalRepository,
            _walletRepository,
            _walletOwnershipService,
            _timeProvider,
            _logger);
    }

    [TestFixture]
    public class Handle : UpsertPrincipalFromCredentialHandlerTests
    {
        [Test]
        public async Task Should_RejectServiceApiProvider()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "service_api",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Validation);
            result.Error.Message.ShouldContain("service_api provider not allowed");
        }

        [Test]
        public async Task Should_ReturnFailure_When_ProviderTypeIsInvalid()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "invalid_provider",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
        }

        [Test]
        public async Task Should_CallWalletOwnershipService_ForCredentialUniquenessCheck()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: null);

            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<AxonPrincipal?, Error>((AxonPrincipal?)null));

            var unitOfWork = Substitute.For<BuildingBlocks.Application.IUnitOfWork>();
            _principalRepository.UnitOfWork.Returns(unitOfWork);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _walletOwnershipService.Received(1)
                .CheckCredentialUniquenessAsync(
                    Arg.Is<ProviderType>(pt => pt.Value == "dynamic_verified"),
                    "test-issuer",
                    "test-subject",
                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Should_ReturnFailure_When_CredentialUniquenessCheckFails()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: null);

            var error = Error.Failure("Service error");
            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<AxonPrincipal?, Error>(error));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(error);
        }

        [Test]
        public async Task Should_UpdateExistingPrincipal_When_CredentialExists()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: null);

            var existingPrincipal = CreateTestPrincipal();
            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<AxonPrincipal?, Error>(existingPrincipal));

            var unitOfWork = Substitute.For<BuildingBlocks.Application.IUnitOfWork>();
            _principalRepository.UnitOfWork.Returns(unitOfWork);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Status.ShouldBe(UpsertPrincipalStatus.UpdatedLastSeen);
            result.Value.Principal.ShouldNotBeNull();
            result.Value.Credential.ShouldNotBeNull();
        }

        [Test]
        public async Task Should_CreateNewPrincipal_When_CredentialDoesNotExist()
        {
            // Arrange
            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: "test-email-hash",
                AttachWallet: null);

            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<AxonPrincipal?, Error>((AxonPrincipal?)null));

            var unitOfWork = Substitute.For<BuildingBlocks.Application.IUnitOfWork>();
            _principalRepository.UnitOfWork.Returns(unitOfWork);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Status.ShouldBe(UpsertPrincipalStatus.Created);
            result.Value.Principal.ShouldNotBeNull();
            result.Value.Credential.ShouldNotBeNull();

            await _principalRepository.Received(1)
                .AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Should_AttachWallet_When_AttachWalletProvided()
        {
            // Arrange
            var attachRequest = new AttachWalletRequest(
                ChainId: ChainId.Create(1),
                RawAddress: "0x1234567890123456789012345678901234567890",
                ProofType: "dynamic_verified",
                AccessMode: "signing",
                Verify: true,
                SetAsDefault: true,
                Label: "My Wallet"
            );

            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: attachRequest);

            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<AxonPrincipal?, Error>((AxonPrincipal?)null));

            _walletOwnershipService
                .CheckWalletOwnershipConflictAsync(Arg.Any<WalletId>(), Arg.Any<AxonId>(), Arg.Any<ProofType>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<bool, Error>(true));

            _walletRepository
                .GetByChainAndAddressAsync(Arg.Any<ChainId>(), Arg.Any<Address>(), Arg.Any<CancellationToken>())
                .Returns((Wallet?)null);

            var unitOfWork = Substitute.For<BuildingBlocks.Application.IUnitOfWork>();
            _principalRepository.UnitOfWork.Returns(unitOfWork);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Status.ShouldBe(UpsertPrincipalStatus.LinkedWithWallet);
            result.Value.AttachedWallet.ShouldNotBeNull();

            await _walletOwnershipService.Received(1)
                .CheckWalletOwnershipConflictAsync(
                    Arg.Any<WalletId>(),
                    Arg.Any<AxonId>(),
                    Arg.Any<ProofType>(),
                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Should_ReturnConflictError_When_WalletOwnershipConflictExists()
        {
            // Arrange
            var attachRequest = new AttachWalletRequest(
                ChainId: ChainId.Create(1),
                RawAddress: "0x1234567890123456789012345678901234567890",
                ProofType: "dynamic_verified",
                AccessMode: "signing",
                Verify: true,
                SetAsDefault: true,
                Label: "My Wallet"
            );

            var command = new UpsertPrincipalFromCredentialCommand(
                ProviderType: "dynamic_verified",
                Issuer: "test-issuer",
                Subject: "test-subject",
                EnvironmentId: "env-1",
                CredentialMetadata: new CredentialMetadata(new Dictionary<string, object>()),
                PrimaryEmailHash: null,
                AttachWallet: attachRequest);

            _walletOwnershipService
                .CheckCredentialUniquenessAsync(Arg.Any<ProviderType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success<AxonPrincipal?, Error>((AxonPrincipal?)null));

            var conflictError = Error.Conflict("Wallet conflict");
            _walletOwnershipService
                .CheckWalletOwnershipConflictAsync(Arg.Any<WalletId>(), Arg.Any<AxonId>(), Arg.Any<ProofType>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<bool, Error>(conflictError));

            _walletRepository
                .GetByChainAndAddressAsync(Arg.Any<ChainId>(), Arg.Any<Address>(), Arg.Any<CancellationToken>())
                .Returns((Wallet?)null);

            var unitOfWork = Substitute.For<BuildingBlocks.Application.IUnitOfWork>();
            _principalRepository.UnitOfWork.Returns(unitOfWork);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
        }
    }

    private static AxonPrincipal CreateTestPrincipal()
    {
        var principalResult = AxonPrincipal.CreateHumanPrincipal();
        var principal = principalResult.Value;
        
        // Add a test credential
        var credentialResult = principal.LinkIdentityCredential(
            ProviderType.DynamicVerified, 
            "test-issuer", 
            "test-subject", 
            "env-1",
            new CredentialMetadata(new Dictionary<string, object>()),
            TimeProvider.System);

        return principal;
    }
}