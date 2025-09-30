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

namespace Axon.Modules.Identity.Application.Tests.Queries.GetMyPrincipal;

/// <summary>
/// Tests for GetMyPrincipalHandler ETag-based HTTP caching behavior.
/// Validates conditional request handling (If-None-Match) for efficient client-side caching.
/// </summary>
[TestFixture]
public class GetMyPrincipalCachingTests
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
    public async Task Handle_WithMatchingETag_ShouldReturn304NotModified()
    {
        // Arrange
        var clientETag = "matching-etag-123";
        var query = new GetMyPrincipalQuery(TestPrincipalId, clientETag);

        var notModifiedError = Error.Conflict("Content not modified", "NOT_MODIFIED")
            .WithMetadata("ETag", clientETag)
            .WithMetadata("IsNotModified", true);

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, clientETag, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CurrentUserResult, Error>(notModifiedError));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("NOT_MODIFIED");
        result.Error.Metadata.ShouldNotBeNull();
        result.Error.Metadata.ShouldContainKeyAndValue("IsNotModified", true);
        result.Error.Metadata.ShouldContainKey("ETag");
    }

    [Test]
    public async Task Handle_WithStaleETag_ShouldReturnUpdatedProfile()
    {
        // Arrange
        var staleETag = "old-etag-456";
        var newETag = "new-etag-789";
        var query = new GetMyPrincipalQuery(TestPrincipalId, staleETag);

        var updatedProfile = new CurrentUserResult(
            Profile: new UserProfile(
                AxonId: TestPrincipalId.Value.ToString(),
                RiskTier: "low"),
            Wallets: new List<WalletInfo>().AsReadOnly(),
            ETag: newETag);

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, staleETag, Arg.Any<CancellationToken>())
            .Returns(Result.Success<CurrentUserResult, Error>(updatedProfile));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe(newETag);
        result.Value.ETag.ShouldNotBe(staleETag);
    }

    [Test]
    public async Task Handle_WithNoETag_ShouldReturnFreshProfile()
    {
        // Arrange - First request without ETag
        var query = new GetMyPrincipalQuery(TestPrincipalId, null);

        var freshProfile = new CurrentUserResult(
            Profile: new UserProfile(
                AxonId: TestPrincipalId.Value.ToString(),
                RiskTier: "medium"),
            Wallets: new List<WalletInfo>().AsReadOnly(),
            ETag: "fresh-etag-999");

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success<CurrentUserResult, Error>(freshProfile));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ETag.ShouldBe("fresh-etag-999");

        await _userProfileService.Received(1).GetCurrentUserProfileAsync(
            TestPrincipalId, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithMatchingETag_ShouldNotReturnProfileData()
    {
        // Arrange
        var matchingETag = "same-etag-555";
        var query = new GetMyPrincipalQuery(TestPrincipalId, matchingETag);

        var notModifiedError = Error.Conflict("Content not modified", "NOT_MODIFIED")
            .WithMetadata("ETag", matchingETag)
            .WithMetadata("IsNotModified", true);

        _userProfileService.GetCurrentUserProfileAsync(
            TestPrincipalId, matchingETag, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CurrentUserResult, Error>(notModifiedError));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - No profile data should be returned, only metadata
        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldContain("not modified");

        // Verify service was called with correct ETag parameter
        await _userProfileService.Received(1).GetCurrentUserProfileAsync(
            TestPrincipalId, matchingETag, Arg.Any<CancellationToken>());
    }
}