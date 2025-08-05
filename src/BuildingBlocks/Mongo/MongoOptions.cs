namespace BuildingBlocks.Mongo;

/// <summary>
/// MongoDB configuration options for compatibility during PostgreSQL migration
/// </summary>
public class MongoOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}