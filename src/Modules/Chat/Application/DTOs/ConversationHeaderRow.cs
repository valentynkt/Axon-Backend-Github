namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// Read model entity for conversation header data
/// </summary>
public sealed class ConversationHeaderRow
{
    public Guid ConversationId { get; set; }
    public Guid OwnerId { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int MessageCount { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
}