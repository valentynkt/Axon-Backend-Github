using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands.ExchangeCredential;

/// <summary>
/// Tests for ExchangeCredentialHandler command handler.
/// Sprint 6 - Command handler tests following Sprint 5 patterns.
///
/// TESTING PHILOSOPHY:
/// - Focus on token exchange orchestration + complex DTO mapping
/// - Test dictionary parsing from AuthenticationResponse.AdditionalData
/// - Verify safe extraction helpers (GetIntValue, GetBoolValue)
/// - Minimal mocking (orchestrator + jwtTokenService + logger)
/// </summary>
[TestFixture]
public class ExchangeCredentialHandlerTests
{
    private IAuthenticationOrchestrator _orchestrator = null!;
    private IJwtTokenService _jwtTokenService = null!;
    private ICurrentUserService _currentUserService = null!;
    private ILogger<ExchangeCredentialHandler> _logger = null!;
    private ExchangeCredentialHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<ExchangeCredentialHandler>>();

        _handler = new ExchangeCredentialHandler(
            _currentUserService,
            _orchestrator,
            _jwtTokenService,
            _logger);
    }

    #region Success Scenarios (3 tests)

    [Test]
    public async Task ExchangeCredential_WithValidToken_ShouldReturnExchangeOutcome()
    {
        // Arrange
        var bearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.dynamic-jwt-token";
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var authResponse = new AuthenticationResponse(
            AccessToken: "axon-access-token-xyz789",
            UserId: userId,
            ProviderType: "Dynamic",
            ExpiresAt: expiresAt,
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 5,
                ["wallets_linked"] = 3,
                ["defaults_applied"] = 2,
                ["skipped"] = 1,
                ["conflicts"] = 0
            });

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;
        outcome.AccessToken.ShouldBe("axon-access-token-xyz789");
        outcome.TokenType.ShouldBe("Bearer");
        outcome.AxonUserId.Value.ShouldBe(userId);
        outcome.Created.ShouldBeTrue();
        outcome.WalletsProcessed.ShouldBe(5);
        outcome.WalletsLinked.ShouldBe(3);
        outcome.DefaultsApplied.ShouldBe(2);
        outcome.Skipped.ShouldBe(1);
        outcome.Conflicts.ShouldBe(0);
    }

    [Test]
    public async Task ExchangeCredential_WithValidToken_ShouldCallOrchestratorOnce()
    {
        // Arrange
        var bearerToken = "valid-dynamic-jwt-token";
        var authResponse = new AuthenticationResponse(
            AccessToken: "axon-token",
            UserId: Guid.NewGuid(),
            ProviderType: "Dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: null);

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _orchestrator.Received(1).ExchangeDynamicTokenAsync(
            bearerToken,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExchangeCredential_MapsAllFieldsCorrectly()
    {
        // Arrange
        var bearerToken = "dynamic-jwt-token";
        var userId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var expectedExpiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

        var authResponse = new AuthenticationResponse(
            AccessToken: "mapped-access-token",
            UserId: userId,
            ProviderType: "Dynamic",
            ExpiresAt: expiresAt,
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = false,
                ["wallets_processed"] = 10,
                ["wallets_linked"] = 8,
                ["defaults_applied"] = 5,
                ["skipped"] = 2,
                ["conflicts"] = 1
            });

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;

        // Verify all fields mapped correctly
        outcome.AccessToken.ShouldBe("mapped-access-token");
        outcome.TokenType.ShouldBe("Bearer"); // Hardcoded
        outcome.ExpiresIn.ShouldBeInRange(expectedExpiresIn - 1, expectedExpiresIn + 1); // Allow 1s variance
        outcome.AxonUserId.Value.ShouldBe(userId);
        outcome.Created.ShouldBeFalse();
        outcome.WalletsProcessed.ShouldBe(10);
        outcome.WalletsLinked.ShouldBe(8);
        outcome.DefaultsApplied.ShouldBe(5);
        outcome.Skipped.ShouldBe(2);
        outcome.Conflicts.ShouldBe(1);
    }

    #endregion

    #region Validation Scenarios (2 tests)

    [Test]
    public async Task ExchangeCredential_WithEmptyToken_ShouldReturnValidationError()
    {
        // Arrange
        var command = new ExchangeCredentialCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("AUTH.TOKEN_REQUIRED");
        result.Error.Message.ShouldBe("Bearer token is required");

        // Should not call orchestrator
        await _orchestrator.DidNotReceive().ExchangeDynamicTokenAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExchangeCredential_WithWhitespaceToken_ShouldReturnValidationError()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("AUTH.TOKEN_REQUIRED");

        // Should not call orchestrator
        await _orchestrator.DidNotReceive().ExchangeDynamicTokenAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Propagation (2 tests)

    [Test]
    public async Task ExchangeCredential_WithInvalidToken_ShouldReturnOrchestratorError()
    {
        // Arrange
        var bearerToken = "invalid-jwt-token";
        var error = Error.Unauthorized("Invalid JWT signature", "AUTH.INVALID_TOKEN");

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationResponse, Error>(error));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("AUTH.INVALID_TOKEN");
        result.Error.Message.ShouldBe("Invalid JWT signature");
    }

    [Test]
    public async Task ExchangeCredential_WithExpiredToken_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var bearerToken = "expired-jwt-token";
        var error = Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED");

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationResponse, Error>(error));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("AUTH.TOKEN_EXPIRED");
    }

    #endregion

    #region Dictionary Parsing Edge Cases (3 tests)

    [Test]
    public async Task ExchangeCredential_WithNullAdditionalData_ShouldUseDefaults()
    {
        // Arrange
        var bearerToken = "valid-token";
        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: Guid.NewGuid(),
            ProviderType: "Dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: null); // Null dictionary

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;

        // All dictionary-based fields should use defaults
        outcome.Created.ShouldBeFalse(); // Default bool
        outcome.WalletsProcessed.ShouldBe(0); // Default int
        outcome.WalletsLinked.ShouldBe(0);
        outcome.DefaultsApplied.ShouldBe(0);
        outcome.Skipped.ShouldBe(0);
        outcome.Conflicts.ShouldBe(0);
    }

    [Test]
    public async Task ExchangeCredential_WithPartialAdditionalData_ShouldHandleGracefully()
    {
        // Arrange
        var bearerToken = "valid-token";
        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: Guid.NewGuid(),
            ProviderType: "Dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true, // Only 3 of 6 keys present
                ["wallets_processed"] = 7,
                ["conflicts"] = 2
                // Missing: wallets_linked, defaults_applied, skipped
            });

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;

        // Present keys should have their values
        outcome.Created.ShouldBeTrue();
        outcome.WalletsProcessed.ShouldBe(7);
        outcome.Conflicts.ShouldBe(2);

        // Missing keys should use defaults
        outcome.WalletsLinked.ShouldBe(0);
        outcome.DefaultsApplied.ShouldBe(0);
        outcome.Skipped.ShouldBe(0);
    }

    [Test]
    public async Task ExchangeCredential_WithMixedDataTypes_ShouldConvertCorrectly()
    {
        // Arrange
        var bearerToken = "valid-token";
        var authResponse = new AuthenticationResponse(
            AccessToken: "token",
            UserId: Guid.NewGuid(),
            ProviderType: "Dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = "true", // String instead of bool
                ["wallets_processed"] = "15", // String instead of int
                ["wallets_linked"] = 10, // Actual int
                ["defaults_applied"] = "0", // String "0"
                ["skipped"] = 1, // Actual int
                ["conflicts"] = "3" // String "3"
            });

        _orchestrator.ExchangeDynamicTokenAsync(bearerToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(authResponse));

        var command = new ExchangeCredentialCommand(bearerToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;

        // Convert.ToBoolean and Convert.ToInt32 should handle strings
        outcome.Created.ShouldBeTrue();
        outcome.WalletsProcessed.ShouldBe(15);
        outcome.WalletsLinked.ShouldBe(10);
        outcome.DefaultsApplied.ShouldBe(0);
        outcome.Skipped.ShouldBe(1);
        outcome.Conflicts.ShouldBe(3);
    }

    #endregion
}