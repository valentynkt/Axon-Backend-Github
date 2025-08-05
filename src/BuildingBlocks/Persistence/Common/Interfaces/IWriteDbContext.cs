using BuildingBlocks.Core.Event;
using BuildingBlocks.Core.Model;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Write-side database context for CQRS command operations
/// Handles aggregates, domain events, and transactional consistency
/// </summary>
public interface IWriteDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>
    /// Module name for schema separation
    /// </summary>
    string ModuleName { get; }
    
    /// <summary>
    /// Get domain events from all aggregate roots for event sourcing
    /// </summary>
    IReadOnlyList<IDomainEvent> GetDomainEvents();
    
    /// <summary>
    /// Clear all domain events after processing
    /// </summary>
    void ClearDomainEvents();
}