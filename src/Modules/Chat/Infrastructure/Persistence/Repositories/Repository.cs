using Axon.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Simple base repository implementation
/// </summary>
public abstract class Repository<TEntity, TId> 
    where TEntity : class
    where TId : notnull
{
    protected ChatDbContext Context { get; }

    protected Repository(ChatDbContext context)
    {
        Context = context;
    }

    public virtual async Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            Context.Set<TEntity>().Add(entity);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Persistence(ex.Message));
        }
    }

    public virtual async Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            Context.Set<TEntity>().Update(entity);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Persistence(ex.Message));
        }
    }

    public virtual async Task<Result<TEntity?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await Context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
            return Result<TEntity?>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<TEntity?>.Failure(Error.Persistence(ex.Message));
        }
    }
}