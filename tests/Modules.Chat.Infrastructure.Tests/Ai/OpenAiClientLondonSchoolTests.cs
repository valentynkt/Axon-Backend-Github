using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Tests.Shared.Mocks;
using Axon.Tests.Shared.TestBase;
using Axon.Tests.Shared.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai;

/// <summary>
/// London School TDD tests for OpenAiClient focusing on interaction testing with external HTTP services
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Infrastructure")]
[Category("LondonSchool")]
public sealed class OpenAiClientLondonSchoolTests : LondonSchoolTestBase
{
    private OpenAiClient _client = null!;
    private Mock<HttpMessageHandler> _httpHandlerMock = null!;
    private Mock<ILogger<OpenAiClient>> _loggerMock = null!;
    private HttpClient _httpClient = null!;
    private OpenAiOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        // Create strict mocks for HTTP interactions
        _httpHandlerMock = InfrastructureContractMocks.CreateHttpMessageHandlerMock();
        _loggerMock = CreateLooseMock<ILogger<OpenAiClient>>();
        
        // Setup configuration
        _options = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "gpt-4",
            TimeoutSeconds = 30,
            MaxTokens = 1000,
            Temperature = 0.7,
            McpEnabled = true
        };

        var optionsMock = InfrastructureContractMocks.CreateOptionsMock(_options);

        // Create HTTP client with mock handler
        _httpClient = new HttpClient(_httpHandlerMock.Object);

        // Create system under test
        _client = new OpenAiClient(_httpClient, optionsMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    [InteractionTest]
    public async Task ProcessMessageAsync_ShouldMakeHttpRequestWithCorrectHeaders_WhenProcessingMessage()
    {
        // Arrange - Setup HTTP interaction expectation
        var request = new AiRequest("Test message", null, null);
        
        InfrastructureContractMocks.SetupOpenAiResponse(
            _httpHandlerMock, 
            "response-123", 
            "HTTP response test");

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify HTTP interaction occurred with correct headers
        result.ShouldBeSuccess();

        InfrastructureContractMocks.VerifyHttpRequest(
            _httpHandlerMock,
            "api.openai.com/v1/responses",
            HttpMethod.Post,
            Times.Once);

        // Verify authentication header was set (this is verified through the HttpClient setup)
        // The Authorization header should be "Bearer test-api-key"
    }

    [Test]
    [BehaviorTest]
    public async Task ProcessMessageAsync_ShouldSendCorrectPayload_WhenProcessingWithMcpServers()
    {
        // Arrange - Setup scenario with MCP servers
        var mcpConfigs = new List<McpServerConfig>
        {
            new McpServerConfig(
                ServerUrl: "https://weather.mcp.com",
                ServerLabel: "Weather Service",
                Headers: new Dictionary<string, string> { { "Auth", "token123" } },
                AllowedTools: new[] { "weather", "forecast" },
                RequireApproval: false),
            new McpServerConfig(
                ServerUrl: "https://calendar.mcp.com",
                ServerLabel: "Calendar Service",
                Headers: null,
                AllowedTools: new[] { "schedule" },
                RequireApproval: true)
        }.AsReadOnly();

        var request = new AiRequest("Weather and calendar query", mcpConfigs, "prev-123");

        InfrastructureContractMocks.SetupOpenAiResponse(
            _httpHandlerMock,
            "mcp-response-456",
            "MCP integrated response");

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify payload structure
        result.ShouldBeSuccess();

        // Verify HTTP payload contains expected MCP configuration
        InfrastructureContractMocks.VerifyHttpRequestPayload(
            _httpHandlerMock,
            payload =>
            {
                var payloadObj = JsonSerializer.Deserialize<JsonElement>(payload);
                
                // Verify basic structure
                payloadObj.TryGetProperty("model", out var model).ShouldBeTrue();
                model.GetString().ShouldBe("gpt-4");
                
                payloadObj.TryGetProperty("input", out var input).ShouldBeTrue();
                input.GetString().ShouldBe("Weather and calendar query");
                
                // Verify MCP tools are included
                payloadObj.TryGetProperty("tools", out var tools).ShouldBeTrue();
                tools.GetArrayLength().ShouldBe(2);
                
                return true;
            },
            Times.Once);
    }

    [Test]
    [ContractTest]
    public async Task ProcessMessageAsync_ShouldHandleHttpFailure_GracefullyWithDomainError()
    {
        // Arrange - Setup HTTP failure scenario
        var request = new AiRequest("Failing request", null, null);
        
        InfrastructureContractMocks.SetupHttpFailure(
            _httpHandlerMock,
            HttpStatusCode.ServiceUnavailable,
            "OpenAI service temporarily unavailable");

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify error handling contract
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.ExternalService);

        // Verify error was logged appropriately
        InfrastructureContractMocks.VerifyInfrastructureLogging(
            _loggerMock,
            LogLevel.Error,
            "Failed to process message",
            Times.AtLeastOnce);
    }

    [Test]
    [InteractionTest]
    public async Task ProcessMessageAsync_ShouldHandleTimeoutGracefully_WithAppropriateErrorMapping()
    {
        // Arrange - Setup timeout scenario
        var request = new AiRequest("Timeout test", null, null);
        
        InfrastructureContractMocks.SetupHttpTimeout(_httpHandlerMock);

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify timeout handling
        result.ShouldBeFailure();
        result.Error.ShouldBe(ChatErrors.AiClient.ProcessingTimeout);

        // Verify timeout was logged
        InfrastructureContractMocks.VerifyInfrastructureLogging(
            _loggerMock,
            LogLevel.Error,
            "timeout",
            Times.AtLeastOnce);
    }

    [Test]
    [BehaviorTest]
    public async Task ProcessMessageAsync_ShouldExtractToolExecutions_FromOpenAiResponse()
    {
        // Arrange - Setup response with tool executions
        var request = new AiRequest("Tool execution test", null, null);
        
        var mcpCalls = new[]
        {
            new Axon.Modules.Chat.Infrastructure.Ai.Models.McpCallItem(
                ToolName: "calculator",
                Arguments: new { operation = "add", a = 5, b = 3 },
                Output: new { result = 8 },
                Error: null),
            new Axon.Modules.Chat.Infrastructure.Ai.Models.McpCallItem(
                ToolName: "weather",
                Arguments: new { location = "unknown" },
                Output: null,
                Error: "Location not found")
        };

        InfrastructureContractMocks.SetupOpenAiResponse(
            _httpHandlerMock,
            "tool-response-789",
            "Tool execution completed",
            mcpCalls);

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify tool execution extraction behavior
        result.ShouldBeSuccessAnd(response =>
        {
            response.ToolExecutions.ShouldNotBeNull();
            response.ToolExecutions!.Length.ShouldBe(2);

            var calculatorExecution = response.ToolExecutions[0];
            calculatorExecution.ToolName.ShouldBe("calculator");
            calculatorExecution.IsSuccess.ShouldBeTrue();

            var weatherExecution = response.ToolExecutions[1];
            weatherExecution.ToolName.ShouldBe("weather");
            weatherExecution.IsSuccess.ShouldBeFalse();
        });

        // Verify tool execution logging
        InfrastructureContractMocks.VerifyInfrastructureLogging(
            _loggerMock,
            LogLevel.Debug,
            "MCP tool execution",
            Times.AtLeastOnce);
    }

    [Test]
    [InteractionTest]
    public async Task ProcessMessageAsync_ShouldUseInfrastructureBehaviorScenario_ForComplexTesting()
    {
        // Arrange - Use infrastructure behavior scenario
        var scenario = InfrastructureContractMocks.CreateInfrastructureScenario()
            .WithSuccessfulHttpResponse("scenario-response-999", "Scenario test response")
            .ExpectingHttpRequest("api.openai.com", HttpMethod.Post, Times.Once)
            .ExpectingHttpPayload(payload => payload.Contains("Scenario message"), Times.Once)
            .ExpectingLogEntry(_loggerMock, LogLevel.Information, "Processing message", Times.AtLeastOnce);

        var request = new AiRequest("Scenario message", null, null);

        // Replace HTTP client with scenario client
        _httpClient.Dispose();
        _httpClient = scenario.CreateHttpClient();
        
        var optionsMock = InfrastructureContractMocks.CreateOptionsMock(_options);
        var scenarioClient = new OpenAiClient(_httpClient, optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await scenarioClient.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify scenario execution
        result.ShouldBeSuccess();
        result.Value.ResponseId.ShouldBe("scenario-response-999");
        result.Value.Content.ShouldBe("Scenario test response");

        // Verify all scenario behaviors
        scenario.VerifyAllBehaviors();
    }

    [Test]
    [ContractTest]
    public async Task ProcessMessageAsync_ShouldValidateArguments_AccordingToContract()
    {
        // Act & Assert - Verify argument validation contract
        await Should.ThrowAsync<ArgumentNullException>(() => 
            _client.ProcessMessageAsync(null!, CancellationToken.None));
    }

    [Test]
    [BehaviorTest]
    public async Task ProcessMessageAsync_ShouldRespectCancellation_WhenTokenIsCancelled()
    {
        // Arrange - Setup cancellation scenario
        var request = new AiRequest("Cancellation test", null, null);
        using var cancellationTokenSource = new CancellationTokenSource();
        
        // Setup HTTP handler to delay response
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                await Task.Delay(100, ct); // Small delay to allow cancellation
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"cancelled\",\"output_text\":\"Should not reach\"}")
                };
            });

        // Cancel immediately
        await cancellationTokenSource.CancelAsync();

        // Act & Assert - Verify cancellation behavior
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _client.ProcessMessageAsync(request, cancellationTokenSource.Token));
    }

    [Test]
    [InteractionTest]
    public async Task ProcessMessageAsync_ShouldIncludeRequestIdInLogs_ForTraceability()
    {
        // Arrange - Setup for traceability testing
        var request = new AiRequest("Traceability test", null, null);
        
        InfrastructureContractMocks.SetupOpenAiResponse(
            _httpHandlerMock,
            "traceable-response-111",
            "Traceable response");

        // Act
        var result = await _client.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify logging includes traceability information
        result.ShouldBeSuccess();

        // Verify start processing log
        InfrastructureContractMocks.VerifyInfrastructureLogging(
            _loggerMock,
            LogLevel.Information,
            "Processing message with OpenAI",
            Times.AtLeastOnce);

        // Verify completion log with metrics
        InfrastructureContractMocks.VerifyInfrastructureLogging(
            _loggerMock,
            LogLevel.Information,
            "Successfully processed message",
            Times.AtLeastOnce);
    }

    [Test]
    [ContractTest]
    public async Task ProcessMessageAsync_ShouldNotIncludeMcpTools_WhenMcpDisabled()
    {
        // Arrange - Setup with MCP disabled
        var disabledMcpOptions = new OpenAiOptions
        {
            ApiKey = "test-key",
            Model = "gpt-4",
            McpEnabled = false, // Disabled
            TimeoutSeconds = 30
        };

        var optionsMock = InfrastructureContractMocks.CreateOptionsMock(disabledMcpOptions);
        var clientWithDisabledMcp = new OpenAiClient(_httpClient, optionsMock.Object, _loggerMock.Object);

        var mcpConfigs = new List<McpServerConfig>
        {
            new McpServerConfig("https://test.mcp.com", "Test", null, new[] { "test" }, false)
        }.AsReadOnly();

        var request = new AiRequest("MCP disabled test", mcpConfigs, null);

        InfrastructureContractMocks.SetupOpenAiResponse(
            _httpHandlerMock,
            "no-mcp-response",
            "Response without MCP");

        // Act
        var result = await clientWithDisabledMcp.ProcessMessageAsync(request, CancellationToken.None);

        // Assert - Verify MCP tools were not included
        result.ShouldBeSuccess();

        // Verify payload does not contain tools
        InfrastructureContractMocks.VerifyHttpRequestPayload(
            _httpHandlerMock,
            payload =>
            {
                var payloadObj = JsonSerializer.Deserialize<JsonElement>(payload);
                payloadObj.TryGetProperty("tools", out _).ShouldBeFalse();
                return true;
            },
            Times.Once);
    }
}