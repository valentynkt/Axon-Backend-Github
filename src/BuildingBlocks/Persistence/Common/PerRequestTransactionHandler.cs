using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Persistence.Common;

/// <summary>
/// Transaction handler that manages one transaction per request/operation scope
/// Does not automatically create transactions - relies on external transaction management
/// Suitable for scenarios where transaction scope is managed at the request level
/// </summary>
public class PerRequestTransactionHandler : ITransactionBehaviorHandler
{
    private readonly ILogger<PerRequestTransactionHandler> _logger;

    public PerRequestTransactionHandler(ILogger<PerRequestTransactionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TransactionBehavior BehaviorType => TransactionBehavior.PerRequest;

    public bool HasActiveTransaction => false; // External management

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        _logger.LogDebug("Executing operation with per-request transaction behavior");
        
        // In per-request mode, we don't manage transactions ourselves
        // The transaction scope is expected to be managed at the request level
        await operation();
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        _logger.LogDebug("Executing operation with per-request transaction behavior (with return value)");
        
        // In per-request mode, we don't manage transactions ourselves
        // The transaction scope is expected to be managed at the request level
        return await operation();
    }
}