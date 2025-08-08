using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Abstractions.Messaging.Contracts.EventBus.Messages;

public record PassengerRegistrationCompleted(Guid Id) : IntegrationEventBase;
public record PassengerCreated(Guid Id) : IntegrationEventBase;