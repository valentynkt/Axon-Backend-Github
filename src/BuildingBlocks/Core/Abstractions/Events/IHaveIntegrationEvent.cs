namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Marker interface for domain events that should generate integration events.
/// Indicates that this domain event should be transformed into an integration event
/// for cross-bounded-context communication.
/// </summary>
public interface IHaveIntegrationEvent
{
    /// <summary>
    /// Gets the integration events that should be published as a result of this domain event.
    /// </summary>
    /// <returns>Collection of integration events to be published</returns>
    IEnumerable<IIntegrationEvent> GetIntegrationEvents();
}