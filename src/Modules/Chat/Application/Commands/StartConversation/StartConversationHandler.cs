using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Shared.Common;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Handler for starting a new conversation
/// </summary>
public sealed class StartConversationHandler : IRequestHandler<StartConversationCommand, Result<StartConversationResponse>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartConversationHandler(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StartConversationResponse>> Handle(
        StartConversationCommand request, 
        CancellationToken cancellationToken)
    {
        // Create conversation aggregate
        var conversationResult = Conversation.Create(request.Title, "default-user");
        if (conversationResult.IsFailure)
            return conversationResult.Error;

        var conversation = conversationResult.Value;

        // Add to repository and save
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
            return Error.Persistence("Failed to save conversation");

        // Return response using audit accessor methods
        return new StartConversationResponse(
            conversation.Id,
            conversation.Title,
            conversation.GetCreatedAt());
    }
}