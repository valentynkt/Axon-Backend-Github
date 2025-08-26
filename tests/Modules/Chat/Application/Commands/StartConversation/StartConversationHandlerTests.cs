using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Tests.Builders;
using Axon.Modules.Chat.Application.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Extensions;

namespace Axon.Modules.Chat.Application.Tests.Commands.StartConversation;

[TestFixture]
public class StartConversationHandlerTests : CommandHandlerTestBase<StartConversationCommand, ProcessMessageResponse, StartConversationHandler>
{
    protected override StartConversationHandler CreateHandler()
    {
        return new StartConversationHandler(
            MockCurrentUserService,
            MockRepository,
            MockOrchestrator,
            MockTimeProvider,
            MockHandlerLogger);
    }

    protected override void ConfigureHandlerDependencies()
    {
        // Handler-specific setup for StartConversation scenarios
        // Setup repository to handle AddAsync instead of GetByIdAsync
        MockRepository.AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult(args.ArgAt<Conversation>(0)));
    }


    protected override StartConversationCommand CreateValidCommand()
    {
        return CommandTestDataBuilder.StartConversation()
            .WithMessage("Hello, I need help with something.")
            .Build();
    }

    protected override StartConversationCommand CreateInvalidCommand()
    {
        // Create a command that will fail validation (empty message)
        return CommandTestDataBuilder.StartConversation()
            .WithMessage("")
            .Build();
    }

    #region Critical Path Tests

    [TestCaseSource(nameof(GetValidCommandScenarios))]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithProcessMessageResponse(
        StartConversationCommand command, 
        string _)
    {
        // Arrange
        var userId = CreateUserId();
        var conversationId = ConversationId.New();
        
        MockCurrentUserService.UserId
            .Returns(userId.Value.ToString());
        SetupOrchestratorSuccess(conversationId, "Assistant response to your message");

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        result.Value.AssistantMessage.Value.ShouldBe("Assistant response to your message");
        
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await MockUnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Verify orchestrator was called at least once (avoid Vogen issues)
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);
    }



    [Test]
    public async Task Handle_WithOrchestratorFailure_ShouldReturnOrchestratorError()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = CreateUserId();
        var orchestratorError = Error.Failure("AI processing failed", "AI_PROCESSING_ERROR");

        MockCurrentUserService.UserId
            .Returns(userId.Value.ToString());
        SetupOrchestratorFailure(orchestratorError);

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("AI processing failed");
        
        // Conversation should still be persisted before orchestrator is called
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await MockUnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);
    }

    [Test]
    public async Task Handle_WithRepositoryFailure_ShouldPropagateException()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = CreateUserId();

        MockCurrentUserService.UserId
            .Returns(userId.Value.ToString());
        MockRepository.AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Conversation>(new InvalidOperationException("Database connection failed")));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ExecuteCommand(command));
        
        exception.Message.ShouldContain("Database connection failed");
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(0);
    }

    #endregion

    #region Test Data Sources

    private static IEnumerable<TestCaseData> GetValidCommandScenarios()
    {
        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation().WithMessage("Hello, I need help.").Build(),
            "Standard valid command")
            .SetName("ValidCommand_StandardMessage");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation().WithQuestionMessage().Build(),
            "Valid command with question")
            .SetName("ValidCommand_QuestionMessage");

        yield return new TestCaseData(
            CommandTestDataBuilder.StartConversation().WithTechnicalMessage().Build(),
            "Valid command with technical content")
            .SetName("ValidCommand_TechnicalMessage");
    }

    #endregion

    protected override async Task AssertCommandSideEffects(StartConversationCommand command, ProcessMessageResponse result)
    {
        // Verify the conversation was created and saved (AddAsync, not UpdateAsync)
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        await MockUnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Verify orchestrator was called at least once (avoid Vogen issues)
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);

        // Verify response structure
        result.ConversationId.ShouldNotBe(default);
        result.UserMessageId.ShouldNotBe(default);
        result.AssistantMessageId.ShouldNotBe(default);
        result.AssistantMessage.Value.ShouldNotBeEmpty();
    }
}