using System.Diagnostics;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Axon.Modules.Identity.Infrastructure.Services.Tests;

[TestFixture]
public class PrincipalResolutionServiceTests
{
    private PrincipalResolutionService _service = null!;
    private IAxonPrincipalReadRepository _principalReadRepository = null!;
    private IAxonPrincipalWriteRepository _principalWriteRepository = null!;
    private IWalletReadRepository _walletReadRepository = null!;
    private IWalletWriteRepository _walletWriteRepository = null!;
    private IWalletOwnershipRepository _ownershipRepository = null!;
    private ILogger<PrincipalResolutionService> _logger = null!;

    private ProviderType _dynamicProvider;
    private ChainId _solanaChain;
    private Address _testAddress;

    [SetUp]
    public void SetUp()
    {
        _principalReadRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _principalWriteRepository = Substitute.For<IAxonPrincipalWriteRepository>();
        _walletReadRepository = Substitute.For<IWalletReadRepository>();
        _walletWriteRepository = Substitute.For<IWalletWriteRepository>();
        _ownershipRepository = Substitute.For<IWalletOwnershipRepository>();
        _logger = Substitute.For<ILogger<PrincipalResolutionService>>();

        _service = new PrincipalResolutionService(
            _principalReadRepository,
            _principalWriteRepository,
            _walletReadRepository,
            _walletWriteRepository,
            _ownershipRepository,
            _logger);

        // Test data setup
        _dynamicProvider = ProviderType.Create("dynamic").Value;
        _solanaChain = ChainId.Create("solana:mainnet").Value;
        _testAddress = Address.Create("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM").Value;
    }

    [TearDown]
    public void TearDown()
    {
        _principalWriteRepository.Dispose();
        _walletWriteRepository.Dispose();
    }

    [Test]
    public async Task ResolveAsync_CredentialMatch_ReturnsExistingPrincipal()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var existingPrincipal = CreateTestPrincipal();
        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns(existingPrincipal);

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(existingPrincipal);
        result.Value.Path.Should().Be(ResolutionPath.Credential);
        result.Value.WasAutoLinked.Should().BeFalse();

        // Should not check for wallets or cross-env conflicts when credential found
        await _walletReadRepository.DidNotReceive().FindWalletAsync(Arg.Any<ChainId>(), Arg.Any<Address>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveAsync_CredentialMatchWithCrossEnvConflict_ReturnsConflictError()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var principalA = CreateTestPrincipal("principal-a");
        var principalB = CreateTestPrincipal("principal-b");
        var crossEnvOwnership = CreateTestWalletOwnership(principalB.Id, WalletId.New());

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns(principalA);

        _ownershipRepository.FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(_solanaChain.Value, _testAddress, Arg.Any<CancellationToken>())
            .Returns(crossEnvOwnership);

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("Cross-environment verified+signing conflict");
    }

    [Test]
    public async Task ResolveAsync_CredentialMatchWithSamePrincipalCrossEnv_AutoLinksWallet()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var principalA = CreateTestPrincipal("principal-a");
        var crossEnvOwnership = CreateTestWalletOwnership(principalA.Id, WalletId.New());

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns(principalA);

        _ownershipRepository.FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(_solanaChain.Value, _testAddress, Arg.Any<CancellationToken>())
            .Returns(crossEnvOwnership);

        _walletWriteRepository.UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(CreateTestWallet());

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(principalA);
        result.Value.Path.Should().Be(ResolutionPath.Credential);
        result.Value.WasAutoLinked.Should().BeTrue();

        await _walletWriteRepository.Received(1).UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveAsync_WalletMatch_ReturnsOwningPrincipal()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var existingWallet = CreateTestWallet();
        var ownership = CreateTestWalletOwnership(AxonUserId.New(), existingWallet.Id);
        var existingPrincipal = CreateTestPrincipal();

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(existingWallet);

        _ownershipRepository.FindActiveOwnershipsByWalletAsync(existingWallet.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WalletOwnership> { ownership });

        // Navigation property would be set by EF Core in real scenario

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(existingPrincipal);
        result.Value.Path.Should().Be(ResolutionPath.Wallet);
        result.Value.WasAutoLinked.Should().BeFalse();
    }

    [Test]
    public async Task ResolveAsync_WalletMatchWithTieBreaking_ReturnsHighestAuthorityPrincipal()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var existingWallet = CreateTestWallet();
        var principalA = CreateTestPrincipal("principal-a");
        var principalB = CreateTestPrincipal("principal-b");

        // Create ownerships with different verification sources
        var ownershipA = CreateTestWalletOwnership(principalA.Id, existingWallet.Id,
            verificationSource: VerificationSource.DirectSignatureMsg);
        var ownershipB = CreateTestWalletOwnership(principalB.Id, existingWallet.Id,
            verificationSource: VerificationSource.DynamicAttested);

        // Navigation properties would be set by EF Core in real scenario

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(existingWallet);

        _ownershipRepository.FindActiveOwnershipsByWalletAsync(existingWallet.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WalletOwnership> { ownershipA, ownershipB });

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(principalB); // DynamicAttested has higher authority
        result.Value.Path.Should().Be(ResolutionPath.Wallet);
    }

    [Test]
    public async Task ResolveAsync_NoWalletButCrossEnvOwner_AttachesToExistingPrincipal()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var crossEnvPrincipal = CreateTestPrincipal("cross-env-principal");
        var crossEnvOwnership = CreateTestWalletOwnership(crossEnvPrincipal.Id, WalletId.New());
        var newWallet = CreateTestWallet();

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        _ownershipRepository.FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(_solanaChain.Value, _testAddress, Arg.Any<CancellationToken>())
            .Returns(crossEnvOwnership);

        // Navigation property would be set by EF Core in real scenario

        _walletWriteRepository.UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(newWallet);

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(crossEnvPrincipal);
        result.Value.Path.Should().Be(ResolutionPath.Wallet);
        result.Value.WasAutoLinked.Should().BeTrue();

        await _walletWriteRepository.Received(1).UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveAsync_CreateNewPrincipal_ReturnsNewPrincipalWithCreatedPath()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        _ownershipRepository.FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(_solanaChain.Value, _testAddress, Arg.Any<CancellationToken>())
            .Returns((WalletOwnership?)null);

        var newWallet = CreateTestWallet();
        _walletWriteRepository.UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(newWallet);

        _ownershipRepository.FindActiveOwnershipsByWalletAsync(newWallet.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WalletOwnership>());

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().NotBeNull();
        result.Value.Path.Should().Be(ResolutionPath.Created);
        result.Value.WasAutoLinked.Should().BeFalse();

        // Should have created the wallet first for race protection
        await _walletWriteRepository.Received(1).UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveAsync_RaceConditionDetected_ResolvesToWinningPrincipal()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var racingPrincipal = CreateTestPrincipal("racing-principal");
        var raceOwnership = CreateTestWalletOwnership(racingPrincipal.Id, WalletId.New());
        var newWallet = CreateTestWallet();

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        _ownershipRepository.FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(_solanaChain.Value, _testAddress, Arg.Any<CancellationToken>())
            .Returns((WalletOwnership?)null);

        _walletWriteRepository.UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(newWallet);

        // Simulate race condition: another request already linked ownership
        _ownershipRepository.FindActiveOwnershipsByWalletAsync(newWallet.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WalletOwnership> { raceOwnership });

        // Navigation property would be set by EF Core in real scenario

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(racingPrincipal);
        result.Value.Path.Should().Be(ResolutionPath.Wallet);
        result.Value.WasAutoLinked.Should().BeFalse();

        // Should still have attempted wallet upsert
        await _walletWriteRepository.Received(1).UpsertWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveAsync_VerifiedSigningOwnershipExists_BypassesTieBreaking()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        var existingWallet = CreateTestWallet();
        var verifiedSigningPrincipal = CreateTestPrincipal("verified-signing");
        var watchOnlyPrincipal = CreateTestPrincipal("watch-only");

        var verifiedOwnership = CreateTestWalletOwnership(verifiedSigningPrincipal.Id, existingWallet.Id,
            status: OwnershipStatus.Verified, accessMode: AccessMode.Signing);
        var watchOnlyOwnership = CreateTestWalletOwnership(watchOnlyPrincipal.Id, existingWallet.Id,
            status: OwnershipStatus.Verified, accessMode: AccessMode.WatchOnly);

        // Navigation properties would be set by EF Core in real scenario

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        _walletReadRepository.FindWalletAsync(_solanaChain, _testAddress, Arg.Any<CancellationToken>())
            .Returns(existingWallet);

        _ownershipRepository.FindActiveOwnershipsByWalletAsync(existingWallet.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WalletOwnership> { watchOnlyOwnership, verifiedOwnership });

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Principal.Should().Be(verifiedSigningPrincipal);
        result.Value.Path.Should().Be(ResolutionPath.Wallet);
    }

    [Test]
    public async Task ResolveAsync_PerformanceValidation_CompletesWithinTimeLimit()
    {
        // Arrange
        var issuer = "https://app.dynamic.xyz/mainnet";
        var subject = "user123";

        _principalReadRepository.FindByCredentialAsync(_dynamicProvider, issuer, subject, Arg.Any<CancellationToken>())
            .Returns(Task.Delay(50).ContinueWith<AxonPrincipal?>(_ => CreateTestPrincipal()));

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _service.ResolveAsync(
            _dynamicProvider, issuer, subject,
            _solanaChain, _testAddress, CancellationToken.None);

        stopwatch.Stop();

        // Assert
        result.IsSuccess.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // P95 requirement from story
    }

    private static AxonPrincipal CreateTestPrincipal(string suffix = "")
    {
        var providerType = ProviderType.Create("dynamic").Value;
        var result = AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            $"https://app.dynamic.xyz/mainnet{suffix}",
            $"user123{suffix}");
        return result.Value;
    }

    private static Wallet CreateTestWallet()
    {
        var chainId = ChainId.Create("solana-mainnet").Value;
        var address = Address.Create("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM").Value;
        return Wallet.Create(null, chainId.Value, address);
    }

    private static WalletOwnership CreateTestWalletOwnership(
        AxonUserId principalId,
        WalletId walletId,
        OwnershipStatus status = OwnershipStatus.Verified,
        AccessMode accessMode = AccessMode.Signing,
        VerificationSource verificationSource = VerificationSource.DynamicAttested)
    {
        return WalletOwnership.Create(principalId, walletId, accessMode, status, verificationSource);
    }
}