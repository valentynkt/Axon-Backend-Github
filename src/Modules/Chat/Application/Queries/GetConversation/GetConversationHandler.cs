using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Handler for getting a conversation with all its messages
/// </summary>
public sealed class GetConversationHandler : IRequestHandler<GetConversationQuery, Result<ConversationDto>>
{
    private readonly IConversationRepository _conversationRepository;

    public GetConversationHandler(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<Result<ConversationDto>> HandleAsync(
        GetConversationQuery request, 
        CancellationToken cancellationToken)
    {
        // Get conversation with messages
        var conversationResult = await _conversationRepository.GetAggregateAsync(request.ConversationId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;
            
        var conversation = conversationResult.Value;
        if (conversation is null)
            return Error.NotFound($"Conversation with ID {request.ConversationId} not found");

        // Map to DTO
        var messagesDto = conversation.MessagesOrdered
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.Content,
                m.Role.Value,
                m.Sequence,
                m.CreatedAt,
                m.Metadata))
            .ToList();

        var conversationDto = new ConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.Status.ToString(),
            conversation.CreatedAt,
            conversation.CompletedAt,
            conversation.MessageCount,
            messagesDto);

        return conversationDto;
    }
}