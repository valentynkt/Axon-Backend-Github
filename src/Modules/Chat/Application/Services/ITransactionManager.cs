namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Interface for managing database transactions
/// </summary>
public interface ITransactionManager
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}