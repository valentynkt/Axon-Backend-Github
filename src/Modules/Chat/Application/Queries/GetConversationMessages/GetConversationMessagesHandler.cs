using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Pagination;
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
public sealed class GetConversationMessagesHandler : BaseChatQueryHandler, IQueryHandler<GetConversationMessagesQuery, Paged<ConversationMessageItem>>
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

    public async Task<Result<Paged<ConversationMessageItem>, Error>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        // Authentication and pagination validation handled by pipeline behaviors
        var userId = GetAuthenticatedUserId();
        var page = Page.Sanitize(request.PageNumber, request.PageSize).Value;
        var conversationId = new ConversationId(request.ConversationId);

        // Verify conversation ownership
        var accessSpec = ConversationSpecs.AccessCheck(conversationId, userId);
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

}