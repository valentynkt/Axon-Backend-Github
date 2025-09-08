using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Infrastructure.Configuration;

/// <summary>
/// Configuration options for database operations including timeouts
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Default timeout for standard database transactions (in minutes)
    /// </summary>
    [Range(1, 30, ErrorMessage = "TransactionTimeoutMinutes must be between 1 and 30 minutes")]
    public int TransactionTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Extended timeout for long-running operations like bulk processing (in minutes)
    /// </summary>
    [Range(1, 60, ErrorMessage = "LongRunningOperationTimeoutMinutes must be between 1 and 60 minutes")]
    public int LongRunningOperationTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Gets the standard transaction timeout as TimeSpan
    /// </summary>
    public TimeSpan TransactionTimeout => TimeSpan.FromMinutes(TransactionTimeoutMinutes);

    /// <summary>
    /// Gets the long-running operation timeout as TimeSpan
    /// </summary>
    public TimeSpan LongRunningOperationTimeout => TimeSpan.FromMinutes(LongRunningOperationTimeoutMinutes);
}