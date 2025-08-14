using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.Repositories;
 
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.CompleteConversation;

/// <summary>
/// Handler for completing a conversation
/// </summary>
public sealed class CompleteConversationHandler : IRequestHandler<CompleteConversationCommand, Result<CompleteConversationResponse>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteConversationHandler(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompleteConversationResponse>> Handle(
        CompleteConversationCommand request, 
        CancellationToken cancellationToken)
    {
        // Get conversation aggregate
        var conversationResult = await _conversationRepository.GetAggregateAsync(request.ConversationId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;
            
        var conversation = conversationResult.Value;
        if (conversation is null)
            return Error.NotFound($"Conversation with ID {request.ConversationId} not found");

        // Complete conversation
        var completeResult = conversation.CompleteConversation();
        if (completeResult.IsFailure)
            return completeResult.Error;

        // Update conversation and save
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
            return Error.Persistence("Failed to complete conversation");

        // Return response
        return new CompleteConversationResponse(
            conversation.Id,
            conversation.Status.ToString(),
            conversation.CompletedAt!.Value,
            conversation.MessageCount);
    }
}