namespace Axon.Api.Contracts.V1.Identity.Webhooks;

/// <summary>
/// Represents an incoming webhook event from Dynamic.xyz service
/// </summary>
public record DynamicWebhookEventDto
{
    /// <summary>
    /// Unique identifier for this webhook event
    /// </summary>
    /// <example>evt_01HQXYZ789ABCDEF01234567</example>
    public required string EventId { get; init; }

    /// <summary>
    /// Type of event being notified
    /// </summary>
    /// <example>users.created</example>
    public required string EventName { get; init; }

    /// <summary>
    /// Optional webhook configuration identifier
    /// </summary>
    /// <example>whk_config_123456</example>
    public string? WebhookId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred in UTC
    /// </summary>
    /// <example>2024-01-15T10:30:00Z</example>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Event-specific payload data
    /// </summary>
    /// <example>{"userId": "usr_123", "email": "user@example.com"}</example>
    public required IReadOnlyDictionary<string, object> Data { get; init; }
}