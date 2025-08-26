using System.Diagnostics;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Application.Specifications.Conversations;
using Axon.Modules.Chat.Domain.Errors;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Handler for retrieving paginated conversations for the authenticated user.
/// Supports sorting and filtering with efficient query execution.
/// </summary>
public sealed class GetConversationsHandler : IQueryHandler<GetConversationsQuery, Paged<ConversationListItem>>
{
    private readonly IUserAuthenticationService _userAuthenticationService;
    private readonly IConversationReadRepository _conversationReadRepository;
    private readonly IChatTelemetry _telemetry;
    private readonly ILogger<GetConversationsHandler> _logger;

    public GetConversationsHandler(
        IUserAuthenticationService userAuthenticationService,
        IConversationReadRepository conversationReadRepository,
        IChatTelemetry telemetry,
        ILogger<GetConversationsHandler> logger)
    {
        _userAuthenticationService = userAuthenticationService;
        _conversationReadRepository = conversationReadRepository;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<Paged<ConversationListItem>, Error>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        // Start telemetry tracking
        using var activity = _telemetry.StartActivity("GetConversations");
        var stopwatch = Stopwatch.StartNew();
        
        // Add telemetry tags for request parameters
        activity?.SetTag("page.number", request.PageNumber);
        activity?.SetTag("page.size", request.PageSize);
        activity?.SetTag("sort.by", request.SortBy.ToString());
        activity?.SetTag("sort.direction", request.SortDirection.ToString());
        activity?.SetTag("has.title.filter", !string.IsNullOrWhiteSpace(request.TitleContains));

        try
        {
            // Step 1: Authenticate the user
            var userIdResult = _userAuthenticationService.GetAuthenticatedUserId();
            if (userIdResult.IsFailure)
            {
                activity?.SetTag("result", "auth_failed");
                _logger.LogWarning("Failed to authenticate user for GetConversations query");
                return Result.Failure<Paged<ConversationListItem>, Error>(
                    Error.Unauthorized("User must be authenticated.", "Chat.Auth.Unauthenticated"));
            }

            var userId = userIdResult.Value;
            activity?.SetTag("user.id", userId.Value.ToString());
            _logger.LogInformation("Processing GetConversations for user {UserId}", userId.Value);

            // Step 2: Sanitize pagination parameters
            var pageResult = Page.Sanitize(request.PageNumber, request.PageSize);
            if (pageResult.IsFailure)
            {
                activity?.SetTag("result", "invalid_pagination");
                _logger.LogWarning("Invalid pagination parameters: Page={Page}, Size={Size}", 
                    request.PageNumber, request.PageSize);
                return Result.Failure<Paged<ConversationListItem>, Error>(pageResult.Error);
            }

            var page = pageResult.Value;

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

            // Step 4: Execute queries with performance tracking (sequentially to avoid DbContext threading issues)
            var queryStopwatch = Stopwatch.StartNew();
            
            var conversations = await _conversationReadRepository.ListAsync(dataSpec, cancellationToken);
            var totalCount = await _conversationReadRepository.CountAsync(countSpec, cancellationToken);
            
            queryStopwatch.Stop();

            // Step 5: Create paginated result
            var result = Paged.Create(conversations, page, totalCount);
            stopwatch.Stop();

            // Add result telemetry tags
            activity?.SetTag("result", "success");
            activity?.SetTag("result.count", result.Count);
            activity?.SetTag("result.total_count", result.TotalCount);
            activity?.SetTag("result.total_pages", result.TotalPages);
            activity?.SetTag("performance.total_duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("performance.query_duration_ms", queryStopwatch.ElapsedMilliseconds);

            // Log performance metrics
            _logger.LogInformation(
                "Retrieved {Count} conversations for user {UserId} (Page {PageNumber}/{TotalPages}, Total: {TotalCount}) in {Duration}ms (Query: {QueryDuration}ms)",
                result.Count, userId.Value, result.PageNumber, result.TotalPages, result.TotalCount, 
                stopwatch.ElapsedMilliseconds, queryStopwatch.ElapsedMilliseconds);

            return Result.Success<Paged<ConversationListItem>, Error>(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("result", "error");
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("performance.total_duration_ms", stopwatch.ElapsedMilliseconds);
            
            _logger.LogError(ex, "Failed to retrieve conversations after {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
            return Result.Failure<Paged<ConversationListItem>, Error>(
                Error.Internal("Failed to list conversations.", "Chat.Conversations.ListFailed"));
        }
    }
}