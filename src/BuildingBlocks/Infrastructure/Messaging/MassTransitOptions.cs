using BuildingBlocks.Infrastructure.Messaging.MassTransit;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Strongly-typed configuration options for MassTransit messaging infrastructure.
/// Supports multiple transports and EF Outbox configuration.
/// </summary>
public class MassTransitOptions
{
    /// <summary>
    /// The transport type to use (RabbitMQ, Azure Service Bus, InMemory).
    /// </summary>
    public TransportType Transport { get; set; } = TransportType.RabbitMq;

    /// <summary>
    /// RabbitMQ-specific configuration.
    /// </summary>
    public RabbitMqOptions? RabbitMq { get; set; }

    /// <summary>
    /// Azure Service Bus connection string (when Transport = AzureServiceBus).
    /// </summary>
    public string? AzureServiceBusConnectionString { get; set; }

    /// <summary>
    /// Optional endpoint name prefix for all queues/exchanges (e.g., "dev-", "prod-").
    /// Useful for environment isolation.
    /// </summary>
    public string? EndpointPrefix { get; set; }

    /// <summary>
    /// Environment name (Development, Staging, Production).
    /// Affects transport selection and durability settings.
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Enable EF Outbox for transactional reliability.
    /// Should always be true in production; can be false for testing.
    /// </summary>
    public bool EnableOutbox { get; set; } = true;

    /// <summary>
    /// Enable durable exchanges and queues (RabbitMQ).
    /// Should be true in production.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Default message retry configuration.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Serialization options.
    /// </summary>
    public SerializationOptions Serialization { get; set; } = new();

    /// <summary>
    /// Timeout for publish operations.
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable OpenTelemetry instrumentation.
    /// </summary>
    public bool EnableObservability { get; set; } = true;
    
    /// <summary>
    /// Include environment headers (source-service, environment, message-version) in published messages.
    /// </summary>
    public bool IncludeEnvironmentHeaders { get; set; } = true;
}

/// <summary>
/// Message retry configuration.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry interval.
    /// </summary>
    public TimeSpan InitialInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Maximum retry interval.
    /// </summary>
    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromMinutes(120);

    /// <summary>
    /// Interval increment for exponential backoff.
    /// </summary>
    public TimeSpan IntervalIncrement { get; set; } = TimeSpan.FromMilliseconds(200);
}

/// <summary>
/// Serialization configuration.
/// </summary>
public class SerializationOptions
{
    /// <summary>
    /// Use camelCase for JSON property names.
    /// </summary>
    public bool UseCamelCase { get; set; } = true;

    /// <summary>
    /// Ignore null values in JSON.
    /// </summary>
    public bool IgnoreNullValues { get; set; } = true;

    /// <summary>
    /// Serialize enums as strings.
    /// </summary>
    public bool EnumsAsStrings { get; set; } = true;

    /// <summary>
    /// Use ISO 8601 format for dates.
    /// </summary>
    public bool UseIso8601Dates { get; set; } = true;
}