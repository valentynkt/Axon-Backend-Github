using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.CompleteConversation;

/// <summary>
/// Command to complete/close an active conversation
/// </summary>
public sealed record CompleteConversationCommand(
    ConversationId ConversationId) : IRequest<Result<CompleteConversationResponse>>;