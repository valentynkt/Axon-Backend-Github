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
        // Not applicable for this command type - all parameters are validated value objects
        return CommandTestDataBuilder.StartConversation()
            .WithMessage("Not used")
            .Build();
    }
    

    #region Critical Path Tests

    [TestCaseSource(nameof(GetValidCommandScenarios))]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithProcessMessageResponse(
        StartConversationCommand command, 
        string _)
    {
        // Arrange
        var userId = CreateAxonUserId();
        var conversationId = ConversationId.New();
        
        MockCurrentUserService.AxonUserId
            .Returns(userId.Value.ToString());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(userId);
        SetupOrchestratorSuccess(conversationId, "Assistant response to your message");

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        result.Value.AssistantMessage.Value.ShouldBe("Assistant response to your message");

        // Verify conversation was added to repository (in-memory, not persisted yet)
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());

        // Verify orchestrator was called - orchestrator handles persistence atomically
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);
    }



    [Test]
    public async Task Handle_WithOrchestratorFailure_ShouldReturnOrchestratorError()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = CreateAxonUserId();
        var orchestratorError = Error.Failure("AI processing failed", "AI_PROCESSING_ERROR");

        MockCurrentUserService.AxonUserId
            .Returns(userId.Value.ToString());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(userId);
        SetupOrchestratorFailure(orchestratorError);

        // Act
        var result = await ExecuteCommand(command);

        // Assert
        result.ShouldFailWithErrorType(ErrorType.Internal);
        result.Error.Code.ShouldBe("AI processing failed");

        // Verify conversation was added to repository (but not persisted due to orchestrator failure)
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());

        // Verify orchestrator was called (and failed)
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);
    }

    [Test]
    public async Task Handle_WithRepositoryFailure_ShouldPropagateException()
    {
        // Arrange
        var command = CreateValidCommand();
        var userId = CreateAxonUserId();

        MockCurrentUserService.AxonUserId
            .Returns(userId.Value.ToString());
        MockCurrentUserService.GetAxonUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(userId);
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
        // Verify the conversation was created (AddAsync, not UpdateAsync)
        await MockRepository.Received(1).AddAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());

        // Verify orchestrator was called - orchestrator handles persistence atomically
        MockOrchestrator.ReceivedCalls().Count().ShouldBe(1);

        // Verify response structure
        result.ConversationId.ShouldNotBe(default);
        result.UserMessageId.ShouldNotBe(default);
        result.AssistantMessageId.ShouldNotBe(default);
        result.AssistantMessage.Value.ShouldNotBeEmpty();
    }
}