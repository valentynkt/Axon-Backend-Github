namespace BuildingBlocks.Core.Event;

/// <summary>
/// Interface for domain events that occur within the bounded context.
/// Domain events represent business-significant occurrences within aggregates.
/// These events are handled within the same transaction boundary as the originating command.
/// </summary>
public interface IDomainEvent : IEvent
{
}