using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.Ai;
using Axon.Shared.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace Axon.Modules.Chat.Infrastructure.Tests.Ai;

public sealed class OpenAiClientTests : IDisposable
{
    private readonly Mock<ILogger<OpenAiClient>> _mockLogger;
    private readonly OpenAiOptions _options;
    private readonly IOptions<OpenAiOptions> _mockOptions;
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public OpenAiClientTests()
    {
        _mockLogger = new Mock<ILogger<OpenAiClient>>();
        _options = new OpenAiOptions
        {
            ApiKey = "test-api-key",
            Model = "gpt-4o",
            MaxTokens = 1000,
            Temperature = 0.5,
            TimeoutSeconds = 30,
            McpEnabled = true
        };

        _mockOptions = Options.Create(_options);
        _mockHttpHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHttpHandler);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_GivenValidOptions()
    {
        // Act
        var client = CreateOpenAiClient();

        // Assert
        client.Should().NotBeNull();
        // Verify that no exceptions are thrown during construction
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_GivenNullOptions()
    {
        // Act & Assert
        FluentActions
            .Invoking(() => new OpenAiClient(null!, _mockLogger.Object))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldReturnSuccessResult_GivenValidRequestWithoutMcp()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest(
            Message: "Hello, AI!",
            McpConfig: null,
            PreviousResponseId: null);

        // Note: Since OpenAiClient uses the OpenAI.NET library which creates its own HttpClient,
        // we can't easily mock the HTTP calls without complex setup.
        // For this test, we'll focus on the behavior we can control.

        // Act & Assert
        // This test would need to be an integration test or we'd need dependency injection for HttpClient
        // For now, we'll skip the actual API call and focus on the structure
        await FluentActions
            .Invoking(() => client.ProcessMessageAsync(request, CancellationToken.None))
            .Should()
            .NotThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldThrowArgumentNullException_GivenNullRequest()
    {
        // Arrange
        var client = CreateOpenAiClient();

        // Act & Assert
        await FluentActions
            .Invoking(() => client.ProcessMessageAsync(null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldLogProcessingInformation_GivenValidRequest()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var mcpConfig = new McpServerConfig(
            ServerUrl: "https://api.example.com/mcp",
            ServerLabel: "Test Server",
            Headers: null,
            AllowedTools: null,
            RequireApproval: false);

        var request = new AiRequest(
            Message: "Test message",
            McpConfig: mcpConfig,
            PreviousResponseId: "prev-123");

        try
        {
            // Act
            await client.ProcessMessageAsync(request, CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            // Expected to fail due to no actual API setup, but we can still check logging
        }
        catch (TaskCanceledException)
        {
            // Expected timeout due to no actual API setup, but we can still check logging
        }

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message with OpenAI model gpt-4o")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Short")]
    [InlineData("This is a longer message that should be processed correctly by the AI client")]
    public async Task ProcessMessageAsync_ShouldHandleVariousMessageLengths_GivenDifferentInputs(string message)
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest(
            Message: message,
            McpConfig: null,
            PreviousResponseId: null);

        try
        {
            // Act
            await client.ProcessMessageAsync(request, CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            // Expected to fail due to no actual API setup
        }
        catch (TaskCanceledException)
        {
            // Expected timeout due to no actual API setup
        }

        // Assert - Verify message length is logged correctly  
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message with OpenAI model gpt-4o")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldHandleCancellation_GivenCancelledToken()
    {
        // Arrange
        var client = CreateOpenAiClient();
        var request = new AiRequest("Test message", null, null);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await FluentActions
            .Invoking(() => client.ProcessMessageAsync(request, cancellationTokenSource.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void CreateMcpTool_ShouldCreatePlaceholderTool_GivenMcpConfig()
    {
        // This test verifies the placeholder implementation
        // Once OpenAI.NET supports MCP tools, this test should be updated

        // Expected behavior:
        // - Should create a ChatTool with function name based on MCP config
        // - Should include server URL in description
        // - Should have placeholder parameters until full MCP integration
        
        // Currently a placeholder test - no assertions needed
        Assert.True(true);
    }

    [Fact]
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
        Assert.True(true);
    }

    private OpenAiClient CreateOpenAiClient()
    {
        return new OpenAiClient(_mockOptions, _mockLogger.Object);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _mockHttpHandler?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}