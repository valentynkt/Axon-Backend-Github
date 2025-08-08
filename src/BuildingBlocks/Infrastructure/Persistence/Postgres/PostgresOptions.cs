using BuildingBlocks.Infrastructure.Persistence.Common;

namespace BuildingBlocks.Infrastructure.Persistence.Postgres;

/// <summary>
/// PostgreSQL-specific configuration options that extend the base persistence configuration
/// Maintains backward compatibility while providing enterprise features
/// </summary>
public class PostgresOptions : PersistenceConfigurationOptions
{
    /// <summary>
    /// Section name for configuration binding
    /// </summary>
    public const string SectionName = "PostgresOptions";

    /// <summary>
    /// Default constructor with PostgreSQL-optimized defaults
    /// </summary>
    public PostgresOptions()
    {
        // PostgreSQL-optimized defaults
        MaxRetryCount = 3;
        MaxRetryDelaySeconds = 30;
        CommandTimeout = 30;
        DefaultSchema = "public"; // PostgreSQL default schema
        
        // Enterprise features enabled by default for PostgreSQL
        EnablePerformanceMonitoring = true;
        EnableHealthChecks = true;
        DefaultTransactionBehavior = TransactionBehavior.PerOperation;
        
        // PostgreSQL-specific optimizations
        EnableServiceProviderCaching = true;
        CacheExpiration = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// PostgreSQL connection string
    /// Supports both single connection and named connections
    /// </summary>
    public new string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Named connection strings for different modules/contexts
    /// Example: Flight, Identity, Passenger contexts
    /// </summary>
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();

    /// <summary>
    /// Get connection string by name, falls back to default if not found
    /// </summary>
    /// <param name="name">Connection name (e.g., "Flight", "Identity")</param>
    /// <returns>Connection string for the specified name or default</returns>
    public string GetConnectionString(string? name = null)
    {
        if (string.IsNullOrEmpty(name))
            return ConnectionString;

        return ConnectionStrings.TryGetValue(name, out var namedConnection) 
            ? namedConnection 
            : ConnectionString;
    }

    /// <summary>
    /// Set a named connection string
    /// </summary>
    /// <param name="name">Connection name</param>
    /// <param name="connectionString">Connection string value</param>
    public void SetConnectionString(string name, string connectionString)
    {
        ConnectionStrings[name] = connectionString;
    }

    /// <summary>
    /// PostgreSQL-specific performance settings
    /// </summary>
    public PostgresPerformanceOptions Performance { get; set; } = new();

    /// <summary>
    /// PostgreSQL-specific pooling settings
    /// </summary>
    public PostgresPoolingOptions Pooling { get; set; } = new();
}

/// <summary>
/// PostgreSQL-specific performance configuration
/// </summary>
public class PostgresPerformanceOptions
{
    /// <summary>
    /// Enable query plan caching
    /// </summary>
    public bool EnableQueryPlanCaching { get; set; } = true;

    /// <summary>
    /// Enable prepared statements
    /// </summary>
    public bool EnablePreparedStatements { get; set; } = true;

    /// <summary>
    /// Statement cache size for PostgreSQL
    /// </summary>
    public int StatementCacheSize { get; set; } = 1000;

    /// <summary>
    /// Enable performance counters
    /// </summary>
    public bool EnablePerformanceCounters { get; set; } = true;
}

/// <summary>
/// PostgreSQL connection pooling configuration
/// </summary>
public class PostgresPoolingOptions
{
    /// <summary>
    /// Minimum pool size
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Maximum pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Connection idle lifetime in seconds
    /// </summary>
    public int ConnectionIdleLifetime { get; set; } = 300;

    /// <summary>
    /// Connection pruning interval in seconds
    /// </summary>
    public int ConnectionPruningInterval { get; set; } = 10;

    /// <summary>
    /// Enable connection multiplexing
    /// </summary>
    public bool EnableMultiplexing { get; set; }

    /// <summary>
    /// Maximum commands per multiplexed connection
    /// </summary>
    public int MaxAutoPrepare { get; set; }
}