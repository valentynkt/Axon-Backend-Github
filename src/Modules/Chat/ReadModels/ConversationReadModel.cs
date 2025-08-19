namespace Axon.Modules.Chat.ReadModels;

/// <summary>
/// Read model for OData queries on conversations
/// </summary>
public sealed record ConversationReadModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public int MessageCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}