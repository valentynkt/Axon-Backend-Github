using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Handler for updating conversation title
/// </summary>
public sealed class UpdateConversationTitleHandler : IRequestHandler<UpdateConversationTitleCommand, Result<UpdateConversationTitleResponse>>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<UpdateConversationTitleHandler> _logger;

    public UpdateConversationTitleHandler(
        IConversationRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<UpdateConversationTitleHandler> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<UpdateConversationTitleResponse>> Handle(
        UpdateConversationTitleCommand command,
        CancellationToken cancellationToken)
    {
        // 1) Auth
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.Authorization("User must be authenticated to update conversation title.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            return Result<UpdateConversationTitleResponse>.Failure(ownerIdResult.Error);
        }

        // 2) Load conversation
        var conversationIdResult = ConversationId.From(command.ConversationId);
        if (conversationIdResult.IsFailure)
        {
            return Result<UpdateConversationTitleResponse>.Failure(conversationIdResult.Error);
        }

        var conversation = await _repository.GetByIdAsync(conversationIdResult.Value, cancellationToken);
        if (conversation is null)
        {
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.NotFound($"Conversation {command.ConversationId} not found.", "CHAT.CONVERSATION.NOT_FOUND"));
        }

        // Check ownership
        if (!conversation.BelongsTo(ownerIdResult.Value))
        {
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.Forbidden("Conversation does not belong to the current user.", "CHAT.CONVERSATION.ACCESS_DENIED"));
        }

        // 3) Update title (domain)
        var trimmedTitle = command.Title.Trim();
        var updateResult = conversation.UpdateTitle(trimmedTitle, _clock);
        if (updateResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to update title for conversation {ConversationId}: {ErrorCode}",
                conversation.Id.Value,
                updateResult.Error.Code);
            return Result<UpdateConversationTitleResponse>.Failure(updateResult.Error);
        }

        // 4) Persist
        await _repository.UpdateAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated title for conversation {ConversationId} to '{Title}'",
            conversation.Id.Value,
            conversation.Title);

        // 5) Return
        return Result<UpdateConversationTitleResponse>.Success(
            new UpdateConversationTitleResponse(
                conversation.Id.Value,
                conversation.Title));
    }
}