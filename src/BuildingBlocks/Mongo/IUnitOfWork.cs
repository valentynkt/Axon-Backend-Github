namespace BuildingBlocks.Mongo;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork<out TContext> : IUnitOfWork where TContext : IMongoDbContext
{
    TContext Context { get; }
}

public interface IMongoUnitOfWork<out TContext> : IUnitOfWork<TContext> where TContext : IMongoDbContext
{
}