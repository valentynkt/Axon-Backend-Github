using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Persistence;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Queries.GetAllConversationIds;

/// <summary>
/// Handler for retrieving all conversation IDs for the current user
/// </summary>
public sealed class GetAllConversationIdsHandler : IQueryHandler<GetAllConversationIdsQuery, GetAllConversationIdsResponse>
{
    private const int Cap = 100;
    
    private readonly IChatReadDbContext _readDb;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetAllConversationIdsHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public GetAllConversationIdsHandler(
        IChatReadDbContext readDb,
        ICurrentUserService currentUser,
        ILogger<GetAllConversationIdsHandler> logger,
        IAppTelemetry? telemetry = null)
    {
        _readDb = readDb;
        _currentUser = currentUser;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<Result<GetAllConversationIdsResponse>> Handle(
        GetAllConversationIdsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verify user authentication
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _logger.LogWarning("Unauthenticated access attempt to conversation IDs");
            _telemetry?.TrackValidationFailure(nameof(GetAllConversationIdsQuery), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<GetAllConversationIdsResponse>.Failure(Error.Unauthorized("User must be authenticated"));
        }

        // 2. Parse owner ID
        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            _logger.LogWarning("Invalid UserId format: {UserId}", _currentUser.UserId);
            return Result<GetAllConversationIdsResponse>.Failure(ownerIdResult.Error);
        }

        var ownerId = ownerIdResult.Value.Value; // Get the Guid

        try
        {
            // 3. Query conversation IDs with Cap+1 to determine hasMore
            // Note: Using ConversationHeaderRow as fallback if ConversationIdRow doesn't exist yet
            var conversationIds = await _readDb.Query<ConversationHeaderRow>()
                .Where(c => c.OwnerId == ownerId)
                .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt) // Fallback to UpdatedAt if LastMessageAt is null
                .Select(c => c.ConversationId)
                .Take(Cap + 1)
                .ToListAsync(cancellationToken);

            // 4. Apply pagination logic
            var ids = conversationIds.Take(Cap).ToList();
            var hasMore = conversationIds.Count > Cap;

            _logger.LogInformation("Retrieved {Count} conversation IDs for user {UserId}, hasMore: {HasMore}", 
                ids.Count, _currentUser.UserId, hasMore);

            var response = new GetAllConversationIdsResponse(ids, hasMore);
            return Result<GetAllConversationIdsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation IDs for user {UserId}", _currentUser.UserId);
            return Result<GetAllConversationIdsResponse>.Failure(
                Error.Internal("An error occurred while retrieving conversation IDs"));
        }
    }
}