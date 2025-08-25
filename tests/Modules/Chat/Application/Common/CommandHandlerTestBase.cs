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

    protected override void OnApplicationSetUp()
    {
        Handler = CreateHandler();
        ConfigureHandlerDependencies();
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
    /// Test template for invalid command handling
    /// </summary>
    [Test]
    public async Task Handle_WithInvalidCommand_ShouldReturnFailure()
    {
        // Arrange
        var command = CreateInvalidCommand();
        
        // Act
        var result = await ExecuteCommand(command);
        
        // Assert
        AssertFailure(result);
    }

    /// <summary>
    /// Test template for command execution with cancellation
    /// </summary>
    [Test]
    public async Task Handle_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        var command = CreateValidCommand();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => ExecuteCommand(command, cts.Token));
        
        exception.ShouldNotBeNull();
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

    /// <summary>
    /// Verifies domain events were published correctly
    /// </summary>
    protected void AssertDomainEventsPublished<TEvent>() where TEvent : class
    {
        // This would integrate with your domain event publishing mechanism
        // Implementation depends on your specific event publishing infrastructure
    }
}