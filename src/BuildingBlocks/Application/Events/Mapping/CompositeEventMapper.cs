using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Mapping;

/// <summary>
/// Composite implementation of IEventMapper that delegates to multiple mappers.
/// Uses the first mapper that can handle the domain event (chain of responsibility pattern).
/// </summary>
public sealed class CompositeEventMapper(IEnumerable<IEventMapper> mappers) : IEventMapper
{
    private readonly IEnumerable<IEventMapper> _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));

    /// <summary>
    /// Maps a domain event to an integration event using the first available mapper.
    /// </summary>
    /// <param name="event">The domain event to map</param>
    /// <returns>The integration event if a mapper can handle it, otherwise null</returns>
    public IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event)
    {
        foreach (var mapper in _mappers)
        {
            var integrationEvent = mapper.MapToIntegrationEvent(@event);
            if (integrationEvent is not null)
                return integrationEvent;
        }

        return null;
    }

    /// <summary>
    /// Maps a domain event to an internal command using the first available mapper.
    /// </summary>
    /// <param name="event">The domain event to map</param>
    /// <returns>The internal command if a mapper can handle it, otherwise null</returns>
    public IInternalCommand? MapToInternalCommand(IDomainEvent @event)
    {
        foreach (var mapper in _mappers)
        {
            var internalCommand = mapper.MapToInternalCommand(@event);
            if (internalCommand is not null)
                return internalCommand;
        }

        return null;
    }
}