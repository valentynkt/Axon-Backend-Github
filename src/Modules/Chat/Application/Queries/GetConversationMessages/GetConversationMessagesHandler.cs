using Axon.Modules.Chat.Application.Abstractions.Persistence;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Common.Queries;
using Axon.Modules.Chat.Application.Common.Specifications;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Handler for retrieving paginated messages in a specific conversation.
/// Authentication, validation, telemetry, and error handling are managed by pipeline behaviors.
/// </summary>
public sealed class GetConversationMessagesHandler : BaseChatQueryHandler<GetConversationMessagesQuery, Paged<ConversationMessageItem>>
{
    private readonly IConversationReadRepository _conversationReadRepository;
    private readonly IMessageReadRepository _messageReadRepository;

    public GetConversationMessagesHandler(
        IConversationReadRepository conversationReadRepository,
        IMessageReadRepository messageReadRepository,
        ICurrentUserService currentUserService)
        : base(currentUserService)
    {
        _conversationReadRepository = conversationReadRepository;
        _messageReadRepository = messageReadRepository;
    }

    public override async Task<Result<Paged<ConversationMessageItem>, Error>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Authentication and pagination validation handled by pipeline behaviors
            var authResult = await GetAuthenticatedAxonUserIdAsync(cancellationToken);
            if (authResult.IsFailure)
                return Result.Failure<Paged<ConversationMessageItem>, Error>(authResult.Error);

            var pageResult = Page.Sanitize(request.PageNumber, request.PageSize);
            if (pageResult.IsFailure)
            {
                return Result.Failure<Paged<ConversationMessageItem>, Error>(pageResult.Error);
            }
            
            var page = pageResult.Value;
            var conversationId = new ConversationId(request.ConversationId);

            // Verify conversation ownership
            var accessSpec = ConversationSpecs.AccessCheck(conversationId, authResult.Value);
            var isOwned = await _conversationReadRepository.AnyAsync(accessSpec, cancellationToken);
            
            if (!isOwned)
            {
                return Result.Failure<Paged<ConversationMessageItem>, Error>(
                    ChatErrors.Conversation.AccessDenied(request.ConversationId));
            }

            // Build specifications using fluent builders
            var dataSpec = MessageSpecs.ForConversation(conversationId, page, request.IncludeDeleted);
            var countSpec = MessageSpecs.ForConversationCount(conversationId, request.IncludeDeleted);

            // Execute queries
            var messages = await _messageReadRepository.ListAsync(dataSpec, cancellationToken);
            var totalCount = await _messageReadRepository.CountAsync(countSpec, cancellationToken);

            // Create result
            var result = Paged.Create(messages, page, totalCount);
            return Result.Success<Paged<ConversationMessageItem>, Error>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<Paged<ConversationMessageItem>, Error>(
                Error.Internal("Failed to retrieve conversation messages", "Chat.Messages.ListFailed", ex));
        }
    }

}