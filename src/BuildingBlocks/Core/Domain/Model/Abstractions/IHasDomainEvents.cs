namespace BuildingBlocks.Core.Domain.Core.Model.Abstractions;

/// <summary>
/// Exposes the domain events raised by an aggregate in the current unit-of-work.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears all currently raised events (called by infra after dispatch).</summary>
    void ClearDomainEvents();
}