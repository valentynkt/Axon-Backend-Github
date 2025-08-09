using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// No-op transaction handler. Useful for read-only scenarios or when
/// transaction scoping is handled completely elsewhere.
/// </summary>
public sealed class NoTransactionHandler : TransactionHandlerBase
{
    public NoTransactionHandler(ILogger<NoTransactionHandler> logger) : base(logger) { }

    public override TransactionBehavior BehaviorType => TransactionBehavior.None;

    public override bool HasActiveTransaction => false;

    // All base hooks are no-ops; we only execute the operation.
}