using Microsoft.Extensions.Logging;
using BuildingBlocks.Application.Abstractions.Persistence;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler that creates a new transaction for each operation.
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
        if (Logger.IsEnabled(LogLevel.Debug))
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

        if (Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("Per-operation transaction committed successfully");
    }

    protected override async Task OnFailureAsync(Exception exception, CancellationToken cancellationToken)
    {
        if (Logger.IsEnabled(LogLevel.Warning))
            Logger.LogWarning(exception, "Per-operation transaction failed, rolling back");

        if (_unitOfWork.HasActiveTransaction)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
        }
    }
}