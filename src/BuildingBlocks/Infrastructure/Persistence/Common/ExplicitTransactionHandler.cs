using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler for explicit transaction management where the caller
/// is responsible for all transaction lifecycle management.
/// Refactored to eliminate code duplication using the Template Method pattern.
/// </summary>
public sealed class ExplicitTransactionHandler : TransactionHandlerBase
{
    public ExplicitTransactionHandler(ILogger<ExplicitTransactionHandler> logger)
        : base(logger)
    {
    }

    public override TransactionBehavior BehaviorType => TransactionBehavior.Explicit;

    public override bool HasActiveTransaction => false; // Externally managed

    protected override Task BeforeExecutionAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Executing operation with explicit transaction behavior");
        return Task.CompletedTask;
    }

    // No transaction management needed - all handled externally
    // Base class handles the rest through template method pattern
}