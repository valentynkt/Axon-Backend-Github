using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Application.Tests.Extensions;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Extensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Application.Tests.Commands.AppendUserMessage;

[TestFixture]
public class AppendUserMessageHandlerTests : CommandHandlerTestBase<AppendUserMessageCommand, ProcessMessageResponse, AppendUserMessageHandler>
{
    private IConversationRepository _mockRepository = null!;
    private IUserAuthenticationService _mockAuthService = null!;
    private IMessageProcessingOrchestrator _mockOrchestrator = null!;
    private TimeProvider _mockTimeProvider = null!;
    private ILogger<AppendUserMessageHandler> _mockLogger = null!;

    protected override AppendUserMessageHandler CreateHandler()
    {
        return new AppendUserMessageHandler(
            _mockRepository,
            _mockAuthService,
            _mockOrchestrator,
            _mockTimeProvider,
            _mockLogger);
    }

    protected override void ConfigureHandlerDependencies()
    {
        _mockRepository = Substitute.For<IConversationRepository>();
        _mockAuthService = Substitute.For<IUserAuthenticationService>();
        _mockOrchestrator = Substitute.For<IMessageProcessingOrchestrator>();
        _mockTimeProvider = Substitute.For<TimeProvider>();
        _mockLogger = Substitute.For<ILogger<AppendUserMessageHandler>>();
    }

    [TearDown]
    public new void TearDown()
    {
        if (_mockRepository is IDisposable disposableRepository)
            disposableRepository.Dispose();
    }

    protected override AppendUserMessageCommand CreateValidCommand()
    {
        return CommandTestDataBuilder.AppendUserMessage()
            .WithValidData()
            .Build();
    }

    protected override AppendUserMessageCommand CreateInvalidCommand()
    {
        return CommandTestDataBuilder.AppendUserMessage()
            .WithInvalidContent()
            .Build();
    }

    #region Critical Path Tests (80/20 Rule)

    [TestCaseSource(nameof(GetValidCommandScenarios))]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithProcessMessageResponse(
        AppendUserMessageCommand command, 
        string _)
    {
        // Arrange
        var userId = UserId.New();
        var conversation = ChatDomainTestFactory.Conversations.CreateWithOwner(userId);
        var expectedResponse = new ProcessMessageResponse(
            command.ConversationId,
            MessageId.New(),
            MessageId.New(),
            MessageContent.Create("Assistant response").Value);

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(userId));
        _mockRepository.GetByIdAsync(command.ConversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);
        _mockOrchestrator.ProcessUserMessageAsync(
            Arg.Any<Conversation>(),
            Arg.Any<MessageContent>(),
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success<ProcessMessageResponse, Error>(expectedResponse));

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ConversationId.ShouldBe(command.ConversationId);
        result.Value.ShouldNotBeNull();
        
        await _mockRepository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await _mockRepository.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _mockOrchestrator.Received(1).ProcessUserMessageAsync(
            conversation,
            command.Content,
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithUnauthenticatedUser_ShouldReturnAuthenticationError()
    {
        // Arrange
        var command = CreateValidCommand();
        var authError = Error.Unauthorized("User not authenticated");

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Failure<UserId, Error>(authError));

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Unauthorized);
        await _mockRepository.DidNotReceive().GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>());
    }

    [TestCaseSource(nameof(GetConversationValidationScenarios))]
    public async Task Handle_WithConversationValidationFailures_ShouldReturnAppropriateError(
        Conversation? conversation,
        Error expectedError,
        string _)
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = UserId.New();

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(userId));
        _mockRepository.GetByIdAsync(command.ConversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(expectedError.Type);
        await _mockOrchestrator.DidNotReceive().ProcessUserMessageAsync(
            Arg.Any<Conversation>(),
            Arg.Any<MessageContent>(),
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithDomainRuleViolation_ShouldReturnDomainError()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = UserId.New();

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(userId));

        // Create a conversation that will simulate domain rule violation
        // by creating it in a state where message append should fail
        var conversationWithMaxMessages = ChatDomainTestFactory.Conversations.CreateWithOwner(userId);
        
        // Add enough messages to trigger the ConversationCanAcceptMoreMessagesRule
        for (int i = 0; i < 100; i++) // Assuming there's a limit
        {
            try
            {
                conversationWithMaxMessages.AppendUserMessageToConversation(
                    MessageContent.Create($"Test message {i}").Value,
                    _mockTimeProvider);
            }
            catch
            {
                // Stop when we can't add more messages
                break;
            }
        }

        _mockRepository.GetByIdAsync(command.ConversationId, Arg.Any<CancellationToken>())
            .Returns(conversationWithMaxMessages);

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.BusinessRule);
        
        // Verify repository methods were called appropriately
        await _mockRepository.Received(1).GetByIdAsync(command.ConversationId, Arg.Any<CancellationToken>());
        
        // Should not call orchestrator when domain rule fails
        await _mockOrchestrator.DidNotReceive().ProcessUserMessageAsync(
            Arg.Any<Conversation>(),
            Arg.Any<MessageContent>(),
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_WithOrchestratorFailure_ShouldReturnOrchestratorError()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = UserId.New();
        var conversation = ChatDomainTestFactory.Conversations.CreateWithOwner(userId);
        var orchestratorError = Error.Failure("AI processing failed", "AI_PROCESSING_ERROR");

        _mockAuthService.GetAuthenticatedUserId()
            .Returns(Result.Success<UserId, Error>(userId));
        _mockRepository.GetByIdAsync(command.ConversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);
        _mockOrchestrator.ProcessUserMessageAsync(
            Arg.Any<Conversation>(),
            Arg.Any<MessageContent>(),
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ProcessMessageResponse, Error>(orchestratorError));

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("AI_PROCESSING_ERROR");
        
        await _mockRepository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await _mockRepository.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidCommandScenarios()
    {
        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage().WithValidData().Build(),
            "Standard valid command")
            .SetName("ValidCommand_StandardMessage");

        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage().WithLongContent().Build(),
            "Valid command with long content")
            .SetName("ValidCommand_LongMessage");

        yield return new TestCaseData(
            CommandTestDataBuilder.AppendUserMessage()
                .WithContent("What is the meaning of life?")
                .Build(),
            "Valid command with question")
            .SetName("ValidCommand_QuestionMessage");
    }

    private static IEnumerable<TestCaseData> GetConversationValidationScenarios()
    {
        var wrongUserId = UserId.New();

        yield return new TestCaseData(
            null,
            Error.NotFound("Conversation not found"),
            "Non-existent conversation")
            .SetName("ConversationValidation_NotFound");

        yield return new TestCaseData(
            ChatDomainTestFactory.Conversations.CreateWithOwner(wrongUserId),
            Error.Forbidden("Access denied to conversation"),
            "Access denied to conversation")
            .SetName("ConversationValidation_AccessDenied");
    }

    #endregion

    protected override async Task AssertCommandSideEffects(AppendUserMessageCommand command, ProcessMessageResponse result)
    {
        // Verify the conversation was updated and saved
        await _mockRepository.Received(1).UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await _mockRepository.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Verify orchestrator was called with correct parameters
        await _mockOrchestrator.Received(1).ProcessUserMessageAsync(
            Arg.Any<Conversation>(),
            command.Content,
            Arg.Any<MessageId>(),
            Arg.Any<CancellationToken>());

        // Verify response structure
        result.ConversationId.ShouldBe(command.ConversationId);
        result.UserMessageId.ShouldNotBe(default);
        result.AssistantMessageId.ShouldNotBe(default);
        result.AssistantMessage.Value.ShouldNotBeNullOrEmpty();
    }
}