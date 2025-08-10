namespace BuildingBlocks.Infrastructure.Messaging.MassTransit;

public enum TransportType
{
    InMemory,
    RabbitMq,
    AzureServiceBus,
    AmazonSqs
}