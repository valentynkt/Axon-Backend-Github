using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Persistence;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Handler for retrieving a conversation with all messages
/// </summary>
public sealed class GetConversationHandler : IQueryHandler<GetConversationQuery, GetConversationResponse>
{
    private readonly IChatReadDbContext _readDb;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetConversationHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public GetConversationHandler(
        IChatReadDbContext readDb,
        ICurrentUserService currentUser,
        ILogger<GetConversationHandler> logger,
        IAppTelemetry? telemetry = null)
    {
        _readDb = readDb;
        _currentUser = currentUser;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<Result<GetConversationResponse>> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verify user authentication
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _logger.LogWarning("Unauthenticated access attempt to conversation {ConversationId}", request.ConversationId);
            _telemetry?.TrackValidationFailure(nameof(GetConversationQuery), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<GetConversationResponse>.Failure(Error.Unauthorized("User must be authenticated"));
        }

        // 2. Parse owner ID
        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            _logger.LogWarning("Invalid UserId format: {UserId}", _currentUser.UserId);
            return Result<GetConversationResponse>.Failure(ownerIdResult.Error);
        }

        var ownerId = ownerIdResult.Value.Value; // Get the Guid
        var conversationId = request.ConversationId;

        try
        {
            // 3. Query conversation header (owner-scoped)
            var header = await _readDb.Query<ConversationHeaderRow>()
                .Where(c => c.ConversationId == conversationId && c.OwnerId == ownerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (header is null)
            {
                _logger.LogInformation("Conversation {ConversationId} not found or not accessible to user {UserId}", 
                    conversationId, _currentUser.UserId);
                return Result<GetConversationResponse>.Failure(
                    Error.NotFound("Conversation not found or not accessible"));
            }

            // 4. Query messages ordered by sequence
            var messages = await _readDb.Query<MessageRow>()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Sequence)
                .ToListAsync(cancellationToken);

            // 5. Map to response DTOs
            var messageDtos = messages.Select(m => new GetConversationMessageDto(
                MessageId: m.MessageId,
                Role: m.Role,
                Content: m.Content,
                Sequence: m.Sequence,
                CreatedAt: m.CreatedAt)).ToList();

            var response = new GetConversationResponse(
                ConversationId: header.ConversationId,
                Title: header.Title ?? string.Empty,
                Status: header.Status,
                CreatedAt: header.CreatedAt,
                UpdatedAt: header.UpdatedAt,
                CompletedAt: header.CompletedAt,
                MessageCount: header.MessageCount,
                Messages: messageDtos);

            _logger.LogInformation("Successfully retrieved conversation {ConversationId} with {MessageCount} messages", 
                conversationId, messageDtos.Count);

            return Result<GetConversationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation {ConversationId} for user {UserId}", 
                conversationId, _currentUser.UserId);
            return Result<GetConversationResponse>.Failure(
                Error.Internal("An error occurred while retrieving the conversation"));
        }
    }
}