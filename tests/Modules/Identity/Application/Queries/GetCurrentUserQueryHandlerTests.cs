using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Queries.GetCurrentUser;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Queries;

[TestFixture]
public class GetCurrentUserQueryHandlerTests
{
    private IWalletAuthorizationService _walletAuthorizationService;
    private IAxonPrincipalReadRepository _principalRepository;
    private IWalletReadRepository _walletRepository;
    private ILogger<GetCurrentUserQueryHandler> _logger;
    private GetCurrentUserQueryHandler _handler;

    [SetUp]
    public void SetUp()
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

    [Test]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var query = new GetCurrentUserQuery();
        _walletAuthorizationService.GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
            .Returns((AxonId?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AUTH.NOT_AUTHENTICATED");
    }

    [Test]
    public async Task Handle_WhenPrincipalNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var query = new GetCurrentUserQuery();
        var axonId = AxonId.New();
        
        _walletAuthorizationService.GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
            .Returns(axonId);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("IDENTITY.PRINCIPAL.NOT_FOUND");
    }

    [Test]
    public async Task Handle_WhenPrincipalFoundWithNoWallets_ShouldReturnSuccessWithEmptyWallets()
    {
        // Arrange
        var query = new GetCurrentUserQuery();
        var axonId = AxonId.New();
        
        var principal = AxonPrincipal.CreateHumanPrincipal().Value;
        
        _walletAuthorizationService.GetCurrentUserAxonIdAsync(Arg.Any<CancellationToken>())
            .Returns(axonId);
        _principalRepository.GetByIdWithActiveOwnershipsAsync(axonId, Arg.Any<CancellationToken>())
            .Returns(principal);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.OwnedWallets.ShouldBeEmpty();
        result.Value.Profile.ShouldNotBeNull();
        result.Value.Profile.AxonId.ShouldBe(principal.Id);
    }
}