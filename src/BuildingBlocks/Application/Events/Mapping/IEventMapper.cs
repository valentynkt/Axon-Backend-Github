using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Mapping;

/// <summary>
/// Interface for mapping domain events to integration events or internal commands.
/// Implementations should handle specific domain event types and transform them
/// into the appropriate external communication format.
/// </summary>
public interface IEventMapper
{
    /// <summary>
    /// Maps a domain event to an integration event for cross-bounded context communication.
    /// </summary>
    /// <param name="event">The domain event to map</param>
    /// <returns>The integration event if the mapper can handle the domain event, otherwise null</returns>
    IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event);

    /// <summary>
    /// Maps a domain event to an internal command for within-bounded context processing.
    /// </summary>
    /// <param name="event">The domain event to map</param>
    /// <returns>The internal command if the mapper can handle the domain event, otherwise null</returns>
    IInternalCommand? MapToInternalCommand(IDomainEvent @event);
}