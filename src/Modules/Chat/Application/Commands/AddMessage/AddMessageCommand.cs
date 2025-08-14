using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
 
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.AddMessage;

/// <summary>
/// Command to add a message to an existing conversation
/// </summary>
public sealed record AddMessageCommand(
    ConversationId ConversationId,
    string Content,
    string Role,
    Dictionary<string, object>? Metadata = null) : IRequest<Result<AddMessageResponse>>;