using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Publishing;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default);
}