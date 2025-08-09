using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler for per-request transaction management where transactions
/// are managed at the HTTP request level (typically via middleware or filters).
/// Refactored to eliminate code duplication using the Template Method pattern.
/// </summary>
public sealed class PerRequestTransactionHandler : TransactionHandlerBase
{
    public PerRequestTransactionHandler(ILogger<PerRequestTransactionHandler> logger)
        : base(logger)
    {
    }

    public override TransactionBehavior BehaviorType => TransactionBehavior.PerRequest;

    public override bool HasActiveTransaction => false; // External management

    protected override Task BeforeExecutionAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Executing operation with per-request transaction behavior");
        return Task.CompletedTask;
    }

    // No transaction management needed - all handled at request level
    // Base class handles the rest through template method pattern
}