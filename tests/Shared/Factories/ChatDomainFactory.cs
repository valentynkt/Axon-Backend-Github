using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Tests.Shared.Factories;

/// <summary>
/// Factory for creating valid Chat domain objects for testing
/// </summary>
public static class ChatDomainFactory
{
    /// <summary>
    /// Creates a valid ConversationId
    /// </summary>
    public static ConversationId ValidConversationId() => ConversationId.New();

    /// <summary>
    /// Creates a ConversationId from a specific GUID
    /// </summary>
    public static ConversationId ConversationIdFrom(Guid guid) => ConversationId.Create(guid).Value;

    /// <summary>
    /// Creates a valid MessageId
    /// </summary>
    public static MessageId ValidMessageId() => MessageId.New();

    /// <summary>
    /// Creates a MessageId from a specific GUID
    /// </summary>
    public static MessageId MessageIdFrom(Guid guid) => MessageId.Create(guid).Value;

    /// <summary>
    /// Creates a valid McpServerUrl
    /// </summary>
    public static McpServerUrl ValidMcpServerUrl(string? url = null) => 
        McpServerUrl.Create(url ?? "https://example.com/mcp").Value;

    /// <summary>
    /// Creates multiple valid ConversationIds
    /// </summary>
    public static IEnumerable<ConversationId> ValidConversationIds(int count) =>
        Enumerable.Range(0, count).Select(_ => ValidConversationId());

    /// <summary>
    /// Creates multiple valid MessageIds
    /// </summary>
    public static IEnumerable<MessageId> ValidMessageIds(int count) =>
        Enumerable.Range(0, count).Select(_ => ValidMessageId());

    /// <summary>
    /// Creates a pair of equal ConversationIds from the same GUID
    /// </summary>
    public static (ConversationId first, ConversationId second) EqualConversationIds()
    {
        var guid = Guid.NewGuid();
        return (ConversationIdFrom(guid), ConversationIdFrom(guid));
    }

    /// <summary>
    /// Creates a pair of equal MessageIds from the same GUID
    /// </summary>
    public static (MessageId first, MessageId second) EqualMessageIds()
    {
        var guid = Guid.NewGuid();
        return (MessageIdFrom(guid), MessageIdFrom(guid));
    }

    /// <summary>
    /// Creates a collection of unique ConversationIds
    /// </summary>
    public static IList<ConversationId> UniqueConversationIds(int count)
    {
        var ids = new List<ConversationId>();
        for (int i = 0; i < count; i++)
        {
            ids.Add(ValidConversationId());
        }
        return ids;
    }
}