using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Queries.GetCurrentUser;

[TestFixture]
public class GetCurrentUserQueryHandlerTests
{
    private IWalletAuthorizationService _walletAuthorizationService = null!;
    private IAxonPrincipalReadRepository _principalRepository = null!;
    private IWalletReadRepository _walletRepository = null!;
    private ILogger<GetCurrentUserQueryHandler> _logger = null!;
    private GetCurrentUserQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _walletAuthorizationService = Substitute.For<IWalletAuthorizationService>();
        _principalRepository = Substitute.For<IAxonPrincipalReadRepository>();
        _walletRepository = Substitute.For<IWalletReadRepository>();
        _logger = Substitute.For<ILogger<GetCurrentUserQueryHandler>>();
        
        _handler = new GetCurrentUserQueryHandler(
            _walletAuthorizationService,
            _principalRepository,
            _walletRepository,
            _logger);
    }

    [TestFixture]
    public class Handle : GetCurrentUserQueryHandlerTests
    {
        [Test]
        public async Task Should_ReturnUnauthorized_When_UserNotAuthenticated()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns((AxonId?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Unauthorized);
            result.Error.Code.ShouldBe("AUTH.NOT_AUTHENTICATED");
        }

        [Test]
        public async Task Should_ReturnNotFound_When_PrincipalDoesNotExist()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            var axonId = AxonId.New();
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns(axonId);

            _principalRepository
                .GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
                .Returns((AxonPrincipal?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Code.ShouldBe("IDENTITY.PRINCIPAL.NOT_FOUND");
        }

        [Test]
        public async Task Should_ReturnCurrentUser_When_PrincipalExistsWithNoWallets()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            var axonId = AxonId.New();
            var principal = CreateTestPrincipal(axonId);
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns(axonId);

            _principalRepository
                .GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
                .Returns(principal);

            _walletRepository
                .GetByIdsAsync(Arg.Any<IReadOnlyList<WalletId>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new List<Wallet>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Profile.AxonId.ShouldBe(axonId);
            result.Value.Profile.PrincipalType.ShouldBe("human");
            result.Value.OwnedWallets.ShouldBeEmpty();
            result.Value.SyncedAt.ShouldBeOfType<DateTimeOffset>();
        }

        [Test]
        public async Task Should_ReturnCurrentUser_When_PrincipalExistsWithWallets()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            var axonId = AxonId.New();
            var walletId = WalletId.New();
            var principal = CreateTestPrincipalWithWallet(axonId, walletId);
            var wallet = CreateTestWallet(walletId);
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns(axonId);

            _principalRepository
                .GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
                .Returns(principal);

            _walletRepository
                .GetByIdsAsync(Arg.Is<IReadOnlyList<WalletId>>(ids => ids.Contains(walletId)), false, Arg.Any<CancellationToken>())
                .Returns(new List<Wallet> { wallet });

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Profile.AxonId.ShouldBe(axonId);
            result.Value.OwnedWallets.ShouldHaveCount(1);
            result.Value.OwnedWallets.First().WalletId.ShouldBe(walletId);
            result.Value.OwnedWallets.First().Address.ShouldBe(wallet.Address.Value);
            result.Value.OwnedWallets.First().ChainId.ShouldBe("1");
            result.Value.OwnedWallets.First().ProofType.ShouldBe("dynamic_verified");
        }

        [Test]
        public async Task Should_ReturnFailure_When_ExceptionOccurs()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            var axonId = AxonId.New();
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns(axonId);

            _principalRepository
                .GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Type.ShouldBe(ErrorType.Failure);
            result.Error.Code.ShouldBe("IDENTITY.GET_CURRENT_USER.FAILED");
        }

        [Test]
        public async Task Should_UseCorrectDateTimeOffset_Types()
        {
            // Arrange
            var query = new GetCurrentUserQuery();
            var axonId = AxonId.New();
            var principal = CreateTestPrincipal(axonId);
            
            _walletAuthorizationService
                .GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
                .Returns(axonId);

            _principalRepository
                .GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
                .Returns(principal);

            _walletRepository
                .GetByIdsAsync(Arg.Any<IReadOnlyList<WalletId>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new List<Wallet>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Profile.CreatedAt.ShouldBeOfType<DateTimeOffset>();
            result.Value.Profile.UpdatedAt.ShouldBeOfType<DateTimeOffset>();
            result.Value.SyncedAt.ShouldBeOfType<DateTimeOffset>();
        }
    }

    private static AxonPrincipal CreateTestPrincipal(AxonId axonId)
    {
        var principalResult = AxonPrincipal.CreateHumanPrincipal();
        var principal = principalResult.Value;
        
        // Use reflection to set the ID for test purposes
        var idField = typeof(AxonPrincipal).GetField("_id", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(principal, axonId);

        return principal;
    }

    private static AxonPrincipal CreateTestPrincipalWithWallet(AxonId axonId, WalletId walletId)
    {
        var principal = CreateTestPrincipal(axonId);
        
        // Add wallet ownership using domain method
        var linkResult = principal.LinkWallet(
            walletId,
            ChainId.Create(1).Value,
            ProofType.DynamicVerified,
            AccessMode.Signing,
            "Test Wallet",
            TimeProvider.System);

        return principal;
    }

    private static Wallet CreateTestWallet(WalletId walletId)
    {
        var chainId = ChainId.Create(1).Value;
        var address = "0x1234567890123456789012345678901234567890";
        
        var walletResult = Wallet.RegisterAsync(
            chainId,
            address,
            DateTimeOffset.UtcNow,
            async (_, _) => false, // Not existing
            TimeProvider.System).Result;

        var wallet = walletResult.Value;
        
        // Use reflection to set the ID for test purposes
        var idField = typeof(Wallet).GetField("_id", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(wallet, walletId);

        return wallet;
    }
}