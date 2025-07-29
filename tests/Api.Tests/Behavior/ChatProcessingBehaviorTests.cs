using System.Net;
using System.Net.Http.Json;
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Shared.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

// Aliases to resolve type ambiguity
using ApiProcessMessageResponse = Axon.Api.Contracts.Chat.ProcessMessageResponse;

namespace Axon.Api.Tests.Behavior;

/// <summary>
/// End-to-end behavior tests for the Chat/Direct_MCP feature.
/// These tests validate complete user scenarios and business flows.
/// </summary>
public sealed class ChatProcessingBehaviorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatProcessingBehaviorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UserScenario_SimpleTextChat_ShouldProcessSuccessfully()
    {
        // Scenario: User sends a simple text message without MCP tools
        // Given: A user wants to have a basic conversation with AI
        // When: They send a text message through the API
        // Then: They receive a helpful response with a conversation ID

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var expectedResponse = new AiResponse(
            Content: "Hello! I'm here to help you with any questions or tasks you have. What would you like to know?",
            ResponseId: "response-001",
            ToolExecutions: null);

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "Hello, can you help me?",
            McpServer: null,
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Contain("Hello!");
        content.ConversationId.Should().Be("response-001");
        content.ToolExecutions.Should().BeNull();

        // Verify the AI client received the correct request
        mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req =>
                req.Message == "Hello, can you help me?" &&
                req.McpConfig == null &&
                req.PreviousResponseId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserScenario_ContinuingConversation_ShouldMaintainContext()
    {
        // Scenario: User continues an existing conversation
        // Given: A user has an ongoing conversation
        // When: They send a follow-up message with conversation ID
        // Then: The AI receives the conversation context

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var expectedResponse = new AiResponse(
            Content: "Based on our previous conversation, I can provide more specific help.",
            ResponseId: "response-002",
            ToolExecutions: null);

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "Can you elaborate on that?",
            McpServer: null,
            ConversationId: "previous-conversation-123");

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.ConversationId.Should().Be("response-002");

        // Verify context is passed to AI client
        mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req =>
                req.Message == "Can you elaborate on that?" &&
                req.PreviousResponseId == "previous-conversation-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserScenario_McpToolIntegration_ShouldExecuteTools()
    {
        // Scenario: User requests information that requires MCP tool execution
        // Given: A user wants weather information and provides MCP server details
        // When: They send a message with MCP configuration
        // Then: The system executes MCP tools and returns enriched results

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var toolExecutions = new[]
        {
            ToolExecution.Success(
                "get_weather",
                "{\"location\":\"New York\",\"units\":\"fahrenheit\"}",
                "{\"temperature\":75,\"condition\":\"sunny\",\"humidity\":45}",
                TimeSpan.FromMilliseconds(850))
        };

        var expectedResponse = new AiResponse(
            Content: "Based on the weather data, it's currently 75°F and sunny in New York with 45% humidity. Great day to go outside!",
            ResponseId: "response-003",
            ToolExecutions: toolExecutions);

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "What's the weather like in New York?",
            McpServer: new McpServerRequest(
                ServerUrl: "https://weather.example.com/mcp",
                ServerLabel: null,
                Headers: new Dictionary<string, string> 
                { 
                    { "X-API-Key", "weather-api-key" } 
                },
                AllowedTools: new[] { "get_weather", "get_forecast" }),
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Contain("75°F");
        content.Response.Should().Contain("sunny");
        content.ToolExecutions.Should().HaveCount(1);
        content.ToolExecutions![0].ToolName.Should().Be("get_weather");
        content.ToolExecutions[0].Success.Should().BeTrue();
        content.ToolExecutions[0].DurationMs.Should().Be(850);

        // Verify MCP configuration is passed correctly
        mockAiClient.Verify(x => x.ProcessMessageAsync(
            It.Is<AiRequest>(req =>
                req.Message == "What's the weather like in New York?" &&
                req.McpConfig != null &&
                req.McpConfig.ServerUrl == "https://weather.example.com/mcp" &&
                req.McpConfig.Headers!["X-API-Key"] == "weather-api-key" &&
                req.McpConfig.AllowedTools!.Contains("get_weather")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserScenario_McpToolFailure_ShouldHandleGracefully()
    {
        // Scenario: MCP tool execution fails but conversation continues
        // Given: A user requests information via MCP tools
        // When: The MCP tool fails to execute
        // Then: The system reports the failure but provides helpful response

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var toolExecutions = new[]
        {
            ToolExecution.Failure(
                "get_stock_price",
                "{\"symbol\":\"AAPL\"}",
                "Stock market API is currently unavailable",
                TimeSpan.FromMilliseconds(2000))
        };

        var expectedResponse = new AiResponse(
            Content: "I'm sorry, but I couldn't retrieve the current stock price for AAPL as the stock market API is temporarily unavailable. Please try again later.",
            ResponseId: "response-004",
            ToolExecutions: toolExecutions);

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "What's the current price of AAPL stock?",
            McpServer: new McpServerRequest(
                ServerUrl: "https://finance.example.com/mcp",
                ServerLabel: null,
                Headers: null,
                AllowedTools: new[] { "get_stock_price" }),
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Contain("temporarily unavailable");
        content.ToolExecutions.Should().HaveCount(1);
        content.ToolExecutions![0].ToolName.Should().Be("get_stock_price");
        content.ToolExecutions[0].Success.Should().BeFalse();
        content.ToolExecutions[0].DurationMs.Should().Be(2000);
    }

    [Fact]
    public async Task UserScenario_InvalidMcpConfiguration_ShouldReturnValidationError()
    {
        // Scenario: User provides invalid MCP server configuration
        // Given: A user tries to use MCP tools with invalid configuration
        // When: They send a message with malformed MCP server URL
        // Then: The system returns a validation error

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var validationError = Error.Validation("MCP server URL must use HTTPS scheme for security");

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(validationError));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "Get me some data",
            McpServer: new McpServerRequest(
                ServerUrl: "http://insecure.example.com/mcp",  // HTTP instead of HTTPS
                ServerLabel: null,
                Headers: null,
                AllowedTools: new[] { "get_data" }),
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("HTTPS scheme");
        content.Should().Contain("security");
    }

    [Fact]
    public async Task UserScenario_AiServiceDown_ShouldReturnServiceError()
    {
        // Scenario: AI service is temporarily unavailable
        // Given: The external AI service is down
        // When: A user sends any message
        // Then: The system returns a service error with helpful message

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var serviceError = Error.ExternalService("OpenAI service is temporarily unavailable. Please try again later.");

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Failure(serviceError));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "Hello there!",
            McpServer: null,
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("temporarily unavailable");
        content.Should().Contain("External Service Error");
    }

    [Fact]
    public async Task UserScenario_MultipleToolExecution_ShouldHandleComplexRequests()
    {
        // Scenario: User request requires multiple MCP tool executions
        // Given: A user asks for information requiring multiple data sources
        // When: Multiple MCP tools need to be executed
        // Then: All tool results are aggregated in the response

        // Arrange
        var mockAiClient = new Mock<IAiClient>();
        var toolExecutions = new[]
        {
            ToolExecution.Success("get_weather", "{\"location\":\"SF\"}", "Sunny, 68°F", TimeSpan.FromMilliseconds(400)),
            ToolExecution.Success("get_traffic", "{\"route\":\"SF to LA\"}", "Heavy traffic, 6h drive", TimeSpan.FromMilliseconds(600)),
            ToolExecution.Success("get_hotels", "{\"city\":\"LA\"}", "Found 15 hotels available", TimeSpan.FromMilliseconds(800))
        };

        var expectedResponse = new AiResponse(
            Content: "For your trip from SF to LA: Weather in SF is sunny at 68°F. Traffic is heavy with a 6-hour drive expected. I found 15 hotels available in LA.",
            ResponseId: "response-005",
            ToolExecutions: toolExecutions);

        mockAiClient
            .Setup(x => x.ProcessMessageAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AiResponse>.Success(expectedResponse));

        var client = CreateClientWithMockedAi(mockAiClient.Object);

        var request = new ProcessMessageRequest(
            Message: "Plan my trip from San Francisco to Los Angeles tomorrow",
            McpServer: new McpServerRequest(
                ServerUrl: "https://travel.example.com/mcp",
                ServerLabel: null,
                Headers: new Dictionary<string, string> { { "API-Key", "travel-key" } },
                AllowedTools: new[] { "get_weather", "get_traffic", "get_hotels", "get_flights" }),
            ConversationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/process", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<ApiProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().Contain("trip");
        content.ToolExecutions.Should().HaveCount(3);
        
        // Verify all tools were executed
        var toolNames = content.ToolExecutions!.Select(t => t.ToolName).ToArray();
        toolNames.Should().Contain("get_weather");
        toolNames.Should().Contain("get_traffic");
        toolNames.Should().Contain("get_hotels");
        
        // All tools should have succeeded
        content.ToolExecutions.Should().OnlyContain(t => t.Success);
    }

    private HttpClient CreateClientWithMockedAi(IAiClient mockAiClient)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace IAiClient with mock
                var serviceDescriptor = services.Single(d => d.ServiceType == typeof(IAiClient));
                services.Remove(serviceDescriptor);
                services.AddSingleton(mockAiClient);
            });
        }).CreateClient();
    }
}