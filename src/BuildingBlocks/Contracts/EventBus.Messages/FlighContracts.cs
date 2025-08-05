using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Contracts.EventBus.Messages;

public record FlightCreated(Guid Id) : IntegrationEventBase;
public record FlightUpdated(Guid Id) : IntegrationEventBase;
public record FlightDeleted(Guid Id) : IntegrationEventBase;
public record AircraftCreated(Guid Id) : IntegrationEventBase;
public record AirportCreated(Guid Id) : IntegrationEventBase;
public record SeatCreated(Guid Id) : IntegrationEventBase;
public record SeatReserved(Guid Id) : IntegrationEventBase;