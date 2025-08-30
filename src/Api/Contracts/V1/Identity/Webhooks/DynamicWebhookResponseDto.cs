namespace Axon.Api.Contracts.V1.Identity.Webhooks;

/// <summary>
/// Response returned after processing a Dynamic.xyz webhook event
/// </summary>
public record DynamicWebhookResponseDto
{
    /// <summary>
    /// Indicates whether the webhook was successfully received
    /// </summary>
    /// <example>true</example>
    public required bool Received { get; init; }

    /// <summary>
    /// UTC timestamp when the webhook was processed
    /// </summary>
    /// <example>2024-01-15T10:30:01Z</example>
    public required DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Indicates whether a data synchronization was scheduled
    /// </summary>
    /// <example>true</example>
    public required bool SyncScheduled { get; init; }

    /// <summary>
    /// Detailed acknowledgment information for the webhook
    /// </summary>
    public required WebhookAcknowledgmentDto Acknowledgment { get; init; }
}