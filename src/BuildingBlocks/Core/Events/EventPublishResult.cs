namespace BuildingBlocks.Core.Events;

/// <summary>
/// Represents the result of successfully publishing a single integration event to an external message broker.
/// Contains comprehensive metadata about the publishing operation for observability and debugging.
/// </summary>
/// <param name="MessageId">Unique identifier assigned by the message broker for this published event</param>
/// <param name="PublishedAtUtc">UTC timestamp when the event was successfully published</param>
/// <param name="Destination">The target destination (queue, topic, exchange) where the event was published</param>
/// <param name="Latency">Time taken to publish the event from initiation to broker acknowledgment</param>
/// <param name="DeliveryMode">The delivery mode used for publishing (persistent, transient, etc.)</param>
/// <param name="PartitionKey">Partition key used for message routing, if applicable</param>
/// <param name="Headers">Additional headers applied during publishing</param>
public sealed record EventPublishResult(
    string MessageId,
    DateTime PublishedAtUtc,
    string Destination,
    TimeSpan Latency,
    DeliveryMode DeliveryMode = DeliveryMode.Persistent,
    string? PartitionKey = null,
    IReadOnlyDictionary<string, object>? Headers = null)
{
    /// <summary>
    /// Creates an EventPublishResult with minimal required information.
    /// </summary>
    public static EventPublishResult Create(
        string messageId,
        string destination,
        TimeSpan latency,
        DateTime? publishedAtUtc = null)
    {
        return new EventPublishResult(
            MessageId: messageId,
            PublishedAtUtc: publishedAtUtc ?? DateTime.UtcNow,
            Destination: destination,
            Latency: latency);
    }

    /// <summary>
    /// Creates an EventPublishResult with comprehensive metadata.
    /// </summary>
    public static EventPublishResult CreateDetailed(
        string messageId,
        string destination,
        TimeSpan latency,
        DeliveryMode deliveryMode,
        string? partitionKey = null,
        IReadOnlyDictionary<string, object>? headers = null,
        DateTime? publishedAtUtc = null)
    {
        return new EventPublishResult(
            MessageId: messageId,
            PublishedAtUtc: publishedAtUtc ?? DateTime.UtcNow,
            Destination: destination,
            Latency: latency,
            DeliveryMode: deliveryMode,
            PartitionKey: partitionKey,
            Headers: headers);
    }
}

/// <summary>
/// Represents the delivery mode for message publishing.
/// Determines the reliability and performance characteristics of the message delivery.
/// </summary>
public enum DeliveryMode : byte
{
    /// <summary>
    /// Non-persistent delivery for best performance.
    /// Messages may be lost if the broker restarts.
    /// </summary>
    Transient = 1,

    /// <summary>
    /// Persistent delivery with durability guarantees.
    /// Messages survive broker restarts but have higher latency.
    /// </summary>
    Persistent = 2,

    /// <summary>
    /// Adaptive delivery mode based on message importance and broker capabilities.
    /// The publisher selects the optimal delivery mode automatically.
    /// </summary>
    Adaptive = 3
}