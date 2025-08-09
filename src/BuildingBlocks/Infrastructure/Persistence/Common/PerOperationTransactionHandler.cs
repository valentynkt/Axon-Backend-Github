using Microsoft.Extensions.Logging;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler that creates a new transaction for each operation.
/// Refactored to eliminate code duplication using the Template Method pattern.
/// </summary>
public sealed class PerOperationTransactionHandler : TransactionHandlerBase
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public PerOperationTransactionHandler(
        IWriteUnitOfWork unitOfWork,
        ILogger<PerOperationTransactionHandler> logger)
        : base(logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public override TransactionBehavior BehaviorType => TransactionBehavior.PerOperation;

    public override bool HasActiveTransaction => _unitOfWork.HasActiveTransaction;

    protected override async Task BeforeExecutionAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Starting per-operation transaction");
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
    }

    protected override async Task OnSuccessAsync(CancellationToken cancellationToken)
    {
        if (_unitOfWork.HasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _unitOfWork.CommitTransactionAsync(cancellationToken);
        Logger.LogDebug("Per-operation transaction committed successfully");
    }

    protected override async Task OnFailureAsync(Exception exception, CancellationToken cancellationToken)
    {
        Logger.LogWarning(exception, "Per-operation transaction failed, rolling back");

        if (_unitOfWork.HasActiveTransaction)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
        }
    }
}