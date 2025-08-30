namespace Axon.Api.Contracts.V1.Identity.Webhooks;

/// <summary>
/// Acknowledgment details for webhook processing
/// </summary>
public record WebhookAcknowledgmentDto
{
    /// <summary>
    /// Echo of the received event ID
    /// </summary>
    /// <example>evt_01HQXYZ789ABCDEF01234567</example>
    public required string EventId { get; init; }

    /// <summary>
    /// Echo of the webhook configuration ID if provided
    /// </summary>
    /// <example>whk_config_123456</example>
    public string? WebhookId { get; init; }

    /// <summary>
    /// Processing status
    /// </summary>
    /// <example>processed</example>
    public required string Status { get; init; }

    /// <summary>
    /// Number of retry attempts (0 for first successful attempt)
    /// </summary>
    /// <example>0</example>
    public required int RetryCount { get; init; }

    /// <summary>
    /// Error message if processing failed (null on success)
    /// </summary>
    /// <example>null</example>
    public string? ErrorMessage { get; init; }
}