using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
 
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.AddMessage;

/// <summary>
/// Handler for adding a message to a conversation
/// </summary>
public sealed class AddMessageHandler : IRequestHandler<AddMessageCommand, Result<AddMessageResponse>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddMessageHandler(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AddMessageResponse>> Handle(
        AddMessageCommand request, 
        CancellationToken cancellationToken)
    {
        // Get conversation aggregate
        var conversationResult = await _conversationRepository.GetAggregateAsync(request.ConversationId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;
            
        var conversation = conversationResult.Value;
        if (conversation is null)
            return Error.NotFound($"Conversation with ID {request.ConversationId} not found");

        // Parse message role
        var roleResult = MessageRole.Create(request.Role);
        if (roleResult.IsFailure)
            return roleResult.Error;

        // Add message to conversation
        var messageResult = conversation.AddMessage(request.Content, roleResult.Value, request.Metadata);
        if (messageResult.IsFailure)
            return messageResult.Error;

        var message = messageResult.Value;

        // Update conversation and save
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
            return Error.Persistence("Failed to save message");

        // Return response using audit accessor methods
        return new AddMessageResponse(
            message.Id,
            conversation.Id,
            message.Content,
            message.Role.Value,
            message.Sequence,
            message.GetCreatedAt());
    }
}