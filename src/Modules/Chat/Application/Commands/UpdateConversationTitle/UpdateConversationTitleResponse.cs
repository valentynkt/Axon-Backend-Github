using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Response containing the updated conversation title
/// </summary>
public sealed record UpdateConversationTitleResponse(
    ConversationId ConversationId,
    string Title
);