using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Abstractions.Messaging.Contracts.EventBus.Messages;

public record UserCreated(Guid Id, string Name, string PassportNumber) : IntegrationEventBase;