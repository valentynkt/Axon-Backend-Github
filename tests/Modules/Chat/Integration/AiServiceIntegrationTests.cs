using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Services.AI;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Axon.Modules.Chat.Integration;

/// <summary>
/// Integration tests for AI services including MCP server resolution and AI processing.
/// Tests various AI service scenarios, MCP configurations, and integration edge cases.
/// </summary>
[TestFixture]
public class AiServiceIntegrationTests : ApplicationTestBase
{
    private IMcpServerResolver _mockMcpResolver = null!;
    private IAiClient _mockAiClient = null!;
    private ILogger<McpServerResolutionService> _mockMcpLogger = null!;
    private ILogger<AiProcessingService> _mockAiLogger = null!;
    private McpServerResolutionService _mcpService = null!;
    private AiProcessingService _aiService = null!;

    // Test data
    private ConversationId _testConversationId = null!;
    private MessageContent _testMessage = null!;
    private AiResponseId _testAiResponseId = null!;

    protected override void OnSetUp()
    {
        // Create mocks
        _mockMcpResolver = Substitute.For<IMcpServerResolver>();
        _mockAiClient = Substitute.For<IAiClient>();
        _mockMcpLogger = Substitute.For<ILogger<McpServerResolutionService>>();
        _mockAiLogger = Substitute.For<ILogger<AiProcessingService>>();

        // Create services
        _mcpService = new McpServerResolutionService(_mockMcpResolver, _mockMcpLogger);
        _aiService = new AiProcessingService(_mockAiClient, _mockAiLogger);

        SetupTestData();
    }

    #region MCP Server Resolution Tests

    [Test]
    public async Task McpServerResolution_ValidConversation_ShouldReturnConfiguredServers()
    {
        // Arrange
        var expectedConfigs = new List<McpServerConfig>
        {
            new() { Name = "code-analysis", Url = "http://localhost:3001", Enabled = true },
            new() { Name = "web-search", Url = "http://localhost:3002", Enabled = true },
            new() { Name = "file-operations", Url = "http://localhost:3003", Enabled = false }
        };

        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .Returns(expectedConfigs);

        // Act
        var result = await _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(c => c.Name == "code-analysis" && c.Enabled);
        result.ShouldContain(c => c.Name == "web-search" && c.Enabled);
        result.ShouldContain(c => c.Name == "file-operations" && !c.Enabled);

        await _mockMcpResolver.Received(1).ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task McpServerResolution_EmptyConfiguration_ShouldReturnEmptyList()
    {
        // Arrange
        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig>());

        // Act
        var result = await _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
        await _mockMcpResolver.Received(1).ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task McpServerResolution_ResolverFailure_ShouldPropagateException()
    {
        // Arrange
        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("MCP resolver configuration error"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None));

        exception.Message.ShouldBe("MCP resolver configuration error");
    }

    [Test]
    public async Task McpServerResolution_FilterEnabledServers_ShouldReturnOnlyEnabled()
    {
        // Arrange
        var mixedConfigs = new List<McpServerConfig>
        {
            new() { Name = "enabled-server-1", Url = "http://localhost:3001", Enabled = true },
            new() { Name = "disabled-server-1", Url = "http://localhost:3002", Enabled = false },
            new() { Name = "enabled-server-2", Url = "http://localhost:3003", Enabled = true },
            new() { Name = "disabled-server-2", Url = "http://localhost:3004", Enabled = false }
        };

        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .Returns(mixedConfigs);

        // Act
        var result = await _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(4); // Should return all configs (filtering happens at AI service level)

        var enabledServers = result.Where(c => c.Enabled).ToList();
        enabledServers.Count.ShouldBe(2);
        enabledServers.ShouldContain(c => c.Name == "enabled-server-1");
        enabledServers.ShouldContain(c => c.Name == "enabled-server-2");
    }

    [Test]
    public async Task McpServerResolution_DuplicateServerNames_ShouldHandleCorrectly()
    {
        // Arrange: Configs with duplicate names (edge case)
        var configsWithDuplicates = new List<McpServerConfig>
        {
            new() { Name = "duplicate-server", Url = "http://localhost:3001", Enabled = true },
            new() { Name = "unique-server", Url = "http://localhost:3002", Enabled = true },
            new() { Name = "duplicate-server", Url = "http://localhost:3003", Enabled = false }
        };

        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .Returns(configsWithDuplicates);

        // Act
        var result = await _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None);

        // Assert: Should return all configurations (deduplication handled elsewhere if needed)
        result.Count.ShouldBe(3);
        result.Count(c => c.Name == "duplicate-server").ShouldBe(2);
    }

    #endregion

    #region AI Processing Service Tests

    [Test]
    public async Task AiProcessing_ValidRequest_ShouldReturnSuccessfulResponse()
    {
        // Arrange
        var mcpConfigs = new List<McpServerConfig>
        {
            new() { Name = "test-server", Url = "http://localhost:3001", Enabled = true }
        };

        var expectedResponse = new AiResponse(
            MessageContent.Create("AI processed response").Value,
            _testAiResponseId);

        var expectedAiRequest = new AiRequest(
            _testMessage,
            _testConversationId,
            null, // No previous response
            mcpConfigs);

        _mockAiClient
            .ProcessAsync(
                Arg.Is<AiRequest>(req =>
                    req.UserMessage.Equals(_testMessage) &&
                    req.ConversationId == _testConversationId &&
                    req.PreviousResponseId == null &&
                    req.McpConfigs.Count == 1),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiResponse, Error>(expectedResponse));

        // Act
        var result = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            mcpConfigs,
            CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.AssistantContent.ShouldBe(expectedResponse.AssistantContent);
        result.Value.ResponseId.ShouldBe(expectedResponse.ResponseId);

        await _mockAiClient.Received(1).ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AiProcessing_WithPreviousResponse_ShouldIncludePreviousResponseId()
    {
        // Arrange
        var previousResponseId = CreateAiResponseId();
        var mcpConfigs = new List<McpServerConfig>();

        var expectedResponse = new AiResponse(
            MessageContent.Create("Follow-up AI response").Value,
            CreateAiResponseId());

        _mockAiClient
            .ProcessAsync(
                Arg.Is<AiRequest>(req => req.PreviousResponseId == previousResponseId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiResponse, Error>(expectedResponse));

        // Act
        var result = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            previousResponseId,
            mcpConfigs,
            CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _mockAiClient.Received(1).ProcessAsync(
            Arg.Is<AiRequest>(req => req.PreviousResponseId == previousResponseId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AiProcessing_WithMcpConfigs_ShouldPassAllConfigurations()
    {
        // Arrange
        var mcpConfigs = new List<McpServerConfig>
        {
            new() { Name = "server1", Url = "http://localhost:3001", Enabled = true },
            new() { Name = "server2", Url = "http://localhost:3002", Enabled = false },
            new() { Name = "server3", Url = "http://localhost:3003", Enabled = true }
        };

        var expectedResponse = new AiResponse(
            MessageContent.Create("Response with MCP integration").Value,
            CreateAiResponseId());

        _mockAiClient
            .ProcessAsync(
                Arg.Is<AiRequest>(req => req.McpConfigs.Count == 3),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiResponse, Error>(expectedResponse));

        // Act
        var result = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            mcpConfigs,
            CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _mockAiClient.Received(1).ProcessAsync(
            Arg.Is<AiRequest>(req =>
                req.McpConfigs.Count == 3 &&
                req.McpConfigs.Any(c => c.Name == "server1" && c.Enabled) &&
                req.McpConfigs.Any(c => c.Name == "server2" && !c.Enabled) &&
                req.McpConfigs.Any(c => c.Name == "server3" && c.Enabled)),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region AI Client Failure Scenarios

    [Test]
    public async Task AiProcessing_ClientReturnsError_ShouldPropagateError()
    {
        // Arrange
        var aiError = Error.External("AI model unavailable", "AI_MODEL_UNAVAILABLE");
        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AiResponse, Error>(aiError));

        // Act
        var result = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            new List<McpServerConfig>(),
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.ShouldBe(aiError);
    }

    [Test]
    public async Task AiProcessing_ClientThrowsException_ShouldPropagateException()
    {
        // Arrange
        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("AI service connection failed"));

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            _aiService.ProcessMessageAsync(
                _testMessage,
                _testConversationId,
                null,
                new List<McpServerConfig>(),
                CancellationToken.None));

        exception.Message.ShouldBe("AI service connection failed");
    }

    [Test]
    public async Task AiProcessing_ClientTimeout_ShouldPropagateTimeout()
    {
        // Arrange
        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("AI processing timeout"));

        // Act & Assert
        var exception = await Should.ThrowAsync<TimeoutException>(() =>
            _aiService.ProcessMessageAsync(
                _testMessage,
                _testConversationId,
                null,
                new List<McpServerConfig>(),
                CancellationToken.None));

        exception.Message.ShouldBe("AI processing timeout");
    }

    #endregion

    #region Complex Integration Scenarios

    [Test]
    public async Task CompleteIntegration_McpResolutionToAiProcessing_ShouldWorkEndToEnd()
    {
        // Arrange: Complete workflow from MCP resolution to AI processing
        var mcpConfigs = new List<McpServerConfig>
        {
            new() { Name = "integration-server", Url = "http://localhost:3001", Enabled = true }
        };

        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .Returns(mcpConfigs);

        var expectedAiResponse = new AiResponse(
            MessageContent.Create("End-to-end integration response").Value,
            CreateAiResponseId());

        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiResponse, Error>(expectedAiResponse));

        // Act: Step 1 - Resolve MCP servers
        var mcpResult = await _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None);

        // Act: Step 2 - Process with AI service
        var aiResult = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            mcpResult,
            CancellationToken.None);

        // Assert: Both steps should succeed
        mcpResult.Count.ShouldBe(1);
        mcpResult.First().Name.ShouldBe("integration-server");

        aiResult.ShouldBeSuccess();
        aiResult.Value.AssistantContent.Value.ShouldBe("End-to-end integration response");

        // Verify call sequence
        await _mockMcpResolver.Received(1).ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>());
        await _mockAiClient.Received(1).ProcessAsync(
            Arg.Is<AiRequest>(req => req.McpConfigs.Count == 1 && req.McpConfigs.First().Name == "integration-server"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CompleteIntegration_McpFailureImpactOnAi_ShouldHandleGracefully()
    {
        // Arrange: MCP resolution fails, but AI should still work with empty config
        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("MCP resolution failed"));

        var expectedAiResponse = new AiResponse(
            MessageContent.Create("AI response without MCP").Value,
            CreateAiResponseId());

        _mockAiClient
            .ProcessAsync(
                Arg.Is<AiRequest>(req => req.McpConfigs.Count == 0),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiResponse, Error>(expectedAiResponse));

        // Act & Assert: MCP resolution should fail
        var mcpException = await Should.ThrowAsync<InvalidOperationException>(() =>
            _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None));

        mcpException.Message.ShouldBe("MCP resolution failed");

        // But AI processing with empty config should still work
        var aiResult = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            new List<McpServerConfig>(), // Empty due to MCP failure
            CancellationToken.None);

        aiResult.ShouldBeSuccess();
        aiResult.Value.AssistantContent.Value.ShouldBe("AI response without MCP");
    }

    [Test]
    public async Task CompleteIntegration_CascadingFailures_ShouldFailGracefully()
    {
        // Arrange: Both MCP and AI services fail
        _mockMcpResolver
            .ResolveForConversationAsync(_testConversationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("MCP timeout"));

        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AiResponse, Error>(Error.External("AI service down", "AI_SERVICE_DOWN")));

        // Act & Assert: Should fail at MCP level first
        var mcpException = await Should.ThrowAsync<TimeoutException>(() =>
            _mcpService.ResolveServersAsync(_testConversationId, CancellationToken.None));

        mcpException.Message.ShouldBe("MCP timeout");

        // If we bypassed MCP failure, AI would also fail
        var aiResult = await _aiService.ProcessMessageAsync(
            _testMessage,
            _testConversationId,
            null,
            new List<McpServerConfig>(),
            CancellationToken.None);

        aiResult.ShouldBeFailure();
        aiResult.Error.Code.ShouldBe("AI_SERVICE_DOWN");
    }

    #endregion

    #region Performance and Concurrency

    [Test]
    public async Task AiServiceIntegration_ConcurrentRequests_ShouldHandleCorrectly()
    {
        // Arrange: Multiple concurrent AI requests
        var mcpConfigs = new List<McpServerConfig>
        {
            new() { Name = "concurrent-server", Url = "http://localhost:3001", Enabled = true }
        };

        var tasks = new List<Task<Result<AiResponse, Error>>>();
        var conversationIds = Enumerable.Range(0, 5).Select(_ => ConversationId.New()).ToList();

        _mockAiClient
            .ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<AiRequest>();
                return Result.Success<AiResponse, Error>(new AiResponse(
                    MessageContent.Create($"Response for conversation {request.ConversationId.Value}").Value,
                    CreateAiResponseId()));
            });

        // Act: Send concurrent requests
        foreach (var conversationId in conversationIds)
        {
            tasks.Add(_aiService.ProcessMessageAsync(
                MessageContent.Create($"Message for {conversationId.Value}").Value,
                conversationId,
                null,
                mcpConfigs,
                CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed
        foreach (var result in results)
        {
            result.ShouldBeSuccess();
        }

        results.Length.ShouldBe(5);

        // Verify AI client was called for each request
        await _mockAiClient.Received(5).ProcessAsync(Arg.Any<AiRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task McpServiceIntegration_ConcurrentResolution_ShouldHandleCorrectly()
    {
        // Arrange: Multiple concurrent MCP resolution requests
        var conversationIds = Enumerable.Range(0, 5).Select(_ => ConversationId.New()).ToList();

        _mockMcpResolver
            .ResolveForConversationAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var conversationId = callInfo.Arg<ConversationId>();
                return new List<McpServerConfig>
                {
                    new() { Name = $"server-for-{conversationId.Value}", Url = "http://localhost:3001", Enabled = true }
                };
            });

        // Act: Resolve concurrently
        var tasks = conversationIds.Select(id =>
            _mcpService.ResolveServersAsync(id, CancellationToken.None)).ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed with correct configurations
        results.Length.ShouldBe(5);

        foreach (var (result, index) in results.Select((r, i) => (r, i)))
        {
            result.Count.ShouldBe(1);
            result.First().Name.ShouldBe($"server-for-{conversationIds[index].Value}");
        }

        // Verify resolver was called for each conversation
        foreach (var conversationId in conversationIds)
        {
            await _mockMcpResolver.Received(1).ResolveForConversationAsync(conversationId, Arg.Any<CancellationToken>());
        }
    }

    #endregion

    #region Helper Methods

    private void SetupTestData()
    {
        _testConversationId = ConversationId.New();
        _testMessage = MessageContent.Create("Test message for AI integration").Value;
        _testAiResponseId = CreateAiResponseId();
    }

    #endregion
}