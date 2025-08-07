namespace BuildingBlocks.Persistence.Common;

/// <summary>
/// Defines different transaction behavior patterns for persistence operations
/// Allows configuring how transactions are managed across different scenarios
/// </summary>
public enum TransactionBehavior
{
    /// <summary>
    /// No automatic transaction management - manual control required
    /// </summary>
    None,
    
    /// <summary>
    /// One transaction per HTTP request/operation scope
    /// Transaction spans the entire request lifecycle
    /// </summary>
    PerRequest,
    
    /// <summary>
    /// One transaction per individual database operation
    /// Each operation gets its own transaction scope
    /// </summary>
    PerOperation,
    
    /// <summary>
    /// Explicit transaction management - transactions must be manually started and committed
    /// Provides full control over transaction boundaries
    /// </summary>
    Explicit
}