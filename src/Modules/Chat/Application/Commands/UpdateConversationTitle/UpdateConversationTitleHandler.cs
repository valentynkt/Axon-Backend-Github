using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;

using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Primitives.Ids;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Handler for updating conversation title
/// </summary>
public sealed class UpdateConversationTitleHandler : ICommandHandler<UpdateConversationTitleCommand, UpdateConversationTitleResponse>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateConversationTitleHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public UpdateConversationTitleHandler(
        IConversationRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<UpdateConversationTitleHandler> logger,
        IAppTelemetry? telemetry = null)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<Result<UpdateConversationTitleResponse>> Handle(
        UpdateConversationTitleCommand command,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        // 1) Auth
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _telemetry?.TrackValidationFailure(nameof(UpdateConversationTitleCommand), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.Unauthorized("User must be authenticated to update conversation title.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            return Result<UpdateConversationTitleResponse>.Failure(ownerIdResult.Error);
        }

        // 2) Load conversation
        if (command.ConversationId == Guid.Empty)
        {
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.Validation("ConversationId cannot be empty.", "CHAT.ID.EMPTY"));
        }

        var conversationId = ConversationId.From(command.ConversationId);
        var conversation = await _repository.GetByIdAsync(conversationId, cancellationToken);
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
        var updateResult = conversation.UpdateTitle(trimmedTitle, _timeProvider);
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
        stopwatch.Stop();
        _telemetry?.TrackMessageProcessed(conversation.Id.Value, stopwatch.Elapsed, success: true);

        _logger.LogInformation(
            "Updated title for conversation {ConversationId} to '{Title}'",
            conversation.Id.Value,
            conversation.Title);

        // 5) Return - Title should never be null after successful update
        if (conversation.Title is null)
        {
            return Result<UpdateConversationTitleResponse>.Failure(
                Error.Internal("Title update succeeded but title is still null", "CHAT.TITLE.UPDATE_FAILED"));
        }
        
        return Result<UpdateConversationTitleResponse>.Success(
            new UpdateConversationTitleResponse(
                conversation.Id.Value,
                conversation.Title));
    }
}