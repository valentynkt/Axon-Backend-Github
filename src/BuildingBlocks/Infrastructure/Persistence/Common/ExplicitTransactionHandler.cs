using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Common;

/// <summary>
/// Transaction handler for explicit transaction management
/// Does not automatically manage transactions - requires manual transaction control
/// Suitable for complex scenarios where fine-grained transaction control is needed
/// </summary>
public class ExplicitTransactionHandler : ITransactionBehaviorHandler
{
    private readonly ILogger<ExplicitTransactionHandler> _logger;

    public ExplicitTransactionHandler(ILogger<ExplicitTransactionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TransactionBehavior BehaviorType => TransactionBehavior.Explicit;

    public bool HasActiveTransaction => false; // Externally managed

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        _logger.LogDebug("Executing operation with explicit transaction behavior");
        
        // In explicit mode, we don't manage transactions at all
        // The caller is responsible for all transaction management
        await operation();
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        _logger.LogDebug("Executing operation with explicit transaction behavior (with return value)");
        
        // In explicit mode, we don't manage transactions at all
        // The caller is responsible for all transaction management
        return await operation();
    }
}