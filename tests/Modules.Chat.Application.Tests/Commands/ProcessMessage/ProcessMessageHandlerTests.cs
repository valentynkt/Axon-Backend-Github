using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Shouldly;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Axon.Modules.Chat.Application.Tests.Commands.ProcessMessage;

[TestFixture]
[Category("Unit")]
[Category("Application")]
public sealed class ProcessMessageHandlerTests : ApplicationTestBase
{
    private ProcessMessageHandler _handler = null!;
    private Mock<ILogger<ProcessMessageHandler>> _typedLoggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        // Create typed logger mock for ProcessMessageHandler
        _typedLoggerMock = CreateTypedLoggerMock<ProcessMessageHandler>();
        _handler = new ProcessMessageHandler(AiClientMock.Object, _typedLoggerMock.Object);
    }

    [Test]
    public async Task Handle_GivenValidMessageWithoutMcp_ShouldReturnSuccessResult()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder
            .ForMessage("Hello, AI!")
            .Build();
        
        var expectedAiResponse = AiResponseBuilder
            .ForContent("Hello! How can I help you?")
            .WithResponseId("response-123")
            .Build();

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccessAnd(response =>
        {
            response.Response.ShouldBe("Hello! How can I help you?");
            response.ConversationId.ShouldBe("response-123");
            response.ToolExecutions.ShouldBeNull();
        });

        // Verify AI client was called with correct parameters
        AiClientMock.Verify(x => x.ProcessMessageAsync(

            It.Is<AiRequest>(req => 
                req.Message == "Hello, AI!" && 
                req.McpConfig == null &&
                req.PreviousResponseId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_GivenValidMessageWithMcpConfiguration_ShouldReturnSuccessResult()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder
            .ForMessage("Check the weather")
            .WithFullMcpConfiguration()
            .WithPreviousResponseId("prev-123")
            .Build();

        var expectedAiResponse = AiResponseBuilder
            .ForContent("The weather in Boston is sunny and 72°F.")
            .WithResponseId("response-456")
            .WithSuccessfulTool("weather", "{\"location\":\"Boston\"}", "Sunny, 72°F", 500)
            .Build();

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccessAnd(response =>
        {
            response.Response.ShouldBe("The weather in Boston is sunny and 72°F.");
            response.ConversationId.ShouldBe("response-456");
            response.ToolExecutions!.Length.ShouldBe(1);
            response.ToolExecutions![0].ToolName.ShouldBe("weather");
            response.ToolExecutions[0].Success.ShouldBeTrue();
            response.ToolExecutions[0].Duration.TotalMilliseconds.ShouldBe(500);
        });

        // Verify AI client was called with MCP configuration
        AiClientMock.Verify(x => x.ProcessMessageAsync(

            It.Is<AiRequest>(req =>
                req.Message == "Check the weather" &&
                req.McpConfig != null &&
                req.McpConfig.ServerUrl == "https://api.example.com/mcp" &&
                req.McpConfig.ServerLabel == "User-provided MCP Server" &&

                req.PreviousResponseId == "prev-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_GivenAiResponseWithoutResponseId_ShouldGenerateConversationId()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder.ForMessage("Hello").Build();
        var expectedAiResponse = AiResponseBuilder
            .ForContent("Hi there!")
            .WithoutResponseId()
            .Build();

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccessAnd(response =>
        {
            response.ConversationId.ShouldNotBeNullOrEmpty();
            // Verify it's a valid GUID format
            Guid.TryParse(response.ConversationId, out _).ShouldBeTrue();
        });
    }

    [Test]
    public async Task Handle_GivenAiClientFailure_ShouldReturnFailureResult()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder.ForMessage("Hello").Build();
        var expectedError = Error.ExternalService("AI service is unavailable");

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.ShouldBe(expectedError);
    }

    [Test]
    public async Task Handle_GivenNullOrWhitespaceServerUrl_ShouldSkipMcpConfiguration()
    {
        // Arrange
        var command = ProcessMessageCommandBuilder
            .ForMessage("Hello")
            .WithMcpServerUrl("   ")  // Whitespace only
            .WithCustomHeaders("test", "value")
            .WithSingleTool("tool1")
            .Build();

        var expectedAiResponse = AiResponseBuilder
            .ForContent("Response")
            .WithResponseId("123")
            .Build();

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        // Verify AI client was called without MCP configuration
        AiClientMock.Verify(x => x.ProcessMessageAsync(

            It.Is<AiRequest>(req => req.McpConfig == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_GivenNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => _handler.Handle(null!, CancellationToken.None));
    }

    [Test]

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

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify logging occurred (information level calls)
        _typedLoggerMock.Verify(

            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _typedLoggerMock.Verify(

            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully processed message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]

    public async Task Handle_ShouldLogErrorMessage_GivenAiClientFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message");
        var expectedError = Error.ExternalService("Service unavailable");

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(expectedError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();

        // Verify error logging occurred
        _typedLoggerMock.Verify(

            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AI client failed to process message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]

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

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ToolExecutions!.Length.ShouldBe(2);

        var weatherExecution = result.Value.ToolExecutions![0];
        weatherExecution.ToolName.ShouldBe("weather");
        weatherExecution.Success.ShouldBeTrue();
        weatherExecution.Duration.TotalMilliseconds.ShouldBe(750);

        var calendarExecution = result.Value.ToolExecutions[1];
        calendarExecution.ToolName.ShouldBe("calendar");
        calendarExecution.Success.ShouldBeFalse();
        calendarExecution.Duration.TotalMilliseconds.ShouldBe(250);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task Handle_GivenInvalidMcpServerUrl_ShouldCreateMcpConfigAsNull(string? mcpServerUrl)

    {
        // Arrange
        var command = new ProcessMessageCommand(
            Message: "Test",
            McpServerUrl: mcpServerUrl);

        var expectedAiResponse = new AiResponse("Response", "123", null);

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedAiResponse));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify AI client was called without MCP configuration
        AiClientMock.Verify(x => x.ProcessMessageAsync(

            It.Is<AiRequest>(req => req.McpConfig == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]

    public async Task Handle_ShouldHandleCancellation_GivenCancelledToken()
    {
        // Arrange
        var command = new ProcessMessageCommand("Test message");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        AiClientMock

            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(command, cancellationTokenSource.Token));

    }
}