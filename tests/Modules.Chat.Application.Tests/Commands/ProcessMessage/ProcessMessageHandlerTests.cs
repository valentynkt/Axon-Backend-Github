using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Commands.ProcessMessage;

[TestFixture]
[Category("Unit")]
[Category("Application")]
public sealed class ProcessMessageHandlerTests
{
    private ProcessMessageHandler _handler = null!;
    private Mock<ILogger<ProcessMessageHandler>> _loggerMock = null!;
    private Mock<IAiClient> _aiClientMock = null!;
    private Mock<IMessageRequestBuilder> _requestBuilderMock = null!;
    private Mock<IResponseMappingService> _responseMappingMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<ProcessMessageHandler>>();
        _aiClientMock = new Mock<IAiClient>();
        _requestBuilderMock = new Mock<IMessageRequestBuilder>();
        _responseMappingMock = new Mock<IResponseMappingService>();

        _handler = new ProcessMessageHandler(
            _aiClientMock.Object,
            _requestBuilderMock.Object,
            _responseMappingMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = new ProcessMessageCommand("test message");
        var aiRequest = new AiRequest("test message");
        var aiResponse = new AiResponse("test response");
        var expectedResponse = new ProcessMessageResponse("test response");

        _requestBuilderMock
            .Setup(x => x.BuildAiRequest(It.IsAny<ProcessMessageCommand>()))
            .Returns(Result<(AiRequest Request, int McpServerCount)>.Success((aiRequest, 2)));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(aiResponse));

        _responseMappingMock
            .Setup(x => x.MapToApiResponse(It.IsAny<AiResponse>()))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Response.ShouldBe("test response");
    }

    [Test]
    public async Task Handle_WhenRequestBuildingFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("test message");
        var error = Error.Validation("Request building failed");

        _requestBuilderMock
            .Setup(x => x.BuildAiRequest(It.IsAny<ProcessMessageCommand>()))
            .Returns(Result<(AiRequest Request, int McpServerCount)>.Failure(error));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Test]
    public async Task Handle_WhenAiClientFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new ProcessMessageCommand("test message");
        var aiRequest = new AiRequest("test message");
        var error = Error.ExternalService("AI client failed");

        _requestBuilderMock
            .Setup(x => x.BuildAiRequest(It.IsAny<ProcessMessageCommand>()))
            .Returns(Result<(AiRequest Request, int McpServerCount)>.Success((aiRequest, 2)));

        _aiClientMock
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(error));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        var handler = new ProcessMessageHandler(
            _aiClientMock.Object,
            _requestBuilderMock.Object,
            _responseMappingMock.Object,
            _loggerMock.Object);

        handler.ShouldNotBeNull();
    }
}