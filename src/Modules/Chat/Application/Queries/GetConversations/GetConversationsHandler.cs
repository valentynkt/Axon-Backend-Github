using Axon.Modules.Chat.Application.Abstractions.Persistence;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Common.Queries;
using Axon.Modules.Chat.Application.Common.Specifications;
using Axon.Modules.Chat.Application.Specifications.Conversations;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Handler for retrieving paginated conversations for the authenticated user.
/// Authentication, validation, telemetry, and error handling are managed by pipeline behaviors.
/// </summary>
public sealed class GetConversationsHandler : BaseChatQueryHandler<GetConversationsQuery, Paged<ConversationListItem>>
{
    private readonly IConversationReadRepository _conversationReadRepository;

    public GetConversationsHandler(
        IConversationReadRepository conversationReadRepository,
        ICurrentUserService currentUserService)
        : base(currentUserService)
    {
        _conversationReadRepository = conversationReadRepository;
    }

    public override async Task<Result<Paged<ConversationListItem>, Error>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        // Authentication and pagination validation handled by pipeline behaviors
        try 
        {
            var userId = GetAuthenticatedUserId();
            
            var pageResult = Page.Sanitize(request.PageNumber, request.PageSize);
            if (pageResult.IsFailure)
                return Result.Failure<Paged<ConversationListItem>, Error>(pageResult.Error);
            
            var page = pageResult.Value;

            // Build specifications using fluent builders
            var dataSpec = ConversationSpecs.ForOwner(
                userId, 
                page, 
                request.SortBy, 
                request.SortDirection, 
                request.TitleContains);

            // For count, we need to create a simpler specification without pagination and projections
            var conversationsForOwnerSpec = new ConversationsForOwnerSpec(
                userId, 
                new Page(1, int.MaxValue), // Large page size for counting
                request.SortBy, 
                request.SortDirection, 
                request.TitleContains);

            // Execute queries
            try
            {
                var conversations = await _conversationReadRepository.ListAsync(dataSpec, cancellationToken);
                var totalCount = await _conversationReadRepository.CountAsync(conversationsForOwnerSpec, cancellationToken);

                // Create result
                var result = Paged.Create(conversations, page, totalCount);
                return Result.Success<Paged<ConversationListItem>, Error>(result);
            }
            catch (Exception ex)
            {
                // Handle repository failures
                return Result.Failure<Paged<ConversationListItem>, Error>(
                    Error.Internal($"Failed to list conversations: {ex.Message}", "Chat.Conversations.ListFailed", ex));
            }
        }
        catch (ArgumentNullException)
        {
            // Handle unauthenticated user
            return Result.Failure<Paged<ConversationListItem>, Error>(
                Error.Unauthorized("User must be authenticated to access conversations", "Chat.Auth.Unauthenticated"));
        }
    }

}