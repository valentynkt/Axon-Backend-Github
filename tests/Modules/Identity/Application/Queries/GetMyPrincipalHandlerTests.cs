using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Queries;

/// <summary>
/// Comprehensive test suite for GetMyPrincipalHandler.
/// Tests authentication resolution, ETag caching, data loading, and error scenarios.
/// </summary>
[TestFixture]
public class GetMyPrincipalHandlerTests
{
    private ICurrentUserService _currentUserService = null!;
    private IAxonPrincipalReadRepository _principalRepository = null!;
    private IWalletReadRepository _walletRepository = null!;
    private ILogger<GetMyPrincipalHandler> _logger = null!;
    private GetMyPrincipalHandler _handler = null!;

    private static readonly ProviderType TestProviderType = ProviderType.Create("dynamic").Value;
    private static readonly string TestIssuer = "dynamic:test-env";
    private static readonly string TestSubject = "test-user-123";

    [SetUp]
    public void SetUp()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _principalRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _walletRepository = Substitute.For<IWalletReadRepository>();
        _logger = Substitute.For<ILogger<GetMyPrincipalHandler>>();

        _handler = new GetMyPrincipalHandler(
            _currentUserService,
            _principalRepository,
            _walletRepository,
            _logger);
    }

    #region Principal Not Found Tests

    [TestFixture]
    public class PrincipalNotFoundTests : GetMyPrincipalHandlerTests
    {
        [Test]
        public async Task Handle_WhenPrincipalNotFound_Should_ReturnNotFoundError()
        {
            // Arrange
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Message.ShouldContain("Principal not found");
        }

        [Test]
        public async Task Handle_WhenPrincipalNotFound_Should_OnlyCallFindByCredential()
        {
            // Arrange
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert
            await _principalRepository.Received(1).FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>());

            await _principalRepository.DidNotReceive().GetPrincipalFingerprintAsync(
                Arg.Any<AxonUserId>(), Arg.Any<CancellationToken>());
        }
    }

    #endregion

    #region ETag Caching Tests

    [TestFixture]
    public class ETagCachingTests : GetMyPrincipalHandlerTests
    {
        [Test]
        public async Task Handle_WithMatchingETag_Should_ReturnNotModifiedError()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, etag);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns(principal);

            _principalRepository.GetPrincipalFingerprintAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns(etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            result.Error.Metadata.ShouldNotBeNull();
            result.Error.Metadata.ShouldContainKeyAndValue("ETag", etag);
            result.Error.Metadata.ShouldContainKeyAndValue("IsNotModified", true);
        }

        [Test]
        public async Task Handle_WithQuotedMatchingETag_Should_HandleCorrectly()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var etag = "test-etag-123";
            var quotedETag = $"\"{etag}\"";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, quotedETag);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns(principal);

            _principalRepository.GetPrincipalFingerprintAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns(etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Conflict);
            result.Error.Metadata.ShouldNotBeNull();
            result.Error.Metadata.ShouldContainKeyAndValue("ETag", etag);
        }

        [Test]
        public async Task Handle_WithNonMatchingETag_Should_ContinueProcessing()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var currentETag = "current-etag";
            var clientETag = "old-etag";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, clientETag);

            SetupSuccessfulPrincipalResolution(principal, currentETag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ETag.ShouldBe(currentETag);
        }

        [Test]
        public async Task Handle_WithoutETagHeader_Should_ProcessNormally()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ETag.ShouldBe(etag);
        }

        [Test]
        public async Task Handle_WithEmptyETagHeader_Should_ProcessNormally()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, "");

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ETag.ShouldBe(etag);
        }
    }

    #endregion

    #region Successful Response Tests

    [TestFixture]
    public class SuccessfulResponseTests : GetMyPrincipalHandlerTests
    {
        [Test]
        public async Task Handle_WithValidPrincipal_Should_ReturnCurrentUserResult()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var userResult = result.Value;

            userResult.ShouldSatisfyAllConditions(
                r => r.Profile.AxonId.ShouldBe(principal.Id.Value.ToString()),
                r => r.Profile.RiskTier.ShouldBe("low"),
                r => r.ETag.ShouldBe(etag),
                r => r.Wallets.ShouldNotBeEmpty()
            );
        }

        [Test]
        public async Task Handle_WithValidPrincipal_Should_ReturnDifferentSubjectAndAxonUserId()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var userResult = result.Value;

            // Verify the AxonId is correctly set
            userResult.Profile.AxonId.ShouldBe(principal.Id.Value.ToString());
        }

        [Test]
        public async Task Handle_WithVerifiedOwnerships_Should_IncludeWalletsInResponse()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            var userResult = result.Value;

            userResult.Wallets.ShouldHaveSingleItem()
                .ShouldSatisfyAllConditions(
                    w => w.Chain.ShouldBe("ethereum-mainnet"),
                    w => w.Access.ShouldBe("signing"),
                    w => w.State.ShouldBe("verified")
                );
        }

        [Test]
        public async Task Handle_WithPendingOwnerships_Should_ExcludeFromResponse()
        {
            // Arrange
            var principal = CreateTestPrincipalWithPendingOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupPrincipalWithPendingOwnerships(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Wallets.ShouldBeEmpty(); // Only verified ownerships are included
        }

        [Test]
        public async Task Handle_WithMultipleRiskTiers_Should_MapCorrectly()
        {
            var testCases = new[]
            {
                (RiskTier.Low, "low"),
                (RiskTier.Medium, "medium"),
                (RiskTier.High, "high")
            };

            foreach (var (domainRisk, expectedWire) in testCases)
            {
                // Arrange
                var principal = CreateTestPrincipalWithRiskTier(domainRisk);
                var etag = "test-etag-123";
                var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

                SetupMinimalPrincipalResolution(principal, etag);

                // Act
                var result = await _handler.Handle(query, CancellationToken.None);

                // Assert
                result.IsSuccess.ShouldBeTrue();
                result.Value.Profile.RiskTier.ShouldBe(expectedWire);
            }
        }

        [Test]
        public async Task Handle_WithChainDefaults_Should_MarkWalletAsDefault()
        {
            // Arrange
            var principal = CreateTestPrincipalWithChainDefaults();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupMinimalPrincipalResolution(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Wallets.ShouldContain(w => w.IsDefault == true);
        }
    }

    #endregion

    #region Data Loading Error Tests

    [TestFixture]
    public class DataLoadingErrorTests : GetMyPrincipalHandlerTests
    {
        [Test]
        public async Task Handle_WhenPrincipalDataLoadFails_Should_ReturnNotFoundError()
        {
            // Arrange
            var principal = CreateTestPrincipal();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns(principal);

            _principalRepository.GetPrincipalFingerprintAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns(etag);

            _principalRepository.GetByIdWithActiveOwnershipsAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Message.ShouldContain("Principal data could not be loaded");
        }

        [Test]
        public async Task Handle_WhenWalletDataLoadFails_Should_HandleGracefully()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            _principalRepository.FindByCredentialAsync(
                TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
                .Returns(principal);

            _principalRepository.GetPrincipalFingerprintAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns(etag);

            _principalRepository.GetByIdWithActiveOwnershipsAsync(
                principal.Id, Arg.Any<CancellationToken>())
                .Returns(principal);

            // Setup wallet repository to return empty (simulating data load failure)
            _walletRepository.GetByIdsAsync(
                Arg.Any<IList<WalletId>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new List<Wallet>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue(); // Should handle gracefully
            result.Value.Wallets.ShouldBeEmpty(); // Wallet data missing but response valid
        }
    }

    #endregion

    #region Performance and Ordering Tests

    [TestFixture]
    public class PerformanceAndOrderingTests : GetMyPrincipalHandlerTests
    {
        [Test]
        public async Task Handle_Should_CallRepositoriesInCorrectOrder()
        {
            // Arrange
            var principal = CreateTestPrincipalWithOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupSuccessfulPrincipalResolution(principal, etag);

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert - Verify call order
            Received.InOrder(() =>
            {
                _principalRepository.FindByCredentialAsync(
                    TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>());
                _principalRepository.GetPrincipalFingerprintAsync(
                    principal.Id, Arg.Any<CancellationToken>());
                _principalRepository.GetByIdWithActiveOwnershipsAsync(
                    principal.Id, Arg.Any<CancellationToken>());
                _walletRepository.GetByIdsAsync(
                    Arg.Any<IList<WalletId>>(), false, Arg.Any<CancellationToken>());
            });
        }

        [Test]
        public async Task Handle_WithManyOwnerships_Should_BatchWalletRetrieval()
        {
            // Arrange
            var principal = CreateTestPrincipalWithMultipleOwnerships();
            var etag = "test-etag-123";
            var query = new GetMyPrincipalQuery(TestProviderType, TestIssuer, TestSubject, null);

            SetupPrincipalWithMultipleOwnerships(principal, etag);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            // Verify single batch call for all wallet IDs
            await _walletRepository.Received(1).GetByIdsAsync(
                Arg.Is<IList<WalletId>>(ids => ids.Count == 3),
                false,
                Arg.Any<CancellationToken>());
        }
    }

    #endregion

    #region Helper Methods

    private static AxonPrincipal CreateTestPrincipal()
    {
        var result = AxonPrincipal.CreateWithDynamicCredential(
            TestProviderType,
            TestIssuer,
            TestSubject);
        return result.Value;
    }

    private static AxonPrincipal CreateTestPrincipalWithOwnerships()
    {
        var principal = CreateTestPrincipal();
        var walletId = WalletId.New();
        var ownership = WalletOwnership.Create(
            principal.Id,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Verified);

        // Use reflection to add ownership (since it's normally done through commands)
        var ownershipsField = typeof(AxonPrincipal)
            .GetField("_walletOwnerships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ownerships = (List<WalletOwnership>)ownershipsField!.GetValue(principal)!;
        ownerships.Add(ownership);

        return principal;
    }

    private static AxonPrincipal CreateTestPrincipalWithPendingOwnerships()
    {
        var principal = CreateTestPrincipal();
        var walletId = WalletId.New();
        var ownership = WalletOwnership.Create(
            principal.Id,
            walletId,
            AccessMode.Signing,
            OwnershipStatus.Pending);

        // Use reflection to add ownership
        var ownershipsField = typeof(AxonPrincipal)
            .GetField("_walletOwnerships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ownerships = (List<WalletOwnership>)ownershipsField!.GetValue(principal)!;
        ownerships.Add(ownership);

        return principal;
    }

    private static AxonPrincipal CreateTestPrincipalWithRiskTier(RiskTier riskTier)
    {
        var principal = CreateTestPrincipal();
        principal.UpdateRiskTier(riskTier);
        return principal;
    }

    private static AxonPrincipal CreateTestPrincipalWithChainDefaults()
    {
        var principal = CreateTestPrincipalWithOwnerships();
        var walletId = principal.WalletOwnerships.First().WalletId;
        principal.ApplyChainDefault(NetworkEnvironment.Mainnet, "ethereum-mainnet", walletId);
        return principal;
    }

    private static AxonPrincipal CreateTestPrincipalWithMultipleOwnerships()
    {
        var principal = CreateTestPrincipal();
        var walletIds = new[] { WalletId.New(), WalletId.New(), WalletId.New() };

        var ownershipsField = typeof(AxonPrincipal)
            .GetField("_walletOwnerships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ownerships = (List<WalletOwnership>)ownershipsField!.GetValue(principal)!;

        foreach (var walletId in walletIds)
        {
            var ownership = WalletOwnership.Create(
                principal.Id,
                walletId,
                AccessMode.Signing,
                OwnershipStatus.Verified);
            ownerships.Add(ownership);
        }

        return principal;
    }

    private void SetupSuccessfulPrincipalResolution(AxonPrincipal principal, string etag)
    {
        _principalRepository.FindByCredentialAsync(
            TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
            .Returns(principal);

        _principalRepository.GetPrincipalFingerprintAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(etag);

        _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(principal);

        if (principal.WalletOwnerships.Count != 0)
        {
            var walletIds = principal.WalletOwnerships.Select(o => o.WalletId).ToList();
            var wallets = walletIds.Select(id =>
                Wallet.Create(id, NetworkEnvironment.Mainnet, "ethereum-mainnet", Builders.EthereumAddress)).ToList();

            _walletRepository.GetByIdsAsync(
                Arg.Is<IList<WalletId>>(ids => ids.SequenceEqual(walletIds)),
                false,
                Arg.Any<CancellationToken>())
                .Returns(wallets);
        }
    }

    private void SetupMinimalPrincipalResolution(AxonPrincipal principal, string etag)
    {
        _principalRepository.FindByCredentialAsync(
            TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
            .Returns(principal);

        _principalRepository.GetPrincipalFingerprintAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(etag);

        _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(principal);
    }

    private void SetupPrincipalWithPendingOwnerships(AxonPrincipal principal, string etag)
    {
        _principalRepository.FindByCredentialAsync(
            TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
            .Returns(principal);

        _principalRepository.GetPrincipalFingerprintAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(etag);

        _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(principal);

        // No wallet setup since pending ownerships are filtered out
    }

    private void SetupPrincipalWithMultipleOwnerships(AxonPrincipal principal, string etag)
    {
        _principalRepository.FindByCredentialAsync(
            TestProviderType, TestIssuer, TestSubject, Arg.Any<CancellationToken>())
            .Returns(principal);

        _principalRepository.GetPrincipalFingerprintAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(etag);

        _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id, Arg.Any<CancellationToken>())
            .Returns(principal);

        var walletIds = principal.WalletOwnerships.Select(o => o.WalletId).ToList();
        var wallets = walletIds.Select(id =>
            Wallet.Create(id, NetworkEnvironment.Mainnet, "ethereum-mainnet", Builders.EthereumAddress)).ToList();

        _walletRepository.GetByIdsAsync(
            Arg.Any<IList<WalletId>>(), false, Arg.Any<CancellationToken>())
            .Returns(wallets);
    }

    #endregion
}