using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Abstractions.Messaging.Contracts.EventBus.Messages;

public record BookingCreated(Guid Id) : IntegrationEventBase;