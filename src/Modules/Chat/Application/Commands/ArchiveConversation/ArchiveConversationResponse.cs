using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.ArchiveConversation;

/// <summary>
/// Response for archive conversation command
/// </summary>
public sealed record ArchiveConversationResponse(
    ConversationId ConversationId,
    string Title,
    DateTime ArchivedAt,
    int MessageCount);