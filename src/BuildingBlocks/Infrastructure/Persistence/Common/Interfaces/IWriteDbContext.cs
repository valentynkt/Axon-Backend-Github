using System.ComponentModel;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Write-side context port (CQRS). Used by repositories/UoW under Application behaviors.
/// </summary>
public interface IWriteDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>Logical module name (schema/diagnostics separation).</summary>
    string ModuleName { get; }

    /// <summary>Collect domain events from tracked aggregates.</summary>
    new IReadOnlyList<IDomainEvent> GetDomainEvents();

    /// <summary>Clear tracked domain events after publication.</summary>
    void ClearDomainEvents();
}