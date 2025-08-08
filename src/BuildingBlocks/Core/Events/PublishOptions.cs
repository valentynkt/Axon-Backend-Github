namespace BuildingBlocks.Core.Events;

/// <summary>
/// Configuration options for publishing integration events to external message brokers.
/// Provides comprehensive control over delivery semantics, routing, reliability, and performance characteristics.
/// </summary>
public sealed record PublishOptions
{
    /// <summary>
    /// Target destination (queue, topic, exchange) for the event.
    /// If null, the default destination based on event type will be used.
    /// </summary>
    public string? Destination { get; init; }

    /// <summary>
    /// Custom headers to include with the published event.
    /// These headers are broker-specific and may be used for routing, filtering, or metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// Time-to-live for the message in the broker.
    /// After this time expires, the message may be discarded by the broker.
    /// If null, the broker's default TTL will be used.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Maximum number of retry attempts for failed publish operations.
    /// Does not include the initial attempt. Set to 0 to disable retries.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Whether to require delivery confirmation from the message broker.
    /// When true, the operation will wait for broker acknowledgment before returning success.
    /// When false, the operation returns after the message is sent to the broker.
    /// </summary>
    public bool RequireDeliveryConfirmation { get; init; }

    /// <summary>
    /// Delivery mode for message persistence and reliability.
    /// Controls whether messages survive broker restarts and the performance implications.
    /// </summary>
    public DeliveryMode DeliveryMode { get; init; } = DeliveryMode.Persistent;

    /// <summary>
    /// Message priority level (0-255, where higher values indicate higher priority).
    /// Not all message brokers support priority queues.
    /// </summary>
    public byte Priority { get; init; } = 128; // Default to medium priority

    /// <summary>
    /// Partition key for message routing in partitioned topics/queues.
    /// Messages with the same partition key will be delivered to the same partition.
    /// </summary>
    public string? PartitionKey { get; init; }

    /// <summary>
    /// Correlation identifier for tracking related messages.
    /// Useful for distributed tracing and request correlation.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timeout for the entire publish operation.
    /// If null, the default timeout for the publisher will be used.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Whether to compress the message payload before publishing.
    /// Reduces network overhead but increases CPU usage.
    /// </summary>
    public bool EnableCompression { get; init; }

    /// <summary>
    /// Whether to enable idempotent publishing (if supported by the broker).
    /// Prevents duplicate message delivery in case of network issues or retries.
    /// </summary>
    public bool EnableIdempotency { get; init; } = true;

    /// <summary>
    /// Custom retry strategy settings.
    /// If null, exponential backoff with jitter will be used.
    /// </summary>
    public RetryStrategy? RetryStrategy { get; init; }

    /// <summary>
    /// Default publishing options with sensible defaults for most scenarios.
    /// </summary>
    public static PublishOptions Default => new();

    /// <summary>
    /// Publishing options optimized for high-throughput scenarios.
    /// Trades some reliability for performance.
    /// </summary>
    public static PublishOptions HighThroughput => new()
    {
        DeliveryMode = DeliveryMode.Transient,
        RequireDeliveryConfirmation = false,
        MaxRetryAttempts = 1,
        EnableCompression = true,
        Timeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Publishing options optimized for high-reliability scenarios.
    /// Ensures maximum delivery guarantees with comprehensive retry logic.
    /// </summary>
    public static PublishOptions HighReliability => new()
    {
        DeliveryMode = DeliveryMode.Persistent,
        RequireDeliveryConfirmation = true,
        MaxRetryAttempts = 5,
        EnableIdempotency = true,
        Timeout = TimeSpan.FromMinutes(2),
        RetryStrategy = RetryStrategy.ExponentialBackoff
    };

    /// <summary>
    /// Publishing options for critical events that must be delivered.
    /// Uses maximum reliability settings with extended timeouts.
    /// </summary>
    public static PublishOptions Critical => new()
    {
        DeliveryMode = DeliveryMode.Persistent,
        RequireDeliveryConfirmation = true,
        MaxRetryAttempts = 10,
        EnableIdempotency = true,
        Priority = 255,
        Timeout = TimeSpan.FromMinutes(5),
        RetryStrategy = RetryStrategy.ExponentialBackoffWithJitter
    };

    /// <summary>
    /// Creates publishing options with a specific destination.
    /// </summary>
    public static PublishOptions ForDestination(string destination) => new()
    {
        Destination = destination
    };

    /// <summary>
    /// Creates publishing options with specific headers.
    /// </summary>
    public static PublishOptions WithHeaders(IReadOnlyDictionary<string, object> headers) => new()
    {
        Headers = headers
    };

    /// <summary>
    /// Creates publishing options with a specific correlation ID.
    /// </summary>
    public static PublishOptions WithCorrelationId(string correlationId) => new()
    {
        CorrelationId = correlationId
    };

    /// <summary>
    /// Creates publishing options with a specific partition key.
    /// </summary>
    public static PublishOptions WithPartitionKey(string partitionKey) => new()
    {
        PartitionKey = partitionKey
    };
}

/// <summary>
/// Defines retry strategies for failed publishing operations.
/// </summary>
public enum RetryStrategy : byte
{
    /// <summary>
    /// Fixed delay between retry attempts.
    /// </summary>
    FixedDelay = 1,

    /// <summary>
    /// Exponential backoff with doubling delay between attempts.
    /// </summary>
    ExponentialBackoff = 2,

    /// <summary>
    /// Exponential backoff with random jitter to prevent thundering herd problems.
    /// </summary>
    ExponentialBackoffWithJitter = 3,

    /// <summary>
    /// Linear backoff with incrementally increasing delays.
    /// </summary>
    LinearBackoff = 4
}