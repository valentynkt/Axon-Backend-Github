namespace BuildingBlocks.Application;

public interface IWriteUnitOfWork : IAsyncDisposable, IDisposable
{
    bool   HasActiveTransaction { get; }
    string? CurrentTransactionId { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
}

/// <summary>Marker for the write UoW tied to a specific module.</summary>
public interface IWriteUnitOfWork<TModule> : IWriteUnitOfWork where TModule : class { }

/// <summary>Let a command declare which write module it belongs to.</summary>
public interface IUsesWriteModule<TModule> where TModule : class { }