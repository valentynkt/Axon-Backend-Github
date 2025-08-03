using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Axon.Modules.Chat.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai.Services;

/// <summary>
/// Unit tests for ResponseParser following London School approach
/// Tests the SRP-compliant service extracted from OpenAiClient refactoring
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
[Category("ResponseParser")]
public sealed class ResponseParserTests
{
    private ResponseParser _sut = null!;
    private Mock<IPayloadSerializer> _mockPayloadSerializer = null!;
    private Mock<ILogger<ResponseParser>> _mockLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _mockPayloadSerializer = new Mock<IPayloadSerializer>();
        _mockLogger = new Mock<ILogger<ResponseParser>>();
        
        _sut = new ResponseParser(_mockPayloadSerializer.Object, _mockLogger.Object);
    }

    [Test]
    public void Constructor_WithValidDependencies_ShouldCreateInstance()
    {
        // Act & Assert
        _sut.ShouldNotBeNull();
    }

    [Test]
    public void Constructor_WithNullPayloadSerializer_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ResponseParser(null!, _mockLogger.Object));
    }

    [Test]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ResponseParser(_mockPayloadSerializer.Object, null!));
    }

    [Test]
    public void ParseResponse_WithValidResponseBody_ShouldCallDeserializer()
    {
        // Arrange
        var responseBody = "{\"id\":\"test-id\",\"output\":[]}";
        var expectedResponse = new ResponsesApiResponse("test-id", Array.Empty<OutputItem>());

        _mockPayloadSerializer
            .Setup(x => x.Deserialize<ResponsesApiResponse>(responseBody))
            .Returns(expectedResponse);

        // Act
        var result = _sut.ParseResponse(responseBody);

        // Assert
        _mockPayloadSerializer.Verify(
            x => x.Deserialize<ResponsesApiResponse>(responseBody), 
            Times.Once);
        result.ShouldBe(expectedResponse);
    }

    [Test]
    public void ParseResponse_WithNullResponseBody_ShouldThrowJsonException()
    {
        // Act & Assert
        Should.Throw<JsonException>(() => _sut.ParseResponse(null!));
    }

    [Test]
    public void ParseResponse_WithEmptyResponseBody_ShouldThrowJsonException()
    {
        // Act & Assert
        Should.Throw<JsonException>(() => _sut.ParseResponse(string.Empty));
    }

    [Test]
    public void ParseResponse_WithWhitespaceResponseBody_ShouldThrowJsonException()
    {
        // Act & Assert
        Should.Throw<JsonException>(() => _sut.ParseResponse("   "));
    }

    [Test]
    public void ParseResponse_WhenDeserializationReturnsNull_ShouldThrowJsonException()
    {
        // Arrange
        var responseBody = "{\"invalid\":\"response\"}";
        
        _mockPayloadSerializer
            .Setup(x => x.Deserialize<ResponsesApiResponse>(responseBody))
            .Returns((ResponsesApiResponse?)null);

        // Act & Assert
        Should.Throw<JsonException>(() => _sut.ParseResponse(responseBody));
    }

    [Test]
    public void ParseResponse_WithMessageOutput_ShouldLogResponseDetails()
    {
        // Arrange
        var responseBody = "{\"id\":\"test-id\",\"output\":[{\"type\":\"message\",\"content\":[{\"text\":\"Hello\"}]}]}";
        var outputContent = JsonDocument.Parse("[{\"text\":\"Hello\"}]").RootElement;
        var expectedResponse = new ResponsesApiResponse("test-id", new[]
        {
            new OutputItem("message", outputContent)
        });

        _mockPayloadSerializer
            .Setup(x => x.Deserialize<ResponsesApiResponse>(responseBody))
            .Returns(expectedResponse);

        // Act
        var result = _sut.ParseResponse(responseBody);

        // Assert
        result.Id.ShouldBe("test-id");
        result.Output.ShouldNotBeNull();
        result.Output.Length.ShouldBe(1);
        result.Output[0].Type.ShouldBe("message");
    }

    [Test]
    public void ValidateResponse_WithSuccessfulResponse_ShouldNotThrow()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        var responseBody = "{\"id\":\"test-id\"}";

        // Act & Assert
        Should.NotThrow(() => _sut.ValidateResponse(response, responseBody));
    }

    [Test]
    public void ValidateResponse_WithBadRequestResponse_ShouldThrowHttpRequestException()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var responseBody = "{\"error\":\"Bad request\"}";

        // Act & Assert
        var exception = Should.Throw<HttpRequestException>(() => 
            _sut.ValidateResponse(response, responseBody));
        exception.Message.ShouldContain("BadRequest");
        exception.Message.ShouldContain(responseBody);
    }

    [Test]
    public void ValidateResponse_WithUnauthorizedResponse_ShouldThrowHttpRequestException()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var responseBody = "{\"error\":\"Unauthorized\"}";

        // Act & Assert
        var exception = Should.Throw<HttpRequestException>(() => 
            _sut.ValidateResponse(response, responseBody));
        exception.Message.ShouldContain("Unauthorized");
    }

    [Test]
    public void ValidateResponse_WithInternalServerErrorResponse_ShouldThrowHttpRequestException()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var responseBody = "{\"error\":\"Internal server error\"}";

        // Act & Assert
        var exception = Should.Throw<HttpRequestException>(() => 
            _sut.ValidateResponse(response, responseBody));
        exception.Message.ShouldContain("InternalServerError");
    }

    [Test]
    public void ValidateResponse_WithTooManyRequestsResponse_ShouldThrowHttpRequestException()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var responseBody = "{\"error\":\"Rate limit exceeded\"}";

        // Act & Assert
        var exception = Should.Throw<HttpRequestException>(() => 
            _sut.ValidateResponse(response, responseBody));
        exception.Message.ShouldContain("TooManyRequests");
        exception.Message.ShouldContain("Rate limit exceeded");
    }

    [Test]
    public void ValidateResponse_WithErrorResponse_ShouldLogError()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var responseBody = "{\"error\":\"Test error\"}";

        // Act
        Should.Throw<HttpRequestException>(() => 
            _sut.ValidateResponse(response, responseBody));

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BadRequest")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void ParseResponse_WithComplexResponse_ShouldHandleCorrectly()
    {
        // Arrange
        var responseBody = """
        {
            "id": "response-123",
            "output": [
                {
                    "type": "message",
                    "content": [
                        {
                            "type": "text",
                            "text": "This is a test response"
                        }
                    ]
                },
                {
                    "type": "tool_call",
                    "content": {
                        "tool_name": "search",
                        "parameters": {"query": "test"}
                    }
                }
            ]
        }
        """;

        var messageContent = JsonDocument.Parse("""[{"type":"text","text":"This is a test response"}]""").RootElement;
        var toolContent = JsonDocument.Parse("""{"tool_name":"search","parameters":{"query":"test"}}""").RootElement;
        
        var expectedResponse = new ResponsesApiResponse("response-123", new[]
        {
            new OutputItem("message", messageContent),
            new OutputItem("tool_call", toolContent)
        });

        _mockPayloadSerializer
            .Setup(x => x.Deserialize<ResponsesApiResponse>(responseBody))
            .Returns(expectedResponse);

        // Act
        var result = _sut.ParseResponse(responseBody);

        // Assert
        result.Id.ShouldBe("response-123");
        result.Output.ShouldNotBeNull();
        result.Output.Length.ShouldBe(2);
        result.Output[0].Type.ShouldBe("message");
        result.Output[1].Type.ShouldBe("tool_call");
    }
}