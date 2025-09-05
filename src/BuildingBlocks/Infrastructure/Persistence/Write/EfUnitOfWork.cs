using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

public sealed class EfUnitOfWork<TContext, TModule> : IWriteUnitOfWork<TModule>
    where TContext : IWriteDbContext<TModule>
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
                // Create separate timeout token for transaction operations only
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using var transactionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                // Use transaction token only for Begin/Commit/Rollback operations
                await _db.BeginTransactionAsync(transactionCts.Token);
                try 
                { 
                    // Use original cancellation token for business operations to preserve client cancellation
                    var result = await operation(cancellationToken); 
                    await _db.CommitTransactionAsync(transactionCts.Token); 
                    return result;
                }
                catch (Exception ex)
                { 
                    try
                    {
                        await _db.RollbackTransactionAsync(CancellationToken.None); // Don't pass cancelled token to rollback
                    }
                    catch (Exception rollbackEx)
                    {
                        // Log rollback failure but don't mask the original exception
                        // In production, you might want to log this
                        _ = rollbackEx; // Suppress unused variable warning
                    }
                    throw; 
                }
            },
            verifySucceeded: null,
            ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
    public void Dispose() => _db.Dispose();
}