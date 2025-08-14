using BuildingBlocks.Infrastructure.Persistence;

namespace BuildingBlocks.Infrastructure.Persistence.Postgres;

/// <summary>
/// PostgreSQL-specific configuration. Inherits generic DatabaseOptions and adds PG knobs.
/// </summary>
public class PostgresOptions : DatabaseOptions
{
    public const string SectionName = "PostgresOptions";

    public PostgresOptions()
    {
        // PostgreSQL-friendly defaults
        DefaultSchema = "public";
        EnableServiceProviderCaching = true;
        MaxRetryCount = 3;
        MaxRetryDelaySeconds = 30;
        CommandTimeout = 30;
    }

    /// <summary>
    /// Optional named connection strings (per module/context).
    /// </summary>
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();

    public string GetConnectionString(string? name = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return ConnectionString;
        return ConnectionStrings.TryGetValue(name, out var cs) ? cs : ConnectionString;
    }

    public void SetConnectionString(string name, string connectionString)
        => ConnectionStrings[name] = connectionString;

    public PostgresPerformanceOptions Performance { get; set; } = new();
    public PostgresPoolingOptions    Pooling    { get; set; } = new();
}

public class PostgresPerformanceOptions
{
    public bool EnableQueryPlanCaching { get; set; } = true;
    public bool EnablePreparedStatements { get; set; } = true;
    public int  StatementCacheSize { get; set; } = 1000;
}

public class PostgresPoolingOptions
{
    public int  MinPoolSize { get; set; } = 5;
    public int  MaxPoolSize { get; set; } = 100;
    public int  ConnectionIdleLifetime { get; set; } = 300;
    public int  ConnectionPruningInterval { get; set; } = 10;
    public bool EnableMultiplexing { get; set; }
    public int  MaxAutoPrepare { get; set; }
}