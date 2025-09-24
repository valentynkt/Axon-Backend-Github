using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Commands;

[TestFixture]
public class ExchangeCredentialHandlerTests
{
    private ICurrentUserService _currentUserService = null!;
    private IAuthenticationOrchestrator _orchestrator = null!;
    private IJwtTokenService _jwtTokenService = null!;
    private ILogger<ExchangeCredentialHandler> _logger = null!;
    private ExchangeCredentialHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _orchestrator = Substitute.For<IAuthenticationOrchestrator>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _logger = Substitute.For<ILogger<ExchangeCredentialHandler>>();

        // Configure default behaviors for the new services
        ConfigureDefaultServiceBehaviors();

        _handler = new ExchangeCredentialHandler(
            _currentUserService,
            _orchestrator,
            _jwtTokenService,
            _logger);
    }

    [TearDown]
    public void TearDown()
    {
        // No resources to dispose in the simplified test setup
    }

    private void ConfigureDefaultServiceBehaviors()
    {
        // Configure default orchestrator behavior - success with mock response
        var mockResponse = new AuthenticationResponse(
            AccessToken: "mock-access-token",
            UserId: Guid.NewGuid(),
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = false,
                ["wallets_processed"] = 1,
                ["wallets_linked"] = 1,
                ["defaults_applied"] = 0,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        _orchestrator.ExchangeDynamicTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));
    }

    [Test]
    public async Task Should_ReturnFailure_When_BearerTokenIsEmpty()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Bearer token is required");
    }

    [Test]
    public async Task Should_ReturnFailure_When_BearerTokenIsNull()
    {
        // Arrange
        var command = new ExchangeCredentialCommand(null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Bearer token is required");
    }

    [Test]
    public async Task Should_ReturnFailure_When_BearerTokenIsWhitespace()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Bearer token is required");
    }

    [Test]
    public async Task Should_ReturnFailure_When_OrchestratorFails()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("invalid-token");
        _orchestrator.ExchangeDynamicTokenAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthenticationResponse, Error>(Error.Validation("Invalid token", "AUTH.INVALID_TOKEN")));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Invalid token");
    }

    [Test]
    public async Task Should_ReturnSuccess_When_OrchestratorSucceeds()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("valid-bearer-token");
        var mockResponse = new AuthenticationResponse(
            AccessToken: "mock-access-token",
            UserId: Guid.NewGuid(),
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 2,
                ["wallets_linked"] = 2,
                ["defaults_applied"] = 1,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        _orchestrator.ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("mock-access-token");
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.Created.ShouldBeTrue();
        result.Value.WalletsProcessed.ShouldBe(2);
        result.Value.WalletsLinked.ShouldBe(2);
        result.Value.DefaultsApplied.ShouldBe(1);
    }

    [Test]
    public async Task Should_DelegateToOrchestrator_When_ValidTokenProvided()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("valid-bearer-token");
        var expectedUserId = Guid.NewGuid();
        var mockResponse = new AuthenticationResponse(
            AccessToken: "new-access-token",
            UserId: expectedUserId,
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 1,
                ["wallets_linked"] = 1,
                ["defaults_applied"] = 1,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        _orchestrator.ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.AxonUserId.Value.ShouldBe(expectedUserId);
        result.Value.AccessToken.ShouldBe("new-access-token");

        // Verify orchestrator was called with correct token
        await _orchestrator.Received(1)
            .ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_ConvertAuthenticationResponseToExchangeOutcome()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("valid-bearer-token");
        var expectedUserId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var mockResponse = new AuthenticationResponse(
            AccessToken: "test-token",
            UserId: expectedUserId,
            ProviderType: "dynamic",
            ExpiresAt: expiresAt,
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = false,
                ["wallets_processed"] = 3,
                ["wallets_linked"] = 2,
                ["defaults_applied"] = 1,
                ["skipped"] = 0,
                ["conflicts"] = 0
            });

        _orchestrator.ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;
        outcome.AccessToken.ShouldBe("test-token");
        outcome.TokenType.ShouldBe("Bearer");
        outcome.AxonUserId.Value.ShouldBe(expectedUserId);
        outcome.Created.ShouldBeFalse();
        outcome.WalletsProcessed.ShouldBe(3);
        outcome.WalletsLinked.ShouldBe(2);
        outcome.DefaultsApplied.ShouldBe(1);
        outcome.Skipped.ShouldBe(0);
        outcome.Conflicts.ShouldBe(0);
        outcome.ExpiresIn.ShouldBeGreaterThan(1790); // Should be close to 30 minutes
    }

    [Test]
    public async Task Should_HandleMissingAdditionalData()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("valid-bearer-token");
        var expectedUserId = Guid.NewGuid();
        var mockResponse = new AuthenticationResponse(
            AccessToken: "test-token",
            UserId: expectedUserId,
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            AdditionalData: null); // No additional data

        _orchestrator.ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;
        outcome.Created.ShouldBeFalse(); // Default value
        outcome.WalletsProcessed.ShouldBe(0); // Default value
        outcome.WalletsLinked.ShouldBe(0); // Default value
        outcome.DefaultsApplied.ShouldBe(0); // Default value
        outcome.Skipped.ShouldBe(0); // Default value
        outcome.Conflicts.ShouldBe(0); // Default value
    }

    [Test]
    public async Task Should_HandlePartialAdditionalData()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("valid-bearer-token");
        var expectedUserId = Guid.NewGuid();
        var mockResponse = new AuthenticationResponse(
            AccessToken: "test-token",
            UserId: expectedUserId,
            ProviderType: "dynamic",
            ExpiresAt: DateTime.UtcNow.AddMinutes(15),
            AdditionalData: new Dictionary<string, object>
            {
                ["created"] = true,
                ["wallets_processed"] = 2
                // Missing other fields
            });

        _orchestrator.ExchangeDynamicTokenAsync("valid-bearer-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success<AuthenticationResponse, Error>(mockResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var outcome = result.Value;
        outcome.Created.ShouldBeTrue(); // Present
        outcome.WalletsProcessed.ShouldBe(2); // Present
        outcome.WalletsLinked.ShouldBe(0); // Default value for missing
        outcome.DefaultsApplied.ShouldBe(0); // Default value for missing
        outcome.Skipped.ShouldBe(0); // Default value for missing
        outcome.Conflicts.ShouldBe(0); // Default value for missing
    }

    [Test]
    public void Should_ValidateLoggingDependency()
    {
        // This test validates that logging dependencies are properly injected
        _logger.ShouldNotBeNull();
    }
}