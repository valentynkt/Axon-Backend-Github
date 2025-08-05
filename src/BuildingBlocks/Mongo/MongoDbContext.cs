using MongoDB.Driver;

namespace BuildingBlocks.Mongo;

/// <summary>
/// Abstract base class for MongoDB contexts - replaced by PostgresMongoCompatDbContext for seamless migration
/// </summary>
public abstract class MongoDbContext : IMongoDbContext
{
    // This is now a stub - actual implementations use PostgresMongoCompatDbContext
    public virtual IMongoCollection<T> GetCollection<T>(string? name = null)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual Task RollbackTransaction(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual void AddCommand(Func<Task> func)
    {
        throw new NotImplementedException("MongoDB functionality has been replaced by PostgreSQL. Use PostgresMongoCompatDbContext instead.");
    }

    public virtual void Dispose()
    {
        // No-op for compatibility
    }
}