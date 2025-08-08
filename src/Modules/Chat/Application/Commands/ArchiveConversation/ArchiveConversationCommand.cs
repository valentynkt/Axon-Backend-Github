using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.ArchiveConversation;

/// <summary>
/// Command to archive a conversation following CQRS pattern
/// </summary>
public sealed record ArchiveConversationCommand(
    ConversationId ConversationId) : IRequest<Result<ArchiveConversationResponse>>;