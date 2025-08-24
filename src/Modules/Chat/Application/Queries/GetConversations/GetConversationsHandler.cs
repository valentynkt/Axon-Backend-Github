using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Specifications.Conversations;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Handler for retrieving paginated conversations for the authenticated user.
/// Supports sorting and filtering with efficient query execution.
/// </summary>
public sealed class GetConversationsHandler : IQueryHandler<GetConversationsQuery, Paged<ConversationListItem>>
{
    private readonly IUserAuthenticationService _userAuthenticationService;
    private readonly IConversationReadRepository _conversationReadRepository;
    private readonly ILogger<GetConversationsHandler> _logger;

    public GetConversationsHandler(
        IUserAuthenticationService userAuthenticationService,
        IConversationReadRepository conversationReadRepository,
        ILogger<GetConversationsHandler> logger)
    {
        _userAuthenticationService = userAuthenticationService;
        _conversationReadRepository = conversationReadRepository;
        _logger = logger;
    }

    public async Task<Result<Paged<ConversationListItem>, Error>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        // Step 1: Authenticate the user
        var userIdResult = _userAuthenticationService.GetAuthenticatedUserId();
        if (userIdResult.IsFailure)
        {
            _logger.LogWarning("Failed to authenticate user for GetConversations query");
            return Result.Failure<Paged<ConversationListItem>, Error>(
                Error.Unauthorized("User must be authenticated.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var userId = userIdResult.Value;
        _logger.LogInformation("Processing GetConversations for user {UserId}", userId.Value);

        // Step 2: Sanitize pagination parameters
        var pageResult = Page.Sanitize(request.PageNumber, request.PageSize);
        if (pageResult.IsFailure)
        {
            _logger.LogWarning("Invalid pagination parameters: Page={Page}, Size={Size}", 
                request.PageNumber, request.PageSize);
            return Result.Failure<Paged<ConversationListItem>, Error>(pageResult.Error);
        }

        var page = pageResult.Value;

        try
        {
            // Step 3: Build specifications for data and count queries
            var dataSpec = new ConversationsForOwnerSpec(
                userId,
                page,
                request.SortBy,
                request.SortDirection,
                request.TitleContains);

            var countSpec = new ConversationsForOwnerCountSpec(
                userId,
                request.TitleContains);

            // Step 4: Execute queries
            var conversationsTask = _conversationReadRepository.ListAsync(dataSpec, cancellationToken);
            var totalCountTask = _conversationReadRepository.CountAsync(countSpec, cancellationToken);

            await Task.WhenAll(conversationsTask, totalCountTask);

            var conversations = await conversationsTask;
            var totalCount = await totalCountTask;

            // Step 5: Create paginated result
            var result = Paged.Create(conversations, page, totalCount);

            _logger.LogInformation(
                "Retrieved {Count} conversations for user {UserId} (Page {PageNumber}/{TotalPages}, Total: {TotalCount})",
                result.Count, userId.Value, result.PageNumber, result.TotalPages, result.TotalCount);

            return Result.Success<Paged<ConversationListItem>, Error>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve conversations for user {UserId}", userId.Value);
            return Result.Failure<Paged<ConversationListItem>, Error>(
                Error.Internal("Failed to list conversations.", "CHAT.CONVERSATIONS.LIST_FAILED"));
        }
    }
}