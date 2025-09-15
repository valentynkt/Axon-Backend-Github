// File: BuildingBlocks/Infrastructure/Messaging/MassTransitOptions.cs
using BuildingBlocks.Infrastructure.Messaging.MassTransit;

namespace BuildingBlocks.Infrastructure.Messaging;

public sealed class MassTransitOptions
{
    /// <summary>Which transport to use.</summary>
    public TransportType Transport { get; init; } = TransportType.RabbitMq;

    // RabbitMQ
    /// <summary>Optional full AMQP URI. If not set, Host/User/Pass/Port/VHost will be used.</summary>
    public string? RabbitMqConnectionString { get; init; }
    public RabbitMqOptions RabbitMq { get; init; } = new();

    // Azure Service Bus
    /// <summary>Full Azure Service Bus connection string.</summary>
    public string? AzureServiceBusConnectionString { get; init; }

    /// <summary>Optional endpoint name prefix (e.g., "dev-" or "orders-").</summary>
    public string? EndpointPrefix { get; init; }

    /// <summary>EF Bus Outbox on/off (recommended: true in prod).</summary>
    public bool EnableOutbox { get; init; } = true;

    /// <summary>Durable endpoints (RabbitMQ). For local/dev you may turn it off.</summary>
    public bool Durable { get; init; } = true;

    /// <summary>Default retry policy for publishes/consumes.</summary>
    public RetryOptions Retry { get; init; } = new();

    /// <summary>Timeout for publish operations.</summary>
    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Add source-service/environment/version headers.</summary>
    public bool IncludeEnvironmentHeaders { get; init; } = true;

    /// <summary>Outbox-specific configuration options.</summary>
    public OutboxOptions Outbox { get; init; } = new();
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan IntervalIncrement { get; init; } = TimeSpan.FromMilliseconds(200);
}

public sealed class OutboxOptions
{
    /// <summary>How often to poll for outbox messages (in seconds).</summary>
    public int QueryDelaySeconds { get; init; } = 5;

    /// <summary>Disable outbox entirely for InMemory transport.</summary>
    public bool DisableForInMemoryTransport { get; init; } = true;
}