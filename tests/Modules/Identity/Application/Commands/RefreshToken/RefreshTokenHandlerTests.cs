using Axon.Modules.Identity.Application.Commands.RefreshToken;
using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands.RefreshToken;

/// <summary>
/// Tests for RefreshTokenHandler command handler.
/// Sprint 5 - Command handler tests following Sprint 2-4 patterns.
///
/// TESTING PHILOSOPHY:
/// - Focus on observable behavior (tokens returned vs errors)
/// - Minimal mocking (just IAuthenticationOrchestrator)
/// - Test validation logic in handler layer
/// - Verify correct mapping of orchestrator responses
/// </summary>
[TestFixture]
public class RefreshTokenHandlerTests
{
    private IAuthenticationOrchestrator _orchestrator = null!;
    private ILogger<RefreshTokenHandler> _logger = null!;
    private RefreshTokenHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        _logger = Substitute.For<ILogger<RefreshTokenHandler>>();

        _handler = new RefreshTokenHandler(_orchestrator, _logger);
    }

    #region Success Scenarios (2 tests)

    [Test]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var refreshToken = "valid-refresh-token-abc123";
        var issuedAt = DateTimeOffset.UtcNow;
        var accessTokenExpiry = issuedAt.AddMinutes(30);
        var refreshTokenExpiry = issuedAt.AddDays(30);

        var orchestratorResponse = new RefreshTokenResponse(
            AccessToken: "new-access-token-xyz789",
            RefreshToken: "new-refresh-token-xyz789",
            TokenType: "Bearer",
            ExpiresIn: 1800,
            IssuedAt: issuedAt,
            AccessTokenExpiresAt: accessTokenExpiry,
            RefreshTokenExpiresAt: refreshTokenExpiry);

        _orchestrator.RefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<RefreshTokenResponse, Error>(orchestratorResponse));

        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var tokenResult = result.Value;
        tokenResult.AccessToken.ShouldBe("new-access-token-xyz789");
        tokenResult.RefreshToken.ShouldBe("new-refresh-token-xyz789");
        tokenResult.ExpiresAt.ShouldBe(accessTokenExpiry.DateTime);
        tokenResult.RefreshExpiresAt.ShouldBe(refreshTokenExpiry.DateTime);
    }

    [Test]
    public async Task RefreshToken_WithValidToken_ShouldCallOrchestratorOnce()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var now = DateTimeOffset.UtcNow;
        var orchestratorResponse = new RefreshTokenResponse(
            AccessToken: "access",
            RefreshToken: "refresh",
            TokenType: "Bearer",
            ExpiresIn: 1800,
            IssuedAt: now,
            AccessTokenExpiresAt: now.AddMinutes(30),
            RefreshTokenExpiresAt: now.AddDays(30));

        _orchestrator.RefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<RefreshTokenResponse, Error>(orchestratorResponse));

        var command = new RefreshTokenCommand(refreshToken);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _orchestrator.Received(1).RefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Validation Errors (2 tests)

    [Test]
    public async Task RefreshToken_WithEmptyToken_ShouldReturnValidationError()
    {
        // Arrange
        var command = new RefreshTokenCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("required");
    }

    [Test]
    public async Task RefreshToken_WithWhitespaceToken_ShouldReturnValidationError()
    {
        // Arrange
        var command = new RefreshTokenCommand("   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("AUTH.TOKEN_REQUIRED");
    }

    #endregion

    #region Orchestrator Failures (2 tests)

    [Test]
    public async Task RefreshToken_WithExpiredToken_ShouldReturnOrchestratorError()
    {
        // Arrange
        var refreshToken = "expired-refresh-token";
        var error = Error.Unauthorized("Refresh token has expired", "AUTH.REFRESH_EXPIRED");

        _orchestrator.RefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RefreshTokenResponse, Error>(error));

        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("AUTH.REFRESH_EXPIRED");
    }

    [Test]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnOrchestratorError()
    {
        // Arrange
        var refreshToken = "invalid-refresh-token";
        var error = Error.Unauthorized("Invalid refresh token", "AUTH.INVALID_TOKEN");

        _orchestrator.RefreshTokenAsync(refreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RefreshTokenResponse, Error>(error));

        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Message.ShouldContain("Invalid");
    }

    #endregion
}