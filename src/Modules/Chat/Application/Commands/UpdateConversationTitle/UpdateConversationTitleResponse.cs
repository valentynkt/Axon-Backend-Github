namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Response containing the updated conversation title
/// </summary>
public sealed record UpdateConversationTitleResponse(
    Guid ConversationId,
    string Title
);