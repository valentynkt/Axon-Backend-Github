using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Command to update the title of a conversation
/// </summary>
public sealed record UpdateConversationTitleCommand(
    Guid ConversationId,
    string Title
) : IRequest<Result<UpdateConversationTitleResponse>>;