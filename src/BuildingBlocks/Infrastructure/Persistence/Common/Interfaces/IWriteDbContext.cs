using System.ComponentModel;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// OBSOLETE: Write-side context port (CQRS).
///
/// MIGRATION: Use IDbContext directly instead. The CQRS read/write split has been replaced
/// with unified contexts. Module-specific interfaces (IIdentityDbContext, IChatDbContext)
/// should inherit directly from IDbContext.
/// </summary>
[Obsolete("Use IDbContext instead. IWriteDbContext will be removed in a future version.")]
public interface IWriteDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }
}