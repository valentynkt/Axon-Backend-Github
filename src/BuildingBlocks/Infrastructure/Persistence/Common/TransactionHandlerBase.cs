using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Base class for transaction handlers that eliminates code duplication between
/// ExecuteAsync and ExecuteAsync&lt;T&gt; method implementations.
/// Follows the Template Method pattern for better maintainability.
/// </summary>
public abstract class TransactionHandlerBase : ITransactionBehaviorHandler
{
    protected readonly ILogger Logger;

    protected TransactionHandlerBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract TransactionBehavior BehaviorType { get; }
    public abstract bool HasActiveTransaction { get; }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteInternalAsync(
            async () =>
            {
                await operation();
                return true; // dummy return value
            },
            cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return await ExecuteInternalAsync(operation, cancellationToken);
    }

    /// <summary>
    /// Template method that defines the transaction execution flow.
    /// Concrete implementations provide the specific transaction management logic.
    /// </summary>
    protected virtual async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await BeforeExecutionAsync(cancellationToken);

        try
        {
            var result = await operation();
            await OnSuccessAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await OnFailureAsync(ex, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Called before the operation execution. Override to implement setup logic.
    /// </summary>
    protected virtual Task BeforeExecutionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called after successful operation execution. Override to implement commit logic.
    /// </summary>
    protected virtual Task OnSuccessAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called when the operation fails. Override to implement rollback logic.
    /// </summary>
    protected virtual Task OnFailureAsync(Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;
}