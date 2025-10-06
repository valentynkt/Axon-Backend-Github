using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// OBSOLETE: Read-side context port (CQRS).
///
/// MIGRATION: Use IDbContext directly instead. The CQRS read/write split has been replaced
/// with unified contexts. Module-specific interfaces (IIdentityDbContext, IChatDbContext)
/// should inherit directly from IDbContext.
/// </summary>
[Obsolete("Use IDbContext instead. IReadDbContext will be removed in a future version.")]
public interface IReadDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }

    /// <summary>No-tracking queryable for read models (advanced).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IQueryable<TReadModel> Query<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TReadModel>() where TReadModel : class;

    /// <summary>Execute a compiled query (advanced/perf-sensitive paths).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default);
}