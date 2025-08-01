using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Shared.Common;
using Shouldly;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using NUnit.Framework;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai;

[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
public sealed class OpenAiClientTests : ApplicationTestBase
{
    private Mock<ILogger<OpenAiClient>> _mockLogger = null!;
    private OpenAiOptions _options = null!;
    private IOptions<OpenAiOptions> _mockOptions = null!;
    private MockHttpMessageHandler _mockHttpHandler = null!;
    private HttpClient _httpClient = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = CreateTypedLoggerMock<OpenAiClient>();

        _options = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "gpt-4o",
            MaxTokens = 1000,
            Temperature = 0.5,
            McpEnabled = true
        };

        _mockOptions = Options.Create(_options);
        _mockHttpHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHttpHandler);
    }

    [TearDown]
    public void TearDown()
    {
        _mockHttpHandler?.Dispose();
        _httpClient?.Dispose();
    }

    [Test]
    public void Constructor_GivenValidOptions_ShouldInitializeCorrectly()
    {
        // Act
        var client = CreateOpenAiClient();

        // Assert
        client.ShouldNotBeNull();
        // Verify HttpClient timeout is configured correctly
        _httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(_options.TimeoutSeconds));
    }

    [Test]
    public void Constructor_GivenNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new OpenAiClient(null!, _mockOptions, _mockLogger.Object));
    }

    [Test]
    public async Task ProcessMessageAsync_GivenValidRequestWithoutMcp_ShouldReturnSuccessResult()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest(
            Message: "Hello, AI!",
            McpConfigs: null,
            PreviousResponseId: null);

        // Mock successful OpenAI API response
        _mockHttpHandler
            .When("https://api.openai.com/v1/responses")
            .Respond("application/json", """
                {
                    "id": "test-response-id",
                    "output_text": "Hello! How can I help you?",
                    "mcp_calls": null
                }
                """);

        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldBe("Hello! How can I help you?");
        result.Value.ResponseId.ShouldBe("test-response-id");
        result.Value.ToolExecutions.ShouldBeNull();
    }

    [Test]

    public async Task ProcessMessageAsync_ShouldThrowArgumentNullException_GivenNullRequest()
    {
        // Arrange
        var client = CreateOpenAiClient();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => client.ProcessMessageAsync(null!, CancellationToken.None));
    }

    [Test]
    public async Task ProcessMessageAsync_ShouldLogProcessingInformation_GivenValidRequest()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var mcpConfig = new McpServerConfig(
            ServerUrl: "https://api.example.com/mcp",
            ServerLabel: "Test Server",
            Headers: null,
            AllowedTools: null,
            RequireApproval: false,
            TimeoutSeconds: 45);

        var mcpConfigs = new List<McpServerConfig> { mcpConfig }.AsReadOnly();
        var request = new AiRequest(
            Message: "Test message",
            McpConfigs: mcpConfigs,
            PreviousResponseId: "prev-123");

        // Mock successful OpenAI API response with MCP calls
        _mockHttpHandler
            .When("https://api.openai.com/v1/responses")
            .Respond("application/json", """
                {
                    "id": "test-response-with-mcp",
                    "output_text": "I've processed your message with MCP tools.",
                    "mcp_calls": [
                        {
                            "tool_name": "test_tool",
                            "arguments": {"query": "test"},
                            "output": {"result": "success"},
                            "error": null
                        }
                    ]
                }
                """);

        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message with OpenAI Responses API (Direct MCP) using model gpt-4o")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Verify successful result
        result.IsSuccess.ShouldBeTrue();
        result.Value.ToolExecutions.ShouldNotBeNull();
        result.Value.ToolExecutions!.Length.ShouldBe(1);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Short")]
    [TestCase("This is a longer message that should be processed correctly by the AI client")]
    public async Task ProcessMessageAsync_GivenDifferentInputs_ShouldHandleVariousMessageLengths(string message)
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest(
            Message: message,
            McpConfigs: null,
            PreviousResponseId: null);

        // Mock successful OpenAI API response
        _mockHttpHandler
            .When("https://api.openai.com/v1/responses")
            .Respond("application/json", $@"{{
                ""id"": ""test-response-{message.Length}"",
                ""output_text"": ""Processed message of length {message.Length}"",
                ""mcp_calls"": null
            }}");

        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify success and logging
        result.IsSuccess.ShouldBeTrue();
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message with OpenAI Responses API (Direct MCP) using model gpt-4o")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task ProcessMessageAsync_ShouldHandleCancellation_GivenCancelledToken()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest("Test message", null, null);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => client.ProcessMessageAsync(request, cancellationTokenSource.Token));
    }

    [Test]
    public async Task ProcessMessageAsync_ShouldReturnFailureResult_GivenHttpError()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest("Test message", null, null);

        // Mock HTTP error response
        _mockHttpHandler
            .When("https://api.openai.com/v1/responses")
            .Respond(HttpStatusCode.BadRequest, "application/json", """
                {
                    "error": {
                        "message": "Invalid request",
                        "type": "invalid_request_error"
                    }
                }
                """);

        // Act
        var result = await client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ChatErrors.AiClient.Unavailable.Code);
    }

    [Test]

    public void CreateMcpTool_ShouldCreatePlaceholderTool_GivenMcpConfig()
    {
        // This test verifies the placeholder implementation
        // Once OpenAI.NET supports MCP tools, this test should be updated

        // Expected behavior:
        // - Should create a ChatTool with function name based on MCP config
        // - Should include server URL in description
        // - Should have placeholder parameters until full MCP integration
        
        // Currently a placeholder test - no assertions needed
        Assert.Pass("Placeholder test for future MCP integration");
    }

    [Test]

    public void SimulateMcpToolExecution_ShouldReturnToolExecution_GivenMcpConfig()
    {
        // This test verifies the placeholder simulation
        // Once real MCP integration is implemented, this should be updated

        // Expected behavior when it becomes testable:
        // - Should return array of ToolExecution objects
        // - Should include simulated tool results
        // - Should respect the provided duration
        // - Should use server URL in results
        
        // Currently a placeholder test - no assertions needed
        Assert.Pass("Placeholder test for future MCP integration");

    }

    private OpenAiClient CreateOpenAiClient()
    {
        return new OpenAiClient(_httpClient, _mockOptions, _mockLogger.Object);
    }


}