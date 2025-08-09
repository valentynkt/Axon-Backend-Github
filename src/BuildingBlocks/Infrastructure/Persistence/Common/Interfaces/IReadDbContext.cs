using System.ComponentModel;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Read-side database context (Infrastructure port) optimized for queries.
/// 
/// Clean Architecture guidance:
/// - Prefer dedicated read repositories or query objects at the Application layer.
/// - Exposing IQueryable is powerful but leaky; use with care and keep at infra/composition edges.
/// </summary>
public interface IReadDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>
    /// Logical module name (schema/tenant separation, diagnostics).
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Get no-tracking queryable for a read model.
    /// Marked Advanced to discourage broad exposure outside infra/query composition.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IQueryable<TReadModel> Query<TReadModel>() where TReadModel : class;

    /// <summary>
    /// Execute a compiled query for performance-sensitive paths.
    /// Marked Advanced; prefer encapsulating compiled queries behind repositories.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default);
}