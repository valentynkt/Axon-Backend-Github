using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler that creates a new transaction for each operation
/// Automatically begins, commits, and rolls back transactions per operation
/// Suitable for operations that need individual transaction isolation
/// </summary>
public class PerOperationTransactionHandler : ITransactionBehaviorHandler
{
    private readonly IWriteUnitOfWork _unitOfWork;
    private readonly ILogger<PerOperationTransactionHandler> _logger;

    public PerOperationTransactionHandler(
        IWriteUnitOfWork unitOfWork,
        ILogger<PerOperationTransactionHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TransactionBehavior BehaviorType => TransactionBehavior.PerOperation;

    public bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        _logger.LogDebug("Starting per-operation transaction");
        
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await operation();
            
            if (_unitOfWork.HasChanges)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            
            _logger.LogDebug("Per-operation transaction committed successfully");
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Per-operation transaction failed, rolling back");
            
            if (_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        _logger.LogDebug("Starting per-operation transaction (with return value)");
        
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var result = await operation();
            
            if (_unitOfWork.HasChanges)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            
            _logger.LogDebug("Per-operation transaction committed successfully (with return value)");
            
            return result;
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Per-operation transaction failed, rolling back (with return value)");
            
            if (_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }
            
            throw;
        }
    }
}