using Axon.Modules.Chat.Application.Repositories;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation with transaction management capabilities
/// Follows SPARC architecture patterns with enhanced transaction support
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ChatDbContext _context;

    public UnitOfWork(ChatDbContext context)
    {
        _context = context;
    }

    public bool HasChanges => _context.ChangeTracker.HasChanges();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.RollbackTransactionAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await BeginTransactionAsync(cancellationToken);
        try
        {
            await operation();
            await CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}