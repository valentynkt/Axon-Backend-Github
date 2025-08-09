using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler for explicit transaction management where the caller
/// is responsible for transaction lifecycle.
/// </summary>
public sealed class ExplicitTransactionHandler : TransactionHandlerBase
{
    public ExplicitTransactionHandler(ILogger<ExplicitTransactionHandler> logger)
        : base(logger) { }

    public override TransactionBehavior BehaviorType => TransactionBehavior.Explicit;

    public override bool HasActiveTransaction => false; // Externally managed

    protected override Task BeforeExecutionAsync(CancellationToken cancellationToken)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("Executing operation with explicit transaction behavior");
        return Task.CompletedTask;
    }
}