using System.Data;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Configuration options for transaction behavior.
/// </summary>
public class TransactionOptions
{
    /// <summary>
    /// The transaction isolation level to use.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
    
    /// <summary>
    /// Whether to enable transaction behavior.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Transaction timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Whether to automatically retry on transient failures.
    /// </summary>
    public bool EnableRetry { get; set; } = true;
    
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}