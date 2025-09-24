using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using Axon.Modules.Chat.Application.Services.Orchestration;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Axon.Modules.Chat.Domain.Tests.Extensions;

namespace Axon.Modules.Chat.Application.Tests.Services.Orchestration;

[TestFixture]
public class MessageProcessingOrchestratorTests : ApplicationTestBase
{
#pragma warning disable NUnit1032 // NSubstitute mocks don't require disposal
    private IConversationRepository _mockRepository = null!;
#pragma warning restore NUnit1032
    private IMcpServerResolutionService _mockMcpResolutionService = null!;
    private IAiProcessingService _mockAiProcessingService = null!;
    private IChatTelemetry _mockTelemetry = null!;
    private ILogger<MessageProcessingOrchestrator> _mockLogger = null!;
    private FakeTimeProvider _timeProvider = null!;
    private MessageProcessingOrchestrator _orchestrator = null!;

    // Test data
    private Conversation _testConversation = null!;
    private MessageContent _testUserMessage;
    private MessageId _testUserMessageId;
    private AiResponseId _testAiResponseId;
    private MessageContent _testAssistantContent;
    private McpServerConfig[] _testMcpConfigs = null!;

    protected override void OnSetUp()
    {
        // Setup test data FIRST before creating mocks
        SetupTestData();

        // Create mocks
        _mockRepository = Substitute.For<IConversationRepository>();
        _mockMcpResolutionService = Substitute.For<IMcpServerResolutionService>();
        _mockAiProcessingService = Substitute.For<IAiProcessingService>();
        _mockTelemetry = Substitute.For<IChatTelemetry>();
        _mockLogger = Substitute.For<ILogger<MessageProcessingOrchestrator>>();
        _timeProvider = new FakeTimeProvider(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        // Setup mock behaviors after test data is initialized
        SetupDefaultMockBehaviors();

        // Create orchestrator
        _orchestrator = new MessageProcessingOrchestrator(
            _mockRepository,
            _mockMcpResolutionService,
            _mockAiProcessingService,
            _timeProvider,
            _mockLogger,
            _mockTelemetry);
    }

    protected override void OnTearDown()
    {
        // NSubstitute mocks don't require disposal, but satisfying analyzer
        base.OnTearDown();
    }

    #region Happy Path Tests

    [Test]
    public async Task ProcessUserMessageAsync_HappyPath_ShouldCompleteSuccessfully()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Setup AI processing success for happy path
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        // Setup repository operations success
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<Conversation>(0)));
        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeSuccess();
        var response = result.Value;

        response.ConversationId.ShouldBe(_testConversation.Id);
        response.UserMessageId.ShouldBe(_testUserMessageId);
        response.AssistantMessageId.ShouldNotBe(default(MessageId));
        response.AssistantMessage.ShouldBe(_testAssistantContent);

        // Verify method calls
        await _mockMcpResolutionService.Received(1).ResolveServersAsync(_testConversation.Id, cancellationToken);
        await _mockAiProcessingService.Received(1).ProcessMessageAsync(
            _testUserMessage,
            _testConversation.Id,
            null, // No previous AI response for new conversation
            _testMcpConfigs,
            cancellationToken);
        await _mockRepository.Received(1).UpdateAsync(_testConversation, cancellationToken);
        await _mockRepository.UnitOfWork.Received(1).SaveChangesAsync(cancellationToken);

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: true);
    }

    [Test]
    public async Task ProcessUserMessageAsync_WithPreviousAiResponse_ShouldPassPreviousResponseId()
    {
        // Arrange: Add previous AI response to conversation
        var previousAiResponseId = CreateAiResponseId();
        var assistantContent = MessageContent.Create("Previous assistant response").Value;
        _testConversation.AppendAssistantResponseToConversation(assistantContent, previousAiResponseId, _timeProvider);

        var cancellationToken = CancellationToken.None;

        // Setup AI processing success
        var newAssistantContent = MessageContent.Create("New AI assistant response").Value;
        var newAiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, previousAiResponseId, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(newAssistantContent, newAiResponseId, TimeSpan.FromMilliseconds(100))));

        // Setup repository operations success
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<Conversation>(0)));
        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeSuccess();

        await _mockAiProcessingService.Received(1).ProcessMessageAsync(
            _testUserMessage,
            _testConversation.Id,
            previousAiResponseId, // Should pass the previous AI response ID
            _testMcpConfigs,
            cancellationToken);
    }

    #endregion

    #region MCP Resolution Failure Tests

    [Test]
    public async Task ProcessUserMessageAsync_McpResolutionFails_ShouldReturnFailure()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockMcpResolutionService
            .ResolveServersAsync(_testConversation.Id, cancellationToken)
            .ThrowsAsync(new InvalidOperationException("MCP resolution failed"));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);

        // Should not proceed to AI processing
        await _mockAiProcessingService.DidNotReceive().ProcessMessageAsync(
            Arg.Is<MessageContent>(m => true),
            Arg.Any<ConversationId>(),
            Arg.Any<AiResponseId?>(),
            Arg.Any<McpServerConfig[]>(),
            Arg.Any<CancellationToken>());

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    #endregion

    #region AI Processing Failure Tests

    [Test]
    public async Task ProcessUserMessageAsync_AiProcessingFails_ShouldReturnFailure()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var aiError = Error.External("AI service failed", "AI_SERVICE_ERROR");

        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Failure<AiProcessingResult, Error>(aiError));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.ShouldBe(aiError);

        // Should not save changes
        await _mockRepository.DidNotReceive().UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await _mockRepository.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    [Test]
    public async Task ProcessUserMessageAsync_AiProcessingThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .ThrowsAsync(new HttpRequestException("AI service timeout"));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Code.ShouldBe(ChatDomainErrors.Processing.UnexpectedErrorCode);

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    #endregion

    #region Message Append Failure Tests

    [Test]
    public async Task ProcessUserMessageAsync_ConversationAppendFails_ShouldReturnFailure()
    {
        // Arrange: Create a completed conversation that can't accept more messages
        var completedConversation = CreateCompletedConversation();
        var cancellationToken = CancellationToken.None;

        // Setup AI processing success (needed to reach conversation append phase)
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, completedConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            completedConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);

        // Should not save changes when append fails
        await _mockRepository.DidNotReceive().UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await _mockRepository.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        _mockTelemetry.Received(1).TrackMessageProcessed(
            completedConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    #endregion

    #region Persistence Failure Tests

    [Test]
    public async Task ProcessUserMessageAsync_RepositoryUpdateFails_ShouldThrowException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Setup AI processing success (needed to reach repository operations)
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeFailure();
        result.Error.Code.ShouldBe(ChatDomainErrors.Processing.UnexpectedErrorCode);

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    [Test]
    public async Task ProcessUserMessageAsync_SaveChangesFails_ShouldThrowException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Setup AI processing success (needed to reach repository operations)
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Transaction failed"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.ProcessUserMessageAsync(
                _testConversation,
                _testUserMessage,
                _testUserMessageId,
                cancellationToken));

        exception.Message.ShouldBe("Transaction failed");

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Any<TimeSpan>(),
            success: false);
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ProcessUserMessageAsync_CancellationRequested_ShouldThrowOperationCancelledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancellationToken = cancellationTokenSource.Token;

        // Setup AI processing success (needed to reach the cancellation point)
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _orchestrator.ProcessUserMessageAsync(
                _testConversation,
                _testUserMessage,
                _testUserMessageId,
                cancellationToken));
    }

    [Test]
    public async Task ProcessUserMessageAsync_CancellationDuringAiProcessing_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _orchestrator.ProcessUserMessageAsync(
                _testConversation,
                _testUserMessage,
                _testUserMessageId,
                cancellationToken));
    }

    #endregion

    #region Performance and Telemetry Tests

    [Test]
    public async Task ProcessUserMessageAsync_SuccessfulProcessing_ShouldTrackPerformanceMetrics()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Setup AI processing success
        var assistantContent = MessageContent.Create("AI assistant response").Value;
        var aiResponseId = CreateAiResponseId();
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(Result.Success<AiProcessingResult, Error>(
                new AiProcessingResult(assistantContent, aiResponseId, TimeSpan.FromMilliseconds(100))));

        // Setup repository operations success
        _mockRepository
            .UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<Conversation>(0)));
        _mockRepository.UnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeSuccess();

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Is<TimeSpan>(ts => ts >= TimeSpan.Zero),
            success: true);
    }

    [Test]
    public async Task ProcessUserMessageAsync_LongRunningProcess_ShouldTrackElapsedTime()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Simulate delay in AI processing
        _mockAiProcessingService
            .ProcessMessageAsync(_testUserMessage, _testConversation.Id, null, _testMcpConfigs, cancellationToken)
            .Returns(async callInfo =>
            {
                await Task.Delay(100, cancellationToken); // Simulate processing time
                return Result.Success<AiProcessingResult, Error>(
                    new AiProcessingResult(_testAssistantContent, _testAiResponseId, TimeSpan.FromMilliseconds(100)));
            });

        // Act
        var result = await _orchestrator.ProcessUserMessageAsync(
            _testConversation,
            _testUserMessage,
            _testUserMessageId,
            cancellationToken);

        // Assert
        result.ShouldBeSuccess();

        _mockTelemetry.Received(1).TrackMessageProcessed(
            _testConversation.Id.Value,
            Arg.Is<TimeSpan>(ts => ts.TotalMilliseconds >= 100),
            success: true);
    }

    #endregion

    #region Helper Methods

    private void SetupTestData()
    {
        var ownerId = CreateAxonUserId();
        _testConversation = CreateConversationBuilder()
            .WithOwner(ownerId)
            .WithUserMessage("Test user message")
            .Build();

        _testUserMessage = MessageContent.Create("New user message").Value;
        _testUserMessageId = MessageId.New();
        _testAiResponseId = CreateAiResponseId();
        _testAssistantContent = MessageContent.Create("AI assistant response").Value;
        _testMcpConfigs = new[]
        {
            new McpServerConfig("http://test.com", "test-server")
        };
    }

    private void SetupDefaultMockBehaviors()
    {
        // MCP resolution returns test configs
        _mockMcpResolutionService
            .ResolveServersAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(_testMcpConfigs);

        // No default AI processing setup - each test will set up its own specific behavior

        // No default repository setup - each test will set up its own specific behavior when needed
    }

    private static Conversation CreateCompletedConversation()
    {
        var ownerId = CreateAxonUserId();
        var conversation = CreateConversationBuilder()
            .WithOwner(ownerId)
            .WithUserMessage("Test message")
            .ThatShouldBeCompleted()
            .Build();

        return conversation;
    }

    #endregion
}