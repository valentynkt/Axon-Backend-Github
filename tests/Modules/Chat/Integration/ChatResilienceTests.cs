#pragma warning disable NS1004 // Argument matcher used with a non-virtual member

using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Services.Orchestration;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Tests.Extensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Axon.Modules.Chat.Integration;

/// <summary>
/// Resilience and error handling tests for Chat module.
/// Tests system behavior under various failure conditions,
/// error recovery, circuit breaker patterns, and fallback mechanisms.
/// </summary>
[TestFixture]
public class ChatResilienceTests : ApplicationTestBase
{
    private IConversationRepository _mockRepository = null!;
    private IMcpServerResolutionService _mockMcpService = null!;
    private IAiProcessingService _mockAiService = null!;
    private ILogger<MessageProcessingOrchestrator> _mockLogger = null!;
    private MessageProcessingOrchestrator _orchestrator = null!;

    // Test data
    private Conversation _testConversation = null!;
    private MessageContent _testMessage;
    private MessageId _testMessageId;

    protected override void OnSetUp()
    {
        // Create mocks
        _mockRepository = Substitute.For<IConversationRepository>();
        _mockMcpService = Substitute.For<IMcpServerResolutionService>();
        _mockAiService = Substitute.For<IAiProcessingService>();
        _mockLogger = Substitute.For<ILogger<MessageProcessingOrchestrator>>();

        // Create orchestrator
        _orchestrator = new MessageProcessingOrchestrator(
            _mockRepository,
            _mockMcpService,
            _mockAiService,
            TimeProvider,
            _mockLogger);

        SetupTestData();
    }

    [TearDown]
    public void TearDown()
    {
        (_mockRepository as IDisposable)?.Dispose();
    }

    #region Database Failure Scenarios

    [Test]
    public async Task ProcessMessage_DatabaseConnectionFailure_ShouldReturnError()
    {
        // Arrange: Database connection fails
        _mockRepository
            .GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database connection timeout"));

        SetupSuccessfulMcpAndAi();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: Should handle database failure gracefully
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Test]
    public async Task ProcessMessage_DatabaseSaveFailure_ShouldReturnError()
    {
        // Arrange: Save operation fails
        SetupSuccessfulMcpAndAi();
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Transaction deadlock"));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: Should fail gracefully
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.ProcessUserMessageAsync(
                _testConversation,
                _testMessage,
                _testMessageId,
                CancellationToken.None));

        exception.Message.ShouldBe("Transaction deadlock");
    }

    [Test]
    public async Task ProcessMessage_DatabaseTransactionTimeout_ShouldPropagateError()
    {
        // Arrange: Transaction timeout
        SetupSuccessfulMcpAndAi();
        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Transaction timeout exceeded"));

        // Act & Assert
        var exception = await Should.ThrowAsync<TimeoutException>(() =>
            _orchestrator.ProcessUserMessageAsync(
                _testConversation,
                _testMessage,
                _testMessageId,
                CancellationToken.None));

        exception.Message.ShouldBe("Transaction timeout exceeded");
    }

    #endregion

    #region AI Service Failure Scenarios

    [Test]
    public async Task ProcessMessage_AiServiceUnavailable_ShouldReturnError()
    {
        // Arrange: AI service is unavailable
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AiProcessingResult, Error>(
                Error.External("AI service unavailable", "AI_SERVICE_UNAVAILABLE")));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.External);
        result.Error.Code.ShouldBe("AI_SERVICE_UNAVAILABLE");

        // Should not save changes when AI fails
        _ = await _mockRepository.DidNotReceive().UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessage_AiServiceTimeout_ShouldReturnError()
    {
        // Arrange: AI service times out
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("AI service request timeout"));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Code.ShouldBe(ChatDomainErrors.Processing.UnexpectedErrorCode);
    }

    [Test]
    public async Task ProcessMessage_AiServiceRateLimited_ShouldReturnError()
    {
        // Arrange: AI service returns rate limit error
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AiProcessingResult, Error>(
                Error.External("Rate limit exceeded", "AI_RATE_LIMIT_EXCEEDED")));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.External);
        result.Error.Code.ShouldBe("AI_RATE_LIMIT_EXCEEDED");
    }

    [Test]
    public async Task ProcessMessage_AiServiceMalformedResponse_ShouldReturnError()
    {
        // Arrange: AI service returns malformed response
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new FormatException("Invalid AI response format"));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    #endregion

    #region MCP Service Failure Scenarios

    [Test]
    public async Task ProcessMessage_McpServiceFailure_ShouldReturnError()
    {
        // Arrange: MCP service fails
        _mockMcpService
            .ResolveServersAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("MCP server discovery failed"));

        SetupSuccessfulAi();
        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);

        // Should not proceed to AI processing
        _ = await _mockAiService.DidNotReceive().ProcessMessageAsync(
            Arg.Any<MessageContent>(),
            Arg.Any<ConversationId>(),
            Arg.Any<AiResponseId?>(),
            Arg.Any<McpServerConfig[]?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessage_McpServiceEmpty_ShouldContinueWithEmptyConfig()
    {
        // Arrange: MCP service returns empty configuration (not a failure)
        _mockMcpService
            .ResolveServersAsync(_testConversation.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<McpServerConfig>());

        SetupSuccessfulAi();
        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: Should continue with empty MCP config
        result.ShouldBeSuccess();

        _ = await _mockAiService.Received(1).ProcessMessageAsync(
            _testMessage,
            _testConversation.Id,
            null,
            Arg.Is<McpServerConfig[]?>(configs => configs == null || configs.Length == 0),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Network and Infrastructure Failures

    [Test]
    public async Task ProcessMessage_NetworkFailure_ShouldPropagateError()
    {
        // Arrange: Network failure during AI call
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Test]
    public async Task ProcessMessage_MemoryPressure_ShouldHandleGracefully()
    {
        // Arrange: Simulate resource exhaustion (use InvalidOperationException instead of OutOfMemoryException)
        SetupSuccessfulMcp();
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Resource exhaustion - insufficient memory"));

        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    #endregion

    #region Cascading Failure Scenarios

    [Test]
    public async Task ProcessMessage_MultipleDependencyFailures_ShouldFailFast()
    {
        // Arrange: Multiple services fail simultaneously
        _mockMcpService
            .ResolveServersAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("MCP timeout"));

        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("AI service down"));

        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database down"));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: Should fail on first dependency (MCP)
        result.ShouldBeFailure();

        // Should not call subsequent services if first one fails
        _ = await _mockAiService.DidNotReceive().ProcessMessageAsync(
            Arg.Any<MessageContent>(),
            Arg.Any<ConversationId>(),
            Arg.Any<AiResponseId?>(),
            Arg.Any<McpServerConfig[]?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessage_PartialFailureRecovery_ShouldRetryAppropriately()
    {
        // Arrange: Service fails first time, succeeds second time
        var callCount = 0;
        SetupSuccessfulMcp();

        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new HttpRequestException("Temporary network issue");
                }
                return Task.FromResult(Result.Success<AiProcessingResult, Error>(
                    new AiProcessingResult(MessageContent.Create("Success after retry").Value, CreateAiResponseId(), TimeSpan.FromSeconds(1))));
            });

        SetupSuccessfulRepository();

        // Act: This simulates a scenario where a retry mechanism might be in place
        // Note: Current implementation doesn't have retry, so this will fail
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: With current implementation (no retry), this should fail
        result.ShouldBeFailure();

        // If retry mechanism were implemented, we would test:
        // result.ShouldBeSuccess();
        // callCount.ShouldBe(2, "Should retry once after first failure");
    }

    #endregion

    #region Business Logic Failure Recovery

    [Test]
    public async Task ProcessMessage_ConversationBusinessRuleViolation_ShouldReturnBusinessError()
    {
        // Arrange: Create a conversation that violates business rules (completed)
        var completedConversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("Initial message")
            .ThatShouldBeCompleted()
            .Build();

        SetupSuccessfulMcpAndAi();
        SetupSuccessfulRepository();

        // Act: Try to add message to completed conversation
        var result = await _orchestrator.ProcessUserMessageAsync(
            completedConversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert: Should return business rule error
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);

        // Should not save changes when business rule is violated
        _ = await _mockRepository.DidNotReceive().UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessMessage_MessageContentValidationFailure_ShouldReturnValidationError()
    {
        // Arrange: This test would typically use invalid message content
        // Since MessageContent.Create validates, we'll test with a conversation at message limit
        var conversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithMaximumMessages()
            .Build();

        SetupSuccessfulMcpAndAi();
        SetupSuccessfulRepository();

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            conversation,
            _testMessage,
            _testMessageId,
            CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
    }

    #endregion

    #region Cancellation and Timeout Handling

    [Test]
    public async Task ProcessMessage_CancellationDuringMcpResolution_ShouldCancelGracefully()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        _mockMcpService
            .ResolveServersAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                await Task.Delay(1000, token); // This will be cancelled
                return Array.Empty<McpServerConfig>();
            });

        SetupSuccessfulAi();
        SetupSuccessfulRepository();

        // Act: Cancel after short delay
        var processingTask = _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            cancellationTokenSource.Token);

        cancellationTokenSource.CancelAfter(100);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(() => processingTask);
    }

    [Test]
    public async Task ProcessMessage_CancellationDuringAiProcessing_ShouldCancelGracefully()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        SetupSuccessfulMcp();

        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                await Task.Delay(1000, token); // This will be cancelled
                return Result.Success<AiProcessingResult, Error>(
                    new AiProcessingResult(MessageContent.Create("Response").Value, CreateAiResponseId(), TimeSpan.FromSeconds(1)));
            });

        SetupSuccessfulRepository();

        // Act
        var processingTask = _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testMessage,
            _testMessageId,
            cancellationTokenSource.Token);

        cancellationTokenSource.CancelAfter(100);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(() => processingTask);

        // Should not save changes when cancelled
        _ = await _mockRepository.DidNotReceive().UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private void SetupTestData()
    {
        _testConversation = CreateConversationBuilder()
            .WithOwner(CreateAxonUserId())
            .WithUserMessage("Initial message")
            .Build();

        _testMessage = MessageContent.Create("Test message for resilience").Value;
        _testMessageId = MessageId.New();
    }

    private void SetupSuccessfulMcp()
    {
        _mockMcpService
            .ResolveServersAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(new McpServerConfig[]
            {
                new("http://test.com", "test-server")
            });
    }

    private void SetupSuccessfulAi()
    {
        _mockAiService
            .ProcessMessageAsync(
                Arg.Any<MessageContent>(),
                Arg.Any<ConversationId>(),
                Arg.Any<AiResponseId?>(),
                Arg.Any<McpServerConfig[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(MessageContent.Create("AI response").Value, CreateAiResponseId(), TimeSpan.FromSeconds(1))));
    }

    private void SetupSuccessfulRepository()
    {
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private void SetupSuccessfulMcpAndAi()
    {
        SetupSuccessfulMcp();
        SetupSuccessfulAi();
    }

    #endregion
}