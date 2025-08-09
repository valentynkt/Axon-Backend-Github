using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Base class for transaction handlers using the Template Method pattern.
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

    // Compatibility overload
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteInternalAsync(async _ =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    // Compatibility overload
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return await ExecuteInternalAsync(_ => operation(), cancellationToken).ConfigureAwait(false);
    }

    // New CT-friendly overload
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteInternalAsync(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    // New CT-friendly overload
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return await ExecuteInternalAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Template method that defines the transaction execution flow.
    /// Concrete implementations provide the specific transaction management logic.
    /// </summary>
    protected virtual async Task<T> ExecuteInternalAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await BeforeExecutionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            await OnSuccessAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await OnFailureAsync(ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Called before the operation execution. Override to implement setup logic.</summary>
    protected virtual Task BeforeExecutionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called after successful operation execution. Override to implement commit logic.</summary>
    protected virtual Task OnSuccessAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called when the operation fails. Override to implement rollback logic.</summary>
    protected virtual Task OnFailureAsync(Exception exception, CancellationToken cancellationToken) => Task.CompletedTask;
}
