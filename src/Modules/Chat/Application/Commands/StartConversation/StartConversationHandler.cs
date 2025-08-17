using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Handler for starting a new conversation
/// </summary>
public sealed class StartConversationHandler : IRequestHandler<StartConversationCommand, Result<StartConversationResponse>>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<StartConversationHandler> _logger;

    public StartConversationHandler(
        IConversationRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<StartConversationHandler> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<StartConversationResponse>> Handle(
        StartConversationCommand command,
        CancellationToken cancellationToken)
    {
        // 1) Auth & owner
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result<StartConversationResponse>.Failure(
                Error.Authorization("User must be authenticated to start a conversation.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            return Result<StartConversationResponse>.Failure(ownerIdResult.Error);
        }

        // 2) Normalize title (domain allows empty for default)
        var normalizedTitle = command.Title?.Trim();
        if (string.IsNullOrEmpty(normalizedTitle))
        {
            normalizedTitle = null; // Let domain handle default title
        }

        // 3) Start aggregate
        var conversationResult = Conversation.Start(ownerIdResult.Value, normalizedTitle, _clock);
        if (conversationResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to start conversation for user {UserId}: {ErrorCode}",
                _currentUser.UserId,
                conversationResult.Error.Code);
            return Result<StartConversationResponse>.Failure(conversationResult.Error);
        }

        var conversation = conversationResult.Value;

        // 4) Persist
        await _repository.AddAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started conversation {ConversationId} for user {UserId}",
            conversation.Id.Value,
            ownerIdResult.Value.Value);

        // 5) Return
        return Result<StartConversationResponse>.Success(
            new StartConversationResponse(conversation.Id.Value));
    }
}