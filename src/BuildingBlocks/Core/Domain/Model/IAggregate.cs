using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Interface for aggregate roots following Epic 2 specifications.
/// Integrates with IStrongId system and Epic 1 functional foundation.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier implementing IStrongId</typeparam>
public interface IAggregateRoot<TId> : IEntity<TId>
    where TId : IStrongId
{
    /// <summary>
    /// Domain events to be dispatched after persistence
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    
    /// <summary>
    /// Clear all domain events (called after dispatch)
    /// </summary>
    void ClearDomainEvents();
    
    /// <summary>
    /// Validate aggregate state against all invariants
    /// </summary>
    Validation<Unit> Validate();
    
    /// <summary>
    /// Get all broken business rules
    /// </summary>
    IReadOnlyList<IBusinessRule> GetBrokenRules();
}
