using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.CompleteConversation;

/// <summary>
/// Command to complete/close an active conversation
/// </summary>
public sealed record CompleteConversationCommand(
    ConversationId ConversationId) : IRequest<Result<CompleteConversationResponse>>;