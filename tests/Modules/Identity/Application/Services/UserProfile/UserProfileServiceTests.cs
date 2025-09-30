using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Services.UserProfile;

/// <summary>
/// Tests for UserProfileService business logic.
/// Sprint 4 - Comprehensive tests for profile retrieval, ETag caching, and wallet aggregation.
///
/// TESTING PHILOSOPHY:
/// - Focus on observable behavior (profile returned, errors thrown)
/// - Minimal repository mocking - just return data
/// - Test ETag logic thoroughly (HIT/MISS/absent scenarios)
/// - Validate wallet aggregation and default chain logic
/// </summary>
[TestFixture]
public class UserProfileServiceTests
{
    private IAxonPrincipalReadRepository _principalRepository = null!;
    private IWalletReadRepository _walletRepository = null!;
    private ILogger<UserProfileService> _logger = null!;
    private UserProfileService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _principalRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _walletRepository = Substitute.For<IWalletReadRepository>();
        _logger = Substitute.For<ILogger<UserProfileService>>();

        _service = new UserProfileService(
            _principalRepository,
            _walletRepository,
            _logger);
    }

    #region Core Profile Retrieval Tests

    [Test]
    public async Task GetCurrentUserProfile_WithValidPrincipal_ShouldReturnCompleteProfile()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var profile = result.Value;
        profile.Profile.AxonId.ShouldBe(principalId.Value.ToString());
        profile.ETag.ShouldBe(etag);
        profile.Wallets.ShouldBeEmpty(); // No wallets in simple principal
    }

    [Test]
    public async Task GetCurrentUserProfile_WithPrincipalNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("not found");
    }

    [Test]
    public async Task GetCurrentUserProfile_WithDataLoadFailure_ShouldReturnNotFound()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null); // Simulate data load failure

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("could not be loaded");
    }

    [Test]
    public async Task GetUserProfileByCredential_WithSuccess_ShouldDelegateToMainMethod()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";
        var providerType = ProviderType.Dynamic;
        var issuer = "https://app.dynamic.xyz";
        var subject = "user-123";

        _principalRepository.FindByCredentialAsync(providerType, issuer, subject, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act
        var result = await _service.GetUserProfileByCredentialAsync(providerType, issuer, subject);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Profile.AxonId.ShouldBe(principalId.Value.ToString());
    }

    [Test]
    public async Task GetUserProfileByCredential_WithPrincipalNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var providerType = ProviderType.Dynamic;
        var issuer = "https://app.dynamic.xyz";
        var subject = "user-123";

        _principalRepository.FindByCredentialAsync(providerType, issuer, subject, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _service.GetUserProfileByCredentialAsync(providerType, issuer, subject);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Test]
    public async Task GetCurrentUserProfile_WithMultipleVerifiedWallets_ShouldAggregateAll()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var wallet1Id = WalletId.New();
        var wallet2Id = WalletId.New();

        var ownership1 = WalletOwnership.Create(
            principalId, wallet1Id, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);
        var ownership2 = WalletOwnership.Create(
            principalId, wallet2Id, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { ownership1, ownership2 });

        var wallet1 = CreateWallet(wallet1Id, "eip155:1", "0x1111111111111111111111111111111111111111");
        var wallet2 = CreateWallet(wallet2Id, "solana", "GqW3r1tFtH9KjLzJ8YxH6L4mN9nD3kP2vB5cT6hQ8eRv");

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _walletRepository.GetByIdsAsync(Arg.Any<List<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(new[] { wallet1, wallet2 });

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.Count.ShouldBe(2);
        result.Value.Wallets.ShouldContain(w => w.Chain == "eip155:1");
        result.Value.Wallets.ShouldContain(w => w.Chain == "solana");
    }

    [Test]
    public async Task GetCurrentUserProfile_WithNoVerifiedWallets_ShouldReturnEmptyWalletsArray()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var walletId = WalletId.New();

        // Create ownership with Pending status (not Verified)
        var ownership = WalletOwnership.Create(
            principalId, walletId, AccessMode.Signing, OwnershipStatus.Pending, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { ownership });

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.ShouldBeEmpty(); // No verified wallets
    }

    #endregion

    #region ETag Caching Tests

    [Test]
    public async Task GetCurrentUserProfile_WithMatchingETag_ShouldReturnNotModified()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);

        // Act - ETag HIT
        var result = await _service.GetCurrentUserProfileAsync(principalId, ifNoneMatch: etag);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict); // NotModified is mapped to Conflict
        result.Error.Code.ShouldBe("NOT_MODIFIED");
        result.Error.Metadata.ShouldNotBeNull();
        result.Error.Metadata.ShouldContainKey("IsNotModified");
        result.Error.Metadata["IsNotModified"].ShouldBe(true);
    }

    [Test]
    public async Task GetCurrentUserProfile_WithMismatchedETag_ShouldReturnFreshProfile()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var currentETag = "etag-12345";
        var clientETag = "old-etag-99999";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(currentETag);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act - ETag MISS
        var result = await _service.GetCurrentUserProfileAsync(principalId, ifNoneMatch: clientETag);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe(currentETag);
        result.Value.Profile.AxonId.ShouldBe(principalId.Value.ToString());
    }

    [Test]
    public async Task GetCurrentUserProfile_WithNoIfNoneMatch_ShouldReturnFreshProfile()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act - No If-None-Match header
        var result = await _service.GetCurrentUserProfileAsync(principalId, ifNoneMatch: null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe(etag);
    }

    [Test]
    public async Task GetCurrentUserProfile_WithQuotedETag_ShouldHandleCorrectly()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var principal = CreatePrincipal(principalId);
        var etag = "etag-12345";

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(etag);

        // Act - ETag with quotes (HTTP spec allows both)
        var result = await _service.GetCurrentUserProfileAsync(principalId, ifNoneMatch: $"\"{etag}\"");

        // Assert - Should match after stripping quotes
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("NOT_MODIFIED");
    }

    #endregion

    #region Wallet Aggregation Tests

    [Test]
    public async Task GetCurrentUserProfile_ShouldIncludeOnlyVerifiedWallets()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var verifiedWalletId = WalletId.New();
        var pendingWalletId = WalletId.New();

        var verifiedOwnership = WalletOwnership.Create(
            principalId, verifiedWalletId, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);
        var pendingOwnership = WalletOwnership.Create(
            principalId, pendingWalletId, AccessMode.Signing, OwnershipStatus.Pending, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { verifiedOwnership, pendingOwnership });

        var verifiedWallet = CreateWallet(verifiedWalletId, "eip155:1", "0x1111111111111111111111111111111111111111");
        // pendingWallet created for completeness but not used since repository filters to verified
        _ = CreateWallet(pendingWalletId, "solana", "GqW3r1tFtH9KjLzJ8YxH6L4mN9nD3kP2vB5cT6hQ8eRv");

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _walletRepository.GetByIdsAsync(Arg.Any<List<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(new[] { verifiedWallet }); // Repository filters to verified

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.Count.ShouldBe(1);
        result.Value.Wallets[0].Chain.ShouldBe("eip155:1");
        result.Value.Wallets[0].State.ShouldBe("verified");
    }

    [Test]
    public async Task GetCurrentUserProfile_WithDefaultWallet_ShouldMarkIsDefaultTrue()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var defaultWalletId = WalletId.New();
        var chainId = "eip155:1";

        var ownership = WalletOwnership.Create(
            principalId, defaultWalletId, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { ownership });
        // Set chain default using domain method
        principal.SetChainDefault(chainId, defaultWalletId, TimeProvider.System);

        var wallet = CreateWallet(defaultWalletId, chainId, "0x1111111111111111111111111111111111111111");

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _walletRepository.GetByIdsAsync(Arg.Any<List<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(new[] { wallet });

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.Count.ShouldBe(1);
        result.Value.Wallets[0].IsDefault.ShouldBeTrue();
    }

    [Test]
    public async Task GetCurrentUserProfile_ShouldMapOwnershipStatusAndAccessMode()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var walletId = WalletId.New();

        var ownership = WalletOwnership.Create(
            principalId, walletId, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { ownership });
        var wallet = CreateWallet(walletId, "eip155:1", "0x1111111111111111111111111111111111111111");

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _walletRepository.GetByIdsAsync(Arg.Any<List<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(new[] { wallet });

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.Count.ShouldBe(1);
        result.Value.Wallets[0].State.ShouldBe("verified");
        result.Value.Wallets[0].Access.ShouldBe("signing");
    }

    [Test]
    public async Task GetCurrentUserProfile_WithMissingWalletInDictionary_ShouldSkipWallet()
    {
        // Arrange
        var principalId = new AxonUserId(Guid.NewGuid());
        var wallet1Id = WalletId.New();
        var wallet2Id = WalletId.New();

        var ownership1 = WalletOwnership.Create(
            principalId, wallet1Id, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);
        var ownership2 = WalletOwnership.Create(
            principalId, wallet2Id, AccessMode.Signing, OwnershipStatus.Verified, VerificationSource.DirectSignatureMsg);

        var principal = CreatePrincipalWithOwnerships(principalId, new[] { ownership1, ownership2 });

        // Only return wallet1, wallet2 lookup will fail
        var wallet1 = CreateWallet(wallet1Id, "eip155:1", "0x1111111111111111111111111111111111111111");

        _principalRepository.GetByIdAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _principalRepository.GetPrincipalFingerprintAsync(principalId, Arg.Any<CancellationToken>())
            .Returns("etag-12345");
        _principalRepository.GetByIdWithActiveOwnershipsAsync(principalId, Arg.Any<CancellationToken>())
            .Returns(principal);
        _walletRepository.GetByIdsAsync(Arg.Any<List<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(new[] { wallet1 }); // wallet2 missing

        // Act
        var result = await _service.GetCurrentUserProfileAsync(principalId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Wallets.Count.ShouldBe(1); // Only wallet1 included
        result.Value.Wallets[0].Chain.ShouldBe("eip155:1");
    }

    #endregion

    #region Test Data Builders

    private static AxonPrincipal CreatePrincipal(AxonUserId principalId)
    {
        return AxonPrincipal.CreateHuman(principalId);
    }

    private static AxonPrincipal CreatePrincipalWithOwnerships(
        AxonUserId principalId,
        IEnumerable<WalletOwnership> ownerships)
    {
        var principal = AxonPrincipal.CreateHuman(principalId);

        // Use LinkWalletOwnership domain method with a stub uniqueness check
        Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> stubUniquenessCheck =
            (walletId, accessMode, status) => Result.Success<bool, Error>(false); // No conflicts

        foreach (var ownership in ownerships)
        {
            principal.LinkWalletOwnership(ownership, stubUniquenessCheck, TimeProvider.System);
        }

        return principal;
    }

    private static Wallet CreateWallet(WalletId walletId, string chainId, string address)
    {
        var addressVO = Address.Create(address).Value;
        return Wallet.Create(walletId, chainId, addressVO);
    }

    #endregion
}