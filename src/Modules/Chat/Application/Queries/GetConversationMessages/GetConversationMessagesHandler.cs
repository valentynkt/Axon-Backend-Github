using System.Diagnostics;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Application.Specifications.Messages;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Specifications;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Handler for GetConversationMessages query.
/// Retrieves paginated messages for a specific conversation with ownership verification.
/// Includes comprehensive telemetry and performance tracking.
/// </summary>
public sealed class GetConversationMessagesHandler : IQueryHandler<GetConversationMessagesQuery, Paged<ConversationMessageItem>>
{
    private readonly IUserAuthenticationService _userAuthenticationService;
    private readonly IConversationReadRepository _conversationReadRepository;
    private readonly IMessageReadRepository _messageReadRepository;
    private readonly IChatTelemetry _telemetry;
    private readonly ILogger<GetConversationMessagesHandler> _logger;

    public GetConversationMessagesHandler(
        IUserAuthenticationService userAuthenticationService,
        IConversationReadRepository conversationReadRepository,
        IMessageReadRepository messageReadRepository,
        IChatTelemetry telemetry,
        ILogger<GetConversationMessagesHandler> logger)
    {
        _userAuthenticationService = userAuthenticationService;
        _conversationReadRepository = conversationReadRepository;
        _messageReadRepository = messageReadRepository;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<Paged<ConversationMessageItem>, Error>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        // Start telemetry tracking
        using var activity = _telemetry.StartActivity("GetConversationMessages");
        var stopwatch = Stopwatch.StartNew();
        
        // Add telemetry tags for request parameters
        activity?.SetTag("conversation.id", request.ConversationId.ToString());
        activity?.SetTag("page.number", request.PageNumber);
        activity?.SetTag("page.size", request.PageSize);
        activity?.SetTag("include.deleted", request.IncludeDeleted);

        try
        {
            // Step 1: Authenticate the user
            var userIdResult = _userAuthenticationService.GetAuthenticatedUserId();
            if (userIdResult.IsFailure)
            {
                activity?.SetTag("result", "auth_failed");
                _logger.LogWarning("Failed to authenticate user for GetConversationMessages query");
                return Result.Failure<Paged<ConversationMessageItem>, Error>(
                    Error.Unauthorized("User must be authenticated.", "Chat.Auth.Unauthenticated"));
            }

            var userId = userIdResult.Value;
            activity?.SetTag("user.id", userId.Value.ToString());
            _logger.LogInformation("Processing GetConversationMessages for user {UserId}, conversation {ConversationId}", 
                userId.Value, request.ConversationId);

            // Step 2: Sanitize pagination parameters
            var pageResult = Page.Sanitize(request.PageNumber, request.PageSize);
            if (pageResult.IsFailure)
            {
                activity?.SetTag("result", "invalid_pagination");
                _logger.LogWarning("Invalid pagination parameters: Page={Page}, Size={Size}", 
                    request.PageNumber, request.PageSize);
                return Result.Failure<Paged<ConversationMessageItem>, Error>(pageResult.Error);
            }

            var page = pageResult.Value;

            // Step 3: Verify conversation ownership using domain specification
            var conversationId = new ConversationId(request.ConversationId);
            var accessSpec = new ConversationAccessSpec(conversationId, userId);
            
            var ownershipStopwatch = Stopwatch.StartNew();
            var isOwned = await _conversationReadRepository.AnyAsync(accessSpec, cancellationToken);
            ownershipStopwatch.Stop();

            if (!isOwned)
            {
                activity?.SetTag("result", "access_denied");
                activity?.SetTag("performance.ownership_check_ms", ownershipStopwatch.ElapsedMilliseconds);
                _logger.LogWarning("User {UserId} attempted to access conversation {ConversationId} without ownership", 
                    userId.Value, request.ConversationId);
                return Result.Failure<Paged<ConversationMessageItem>, Error>(
                    ChatErrors.Conversation.AccessDenied(request.ConversationId));
            }

            // Step 4: Build specifications for data and count queries
            var dataSpec = new MessagesForConversationSpec(
                conversationId,
                page,
                request.IncludeDeleted);

            var countSpec = new MessagesForConversationCountSpec(
                conversationId,
                request.IncludeDeleted);

            // Step 5: Execute queries with performance tracking
            var queryStopwatch = Stopwatch.StartNew();
            
            var messagesTask = _messageReadRepository.ListAsync(dataSpec, cancellationToken);
            var totalCountTask = _messageReadRepository.CountAsync(countSpec, cancellationToken);

            await Task.WhenAll(messagesTask, totalCountTask);
            queryStopwatch.Stop();

            var messages = await messagesTask;
            var totalCount = await totalCountTask;

            // Step 6: Create paginated result
            var result = Paged.Create(messages, page, totalCount);
            stopwatch.Stop();

            // Add result telemetry tags
            activity?.SetTag("result", "success");
            activity?.SetTag("result.count", result.Count);
            activity?.SetTag("result.total_count", result.TotalCount);
            activity?.SetTag("result.total_pages", result.TotalPages);
            activity?.SetTag("performance.total_duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("performance.ownership_check_ms", ownershipStopwatch.ElapsedMilliseconds);
            activity?.SetTag("performance.query_duration_ms", queryStopwatch.ElapsedMilliseconds);

            // Log performance metrics
            _logger.LogInformation(
                "Retrieved {Count} messages for conversation {ConversationId} (Page {PageNumber}/{TotalPages}, Total: {TotalCount}) in {Duration}ms (Ownership: {OwnershipDuration}ms, Query: {QueryDuration}ms)",
                result.Count, request.ConversationId, result.PageNumber, result.TotalPages, result.TotalCount, 
                stopwatch.ElapsedMilliseconds, ownershipStopwatch.ElapsedMilliseconds, queryStopwatch.ElapsedMilliseconds);

            return Result.Success<Paged<ConversationMessageItem>, Error>(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetTag("result", "error");
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("performance.total_duration_ms", stopwatch.ElapsedMilliseconds);
            
            _logger.LogError(ex, "Failed to retrieve conversation messages for conversation {ConversationId} after {Duration}ms", 
                request.ConversationId, stopwatch.ElapsedMilliseconds);
            return Result.Failure<Paged<ConversationMessageItem>, Error>(
                Error.Internal("Failed to retrieve conversation messages.", "Chat.Messages.ListFailed"));
        }
    }
}