namespace Axon.Modules.Identity.Application.Commands.ProcessWebhook;

/// <summary>
/// Result of processing a Dynamic.xyz webhook event
/// </summary>
public record WebhookProcessResult(
    bool Received,
    DateTime ProcessedAt,
    bool SyncScheduled,
    string EventId,
    string? WebhookId,
    string Status,
    int RetryCount,
    string? ErrorMessage
);