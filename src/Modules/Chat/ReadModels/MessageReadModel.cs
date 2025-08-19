namespace Axon.Modules.Chat.ReadModels;

/// <summary>
/// Read model for OData queries on messages
/// </summary>
public sealed record MessageReadModel
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int TokenCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}