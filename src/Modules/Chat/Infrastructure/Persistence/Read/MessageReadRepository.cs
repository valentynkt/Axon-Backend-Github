using Axon.Modules.Chat.ReadModels;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Read;

/// <summary>
/// Repository for reading message data with conversation-scoped filtering
/// </summary>
public class MessageReadRepository : EfReadRepository<MessageReadModel>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ConversationReadRepository _conversationRepository;

    public MessageReadRepository(
        ChatDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ConversationReadRepository conversationRepository) 
        : base(context)
    {
        _httpContextAccessor = httpContextAccessor;
        _conversationRepository = conversationRepository;
    }

    /// <summary>
    /// Apply user-scoped filtering via conversation ownership
    /// </summary>
    protected override IQueryable<MessageReadModel> ApplyUserScopeFilter(IQueryable<MessageReadModel> query)
    {
        // Get current user ID from HTTP context
        var userId = GetCurrentUserId();
        
        if (userId == null)
        {
            // No user context - return empty set for safety
            return query.Where(x => false);
        }

        // Get user's conversation IDs
        var userConversationIds = _conversationRepository
            .Query()
            .Select(c => c.Id)
            .ToList();

        // Filter messages by user's conversations and exclude soft-deleted
        return query
            .Where(m => userConversationIds.Contains(m.ConversationId))
            .Where(m => !m.IsDeleted);
    }

    /// <summary>
    /// Apply stable ordering for consistent pagination
    /// </summary>
    protected override IQueryable<MessageReadModel> ApplyStableOrdering(IQueryable<MessageReadModel> query)
    {
        // Order by CreatedAt ascending (chronological), then by Id for stability
        return query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id);
    }

    private Guid? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Get user ID from claims (adjust claim type as needed)
        var userIdClaim = httpContext.User.FindFirst("sub") 
                       ?? httpContext.User.FindFirst("userId")
                       ?? httpContext.User.FindFirst("id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}