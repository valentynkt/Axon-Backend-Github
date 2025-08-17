namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Response containing the ID of the newly created conversation
/// </summary>
public sealed record StartConversationResponse(Guid ConversationId);