// Auto-commit only for commands that touched a write context.
// Register this behavior INNER than invalidation and OUTER than caching.

using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class AutoCommitOnSuccessBehavior<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse>
    where TRequest : notnull, IRequest<TResponse>
{
    private readonly IEnumerable<Microsoft.EntityFrameworkCore.DbContext> _contexts;

    public AutoCommitOnSuccessBehavior(IEnumerable<Microsoft.EntityFrameworkCore.DbContext> contexts)
    {
        _contexts = contexts; // all scoped DbContexts (one per module typically)
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var res = await next();

        // Commit only write contexts that have pending changes
        foreach (var ctx in _contexts)
        {
            if (ctx.ChangeTracker.HasChanges())
                await ctx.SaveChangesAsync(ct);
        }

        return res;
    }
}