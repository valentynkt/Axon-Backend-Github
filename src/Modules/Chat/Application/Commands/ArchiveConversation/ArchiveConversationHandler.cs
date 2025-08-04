using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Shared.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.ArchiveConversation;

/// <summary>
/// Handler for archive conversation command following CQRS pattern
/// </summary>
public sealed class ArchiveConversationHandler : IRequestHandler<ArchiveConversationCommand, Result<ArchiveConversationResponse>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveConversationHandler> _logger;

    public ArchiveConversationHandler(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        ILogger<ArchiveConversationHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ArchiveConversationResponse>> Handle(
        ArchiveConversationCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Archiving conversation {ConversationId}", request.ConversationId);

        // Get conversation
        var conversationResult = await _conversationRepository.GetAggregateAsync(request.ConversationId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;
            
        var conversation = conversationResult.Value;
        if (conversation is null)
        {
            _logger.LogWarning("Conversation {ConversationId} not found", request.ConversationId);
            return Error.NotFound($"Conversation with ID {request.ConversationId} not found");
        }

        // Archive conversation using domain method
        var archiveResult = conversation.ArchiveConversation();
        if (archiveResult.IsFailure)
        {
            _logger.LogWarning("Failed to archive conversation {ConversationId}: {Error}",
                request.ConversationId, archiveResult.Error);
            return archiveResult.Error;
        }

        // Update conversation in repository
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        // Save changes
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
        {
            _logger.LogError("Failed to save archived conversation {ConversationId}", request.ConversationId);
            return Error.Persistence("Failed to save archived conversation");
        }

        // Create response
        var response = new ArchiveConversationResponse(
            conversation.Id,
            conversation.Title,
            DateTime.UtcNow,
            conversation.MessageCount);

        _logger.LogInformation("Successfully archived conversation {ConversationId} with {MessageCount} messages",
            conversation.Id, conversation.MessageCount);

        return response;
    }
}