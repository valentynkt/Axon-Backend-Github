namespace Axon.Modules.Chat.Application.DTOs.ViewModels;

/// <summary>
/// Read model entity for conversation ID queries (RM-000)
/// </summary>
public sealed class ConversationIdRow
{
    public Guid ConversationId { get; set; }
    public Guid OwnerId { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Status { get; set; }
}