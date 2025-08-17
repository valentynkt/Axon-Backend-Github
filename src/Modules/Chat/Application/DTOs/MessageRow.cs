namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// Read model entity for message data
/// </summary>
public sealed class MessageRow
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}