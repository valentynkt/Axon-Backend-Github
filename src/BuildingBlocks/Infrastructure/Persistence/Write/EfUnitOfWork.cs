using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

public sealed class EfUnitOfWork<TContext, TModule> : IWriteUnitOfWork<TModule>
    where TContext : IDbContext
    where TModule  : class
{
    private readonly TContext _db;
    public EfUnitOfWork(TContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public bool   HasActiveTransaction  => _db.HasActiveTransaction;
    public string? CurrentTransactionId => _db.CurrentTransactionId;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        var strategy = _db.CreateExecutionStrategy();
        await strategy.ExecuteAsync<Func<CancellationToken, Task>, object?>(
            action,
            async (context, operation, cancellationToken) =>
            {
                await _db.BeginTransactionAsync(cancellationToken);
                try 
                { 
                    await operation(cancellationToken); 
                    await _db.CommitTransactionAsync(cancellationToken); 
                    return (object?)null;
                }
                catch 
                { 
                    await _db.RollbackTransactionAsync(cancellationToken); 
                    throw; 
                }
            },
            verifySucceeded: null,
            ct);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        var strategy = _db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<Func<CancellationToken, Task<T>>, T>(
            action,
            async (context, operation, cancellationToken) =>
            {
                await _db.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await operation(cancellationToken);
                    await _db.CommitTransactionAsync(cancellationToken);
                    return result;
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _db.RollbackTransactionAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackEx)
                    {
                        _ = rollbackEx;
                    }

                    // Enhanced error context for better debugging
                    throw new InvalidOperationException(
                        $"Transaction failed in {typeof(TModule).Name} module. " +
                        $"Transaction ID: {_db.CurrentTransactionId ?? "none"}. " +
                        $"Original error: {ex.Message}",
                        ex);
                }
            },
            verifySucceeded: null,
            ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
    public void Dispose() => _db.Dispose();
}