using Axon.Modules.Chat.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Axon.Modules.Chat.Infrastructure.Persistence;
using Axon.Modules.Chat.Application.Services;

namespace Axon.Modules.Chat.Infrastructure.Services;

/// <summary>
/// Simple transaction manager for database transactions
/// </summary>
public sealed class TransactionManager : ITransactionManager, IDisposable
{
    private readonly ChatDbContext _context;
    private readonly ILogger<TransactionManager> _logger;
    private IDbContextTransaction? _currentTransaction;

    public TransactionManager(
        ChatDbContext context,
        ILogger<TransactionManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("Transaction is already in progress");
        }

        _logger.LogDebug("Beginning database transaction");
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed successfully");
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Transaction rolled back");
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
    }
}