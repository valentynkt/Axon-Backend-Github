using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Contracts.EventBus.Messages;

public record PassengerRegistrationCompleted(Guid Id) : IntegrationEventBase;
public record PassengerCreated(Guid Id) : IntegrationEventBase;