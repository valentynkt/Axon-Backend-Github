using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Queries;

/// <summary>
/// Test suite for the refactored GetMyPrincipalHandler that delegates to UserProfileService.
/// Tests the SOLID-compliant thin orchestration layer.
/// </summary>
[TestFixture]
public class GetMyPrincipalHandlerRefactoredTests
{
    private ICurrentUserService _currentUserService = null!;
    private IUserProfileService _userProfileService = null!;
    private ILogger<GetMyPrincipalHandler> _logger = null!;
    private GetMyPrincipalHandler _handler = null!;

    private static readonly AxonUserId TestPrincipalId = new(Guid.NewGuid());

    [SetUp]
    public void SetUp()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _userProfileService = Substitute.For<IUserProfileService>();
        _logger = Substitute.For<ILogger<GetMyPrincipalHandler>>();

        _handler = new GetMyPrincipalHandler(
            _currentUserService,
            _userProfileService,
            _logger);
    }

    [Test]
    public async Task Handle_Should_DelegateToUserProfileService()
    {
        // Arrange
        var query = new GetMyPrincipalQuery(TestPrincipalId);
        var expectedResult = CreateTestUserResult();

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<CurrentUserResult, Error>(expectedResult));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedResult);

        await _userProfileService.Received(1).GetCurrentUserProfileAsync(
            TestPrincipalId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WhenServiceReturnsFailure_Should_ReturnFailure()
    {
        // Arrange
        var query = new GetMyPrincipalQuery(TestPrincipalId);
        var expectedError = Error.NotFound("Principal not found");

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CurrentUserResult, Error>(expectedError));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedError);
    }

    [Test]
    public async Task Handle_Should_LogDebugMessages()
    {
        // Arrange
        var query = new GetMyPrincipalQuery(TestPrincipalId);
        var expectedResult = CreateTestUserResult();

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<CurrentUserResult, Error>(expectedResult));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Verify logger was called with correct levels and messages
        _logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Processing GetMyPrincipal query")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully retrieved user profile")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task Handle_WhenServiceFails_Should_LogFailure()
    {
        // Arrange
        var query = new GetMyPrincipalQuery(TestPrincipalId);
        var expectedError = Error.NotFound("Principal not found");

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CurrentUserResult, Error>(expectedError));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Verify failure was logged at Warning level with error details
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to retrieve user profile") &&
                                o.ToString()!.Contains(TestPrincipalId.Value.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    private static CurrentUserResult CreateTestUserResult()
    {
        return new CurrentUserResult(
            Profile: new UserProfile(
                AxonId: TestPrincipalId.Value.ToString(),
                RiskTier: "low"),
            Wallets: new List<WalletInfo>().AsReadOnly());
    }
}