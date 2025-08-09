using System.ComponentModel;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Write-side database context (Infrastructure port) used by UoW/repositories.
/// 
/// Clean Architecture guidance:
/// - Application orchestrates via UoW/Repositories; Domain should not see this.
/// - Domain events are gathered from aggregates and dispatched by Application orchestration.
/// </summary>
public interface IWriteDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>
    /// Logical module name (schema/tenant separation, diagnostics).
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Get domain events from tracked aggregates for event publication.
    /// </summary>
    new IReadOnlyList<IDomainEvent> GetDomainEvents();

    /// <summary>
    /// Clear collected domain events after publication.
    /// </summary>
    void ClearDomainEvents();

    /// <summary>
    /// Persist changes and dispatch domain events in a single call.
    /// Prefer orchestrating from Application, implemented in Infrastructure.
    /// </summary>
    Task<int> SaveChangesAndDispatchDomainEventsAsync(
        CancellationToken cancellationToken = default);
}