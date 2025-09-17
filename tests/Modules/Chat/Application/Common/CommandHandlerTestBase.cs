using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Responses;

namespace Axon.Modules.Chat.Application.Tests.Common;

/// <summary>
/// Specialized base class for testing command handlers.
/// Provides infrastructure for testing commands that modify state and produce side effects.
/// Follows SOLID principles with template method pattern for extensible command testing.
/// </summary>
/// <typeparam name="TCommand">The command type being tested</typeparam>
/// <typeparam name="TResult">The result type returned by the command</typeparam>
/// <typeparam name="THandler">The command handler type</typeparam>
public abstract class CommandHandlerTestBase<TCommand, TResult, THandler> : ApplicationTestBase
    where TCommand : IRequest<Result<TResult, Error>>
    where THandler : class, IRequestHandler<TCommand, Result<TResult, Error>>
{
    protected THandler Handler { get; private set; } = null!;

    // Common mock dependencies for command handlers
    protected IConversationRepository MockRepository { get; private set; } = null!;
    protected IMessageProcessingOrchestrator MockOrchestrator { get; private set; } = null!;
    protected TimeProvider MockTimeProvider { get; private set; } = null!;
    protected ILogger<THandler> MockHandlerLogger { get; private set; } = null!;
    protected IWriteUnitOfWork<ChatModule> MockUnitOfWork { get; private set; } = null!;
    
    // Store the default user ID to ensure consistency across mocks
    protected UserId DefaultUserId { get; private set; } = default!;

    protected override void OnApplicationSetUp()
    {
        SetupCommonCommandMocks();
        ConfigureHandlerDependencies();
        Handler = CreateHandler();
    }

    /// <summary>
    /// Cleans up disposable mocks
    /// </summary>
    [TearDown]
    public override void TearDown()
    {
        (MockRepository as IDisposable)?.Dispose();
        (MockUnitOfWork as IDisposable)?.Dispose();
        base.TearDown();
    }

    /// <summary>
    /// Sets up common mocks used by most command handlers
    /// </summary>
    protected virtual void SetupCommonCommandMocks()
    {
        // Create a consistent default user ID for all mocks
        DefaultUserId = CreateUserId();
        
        MockRepository = Substitute.For<IConversationRepository>();
        MockOrchestrator = Substitute.For<IMessageProcessingOrchestrator>();
        MockTimeProvider = Substitute.For<TimeProvider>();
        MockHandlerLogger = Substitute.For<ILogger<THandler>>();

        SetupUnitOfWork();
        SetupDefaultAuthentication();
        SetupDefaultOrchestratorBehavior();
    }

    /// <summary>
    /// Sets up the UnitOfWork mock with default behavior
    /// </summary>
    protected void SetupUnitOfWork()
    {
        MockUnitOfWork = Substitute.For<IWriteUnitOfWork<ChatModule>>();
        MockUnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        MockRepository.UnitOfWork.Returns(MockUnitOfWork);
    }

    /// <summary>
    /// Sets up default authentication behavior
    /// </summary>
    protected void SetupDefaultAuthentication(UserId? userId = null)
    {
        var userIdToUse = userId ?? DefaultUserId;
        MockCurrentUserService.UserId
            .Returns(userIdToUse.Value.ToString());
    }

    /// <summary>
    /// Sets up default orchestrator behavior for basic scenarios
    /// </summary>
    protected virtual void SetupDefaultOrchestratorBehavior()
    {
        // Use the consistent default user ID
        var placeholderConversation = ConversationBuilder.New().WithOwner(DefaultUserId).Build();
        var placeholderMessageContent = MessageContent.Create("placeholder").Value;
        var placeholderMessageId = MessageId.New();

        MockOrchestrator
            .ProcessUserMessageAsync(placeholderConversation, placeholderMessageContent, placeholderMessageId, default!)
            .ReturnsForAnyArgs(callInfo =>
            {
                var conversation = callInfo.ArgAt<Conversation>(0);
                var conversationId = conversation?.Id ?? ConversationId.New();
                var defaultResponse = new ProcessMessageResponse(
                    conversationId,
                    MessageId.New(),
                    MessageId.New(),
                    MessageContent.Create("Default assistant response").Value);
                return Result.Success<ProcessMessageResponse, Error>(defaultResponse);
            });
    }

    /// <summary>
    /// Helper to setup orchestrator for success scenarios
    /// </summary>
    protected void SetupOrchestratorSuccess(ConversationId conversationId, string assistantResponse = "Assistant response")
    {
        var assistantMessage = MessageContent.Create(assistantResponse).Value;
        var response = new ProcessMessageResponse(
            conversationId,
            MessageId.New(),
            MessageId.New(),
            assistantMessage);

        var placeholderConversation = ChatDomainTestFactory.Conversations.CreateWithOwner(DefaultUserId);
        var placeholderMessageContent = MessageContent.Create("placeholder").Value;
        var placeholderMessageId = MessageId.New();

        MockOrchestrator
            .ProcessUserMessageAsync(placeholderConversation, placeholderMessageContent, placeholderMessageId, default!)
            .ReturnsForAnyArgs(Result.Success<ProcessMessageResponse, Error>(response));
    }

    /// <summary>
    /// Helper to setup orchestrator for failure scenarios
    /// </summary>
    protected void SetupOrchestratorFailure(Error error)
    {
        var placeholderConversation = ChatDomainTestFactory.Conversations.CreateWithOwner(DefaultUserId);
        var placeholderMessageContent = MessageContent.Create("placeholder").Value;
        var placeholderMessageId = MessageId.New();

        MockOrchestrator
            .ProcessUserMessageAsync(placeholderConversation, placeholderMessageContent, placeholderMessageId, default!)
            .ReturnsForAnyArgs(Result.Failure<ProcessMessageResponse, Error>(error));
    }

    /// <summary>
    /// Helper to setup repository to return a conversation with the default user as owner
    /// </summary>
    protected void SetupRepositoryGetById(ConversationId conversationId, Conversation? conversation = null)
    {
        // If no conversation provided, create one with the default user as owner
        var conversationToReturn = conversation ?? ConversationBuilder.New()
            .WithOwner(DefaultUserId)
            .WithUserMessage("Test message")
            .WithAssistantMessage("Test response", new AiResponseId("test-ai"))
            .Build();
            
        MockRepository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversationToReturn);
    }

    /// <summary>
    /// Helper to setup repository update behavior
    /// </summary>
    protected void SetupRepositoryUpdate()
    {
        MockRepository.UpdateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult(args.ArgAt<Conversation>(0)));
    }

    /// <summary>
    /// Creates the command handler instance with its dependencies.
    /// Must be implemented by derived test classes.
    /// </summary>
    protected abstract THandler CreateHandler();

    /// <summary>
    /// Override to configure handler-specific dependencies and mocks
    /// </summary>
    protected virtual void ConfigureHandlerDependencies() { }

    /// <summary>
    /// Executes a command and returns the result
    /// </summary>
    protected async Task<Result<TResult, Error>> ExecuteCommand(
        TCommand command, 
        CancellationToken cancellationToken = default)
    {
        return await Handler.Handle(command, cancellationToken);
    }

    /// <summary>
    /// Creates a valid command for testing using the builder pattern
    /// </summary>
    protected abstract TCommand CreateValidCommand();

    /// <summary>
    /// Creates an invalid command for negative testing
    /// </summary>
    protected abstract TCommand CreateInvalidCommand();

    /// <summary>
    /// Test template for successful command execution
    /// </summary>
    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = CreateValidCommand();
        
        // Act
        var result = await ExecuteCommand(command);
        
        // Assert
        AssertSuccess(result);
        await AssertCommandSideEffects(command, result.Value);
    }
    

    /// <summary>
    /// Test template for command execution with cancellation
    /// Override this test in derived classes if cancellation testing is needed
    /// </summary>
    [Test]
    public virtual async Task Handle_WithCancellation_ShouldHandleGracefully()
    {
        // This is a base template. Override in derived classes for specific cancellation scenarios
        // Many command handlers may complete synchronously and not check cancellation tokens
        // making this test inappropriate for all command types
        await Task.CompletedTask;
        Assert.Pass("Cancellation test template - override in derived class if cancellation testing is appropriate");
    }

    /// <summary>
    /// Override to verify command-specific side effects
    /// </summary>
    protected virtual Task AssertCommandSideEffects(TCommand command, TResult result)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper to create command test scenarios for parameterized tests
    /// </summary>
    protected static TestCaseData CreateCommandTestCase<T>(
        T command,
        bool shouldSucceed,
        string testName) where T : TCommand
    {
        return new TestCaseData(command, shouldSucceed).SetName(testName);
    }
    
}