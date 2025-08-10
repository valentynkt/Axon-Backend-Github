using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker interface for domain events that can directly provide integration events.
/// Alternative to mapping via IEventMapper - domain events can implement this
/// for direct integration event generation.
/// </summary>
public interface IHaveIntegrationEvent
{
    /// <summary>
    /// Get the integration events that should be published for this domain event.
    /// </summary>
    /// <returns>Collection of integration events to publish</returns>
    IEnumerable<IIntegrationEvent> GetIntegrationEvents();
}