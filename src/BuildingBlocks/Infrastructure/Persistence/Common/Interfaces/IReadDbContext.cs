using System.ComponentModel;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Read-side context port (CQRS). Optimized for queries and composition.
/// </summary>
public interface IReadDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }

    /// <summary>No-tracking queryable for read models (advanced).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IQueryable<TReadModel> Query<TReadModel>() where TReadModel : class;

    /// <summary>Execute a compiled query (advanced/perf-sensitive paths).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default);
}