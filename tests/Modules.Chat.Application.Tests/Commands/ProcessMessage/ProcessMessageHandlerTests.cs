using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Axon.Modules.Chat.Application.Tests.Commands.ProcessMessage;

public sealed class ProcessMessageHandlerTests
{
    private readonly Mock<IAiClient> _mockAiClient;
    private readonly Mock<ILogger<ProcessMessageHandler>> _mockLogger;
    private readonly ProcessMessageHandler _handler;

    public ProcessMessageHandlerTests()
    {
        _mockAiClient = new Mock<IAiClient>();
        _mockLogger = new Mock<ILogger<ProcessMessageHandler>>();
        _handler = new ProcessMessageHandler(_mockAiClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_GivenValidMessageWithoutMcp()
    {
        // Arrange
        var command = new ProcessMessageCommand("Hello, AI!");
        var expectedAiResponse = new AiResponse(
            Content: "Hello! How can I help you?",
            ResponseId: "response-123",
            ToolExecutions: null);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().Be("Hello! How can I help you?");
        result.Value.ConversationId.Should().Be("response-123");
        result.Value.ToolExecutions.Should().BeNull();

        // Verify AI client was called with correct parameters
        _mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req => 
                req.Message == "Hello, AI!" && 
                req.McpConfig == null &&
                req.PreviousResponseId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_GivenValidMessageWithMcpConfiguration()
    {
        // Arrange
        var headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } };
        var allowedTools = new[] { "weather", "calendar" };
        var command = new ProcessMessageCommand(
            Message: "Check the weather",
            McpServerUrl: "https://api.example.com/mcp",
            McpHeaders: headers,
            AllowedTools: allowedTools,
            PreviousResponseId: "prev-123");

        var toolExecutions = new[]
        {
            ToolExecution.Success("weather", "{\"location\":\"Boston\"}", "Sunny, 72°F", TimeSpan.FromMilliseconds(500))
        };

        var expectedAiResponse = new AiResponse(
            Content: "The weather in Boston is sunny and 72°F.",
            ResponseId: "response-456",
            ToolExecutions: toolExecutions);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().Be("The weather in Boston is sunny and 72°F.");
        result.Value.ConversationId.Should().Be("response-456");
        result.Value.ToolExecutions.Should().HaveCount(1);
        result.Value.ToolExecutions![0].ToolName.Should().Be("weather");
        result.Value.ToolExecutions[0].Success.Should().BeTrue();
        result.Value.ToolExecutions[0].Duration.TotalMilliseconds.Should().Be(500);

        // Verify AI client was called with MCP configuration
        _mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req =>
                req.Message == "Check the weather" &&
                req.McpConfig != null &&
                req.McpConfig.ServerUrl == "https://api.example.com/mcp" &&
                req.McpConfig.ServerLabel == "User-provided MCP Server" &&
                req.McpConfig.Headers == headers &&
                req.McpConfig.AllowedTools == allowedTools &&
                req.McpConfig.RequireApproval == false &&
                req.PreviousResponseId == "prev-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldGenerateConversationId_GivenAiResponseWithoutResponseId()
    {
        // Arrange
        var command = new ProcessMessageCommand("Hello");
        var expectedAiResponse = new AiResponse(
            Content: "Hi there!",
            ResponseId: null,  // No response ID provided
            ToolExecutions: null);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ConversationId.Should().NotBeNullOrEmpty();
        // Verify it's a valid GUID format
        Guid.TryParse(result.Value.ConversationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureResult_GivenAiClientFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("Hello");
        var expectedError = Error.ExternalService("AI service is unavailable");

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public async Task Handle_ShouldSkipMcpConfiguration_GivenNullOrWhitespaceServerUrl()
    {
        // Arrange
        var command = new ProcessMessageCommand(
            Message: "Hello",
            McpServerUrl: "   ",  // Whitespace only
            McpHeaders: new Dictionary<string, string> { { "test", "value" } },
            AllowedTools: new[] { "tool1" });

        var expectedAiResponse = new AiResponse("Response", "123", null);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify AI client was called without MCP configuration
        _mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req => req.McpConfig == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_GivenNullRequest()
    {
        // Act & Assert
        await FluentActions
            .Invoking(() => _handler.Handle(null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ShouldLogInformationMessages_GivenSuccessfulProcessing()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message");
        var toolExecutions = new[]
        {
            ToolExecution.Success("tool1", "{}", "result1", TimeSpan.FromMilliseconds(100)),
            ToolExecution.Success("tool2", "{}", "result2", TimeSpan.FromMilliseconds(200))
        };
        
        var expectedAiResponse = new AiResponse("Response", "123", toolExecutions);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify logging occurred (information level calls)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully processed message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogErrorMessage_GivenAiClientFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message");
        var expectedError = Error.ExternalService("Service unavailable");

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();

        // Verify error logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AI client failed to process message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapToolExecutionsCorrectly_GivenMultipleToolExecutions()
    {
        // Arrange
        var command = new ProcessMessageCommand("Execute tools");
        var toolExecutions = new[]
        {
            ToolExecution.Success("weather", "{\"location\":\"NYC\"}", "Cloudy", TimeSpan.FromMilliseconds(750)),
            ToolExecution.Failure("calendar", "{\"date\":\"today\"}", "Calendar not available", TimeSpan.FromMilliseconds(250))
        };

        var expectedAiResponse = new AiResponse("Tool results processed", "456", toolExecutions);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToolExecutions.Should().HaveCount(2);

        var weatherExecution = result.Value.ToolExecutions![0];
        weatherExecution.ToolName.Should().Be("weather");
        weatherExecution.Success.Should().BeTrue();
        weatherExecution.Duration.TotalMilliseconds.Should().Be(750);

        var calendarExecution = result.Value.ToolExecutions[1];
        calendarExecution.ToolName.Should().Be("calendar");
        calendarExecution.Success.Should().BeFalse();
        calendarExecution.Duration.TotalMilliseconds.Should().Be(250);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_ShouldCreateMcpConfigAsNull_GivenInvalidMcpServerUrl(string? mcpServerUrl)
    {
        // Arrange
        var command = new ProcessMessageCommand(
            Message: "Test",
            McpServerUrl: mcpServerUrl);

        var expectedAiResponse = new AiResponse("Response", "123", null);

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify AI client was called without MCP configuration
        _mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req => req.McpConfig == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHandleCancellation_GivenCancelledToken()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await FluentActions
            .Invoking(() => _handler.Handle(command, cancellationTokenSource.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }
}