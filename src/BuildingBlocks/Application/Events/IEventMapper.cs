using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Event;

public interface IEventMapper
{
    IIntegrationEvent? MapToIntegrationEvent(IDomainEvent @event);
    IInternalCommand? MapToInternalCommand(IDomainEvent @event);
}