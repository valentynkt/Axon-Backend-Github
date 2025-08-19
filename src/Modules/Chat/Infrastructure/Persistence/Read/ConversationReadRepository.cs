using System.Linq.Expressions;
using Axon.Modules.Chat.ReadModels;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Read;

/// <summary>
/// Repository for reading conversation data with user-scoped filtering
/// </summary>
public class ConversationReadRepository : EfReadRepository<ConversationReadModel>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConversationReadRepository(
        ChatDbContext context,
        IHttpContextAccessor httpContextAccessor) 
        : base(context)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Apply user-scoped filtering to ensure data isolation
    /// </summary>
    protected override IQueryable<ConversationReadModel> ApplyUserScopeFilter(IQueryable<ConversationReadModel> query)
    {
        // Get current user ID from HTTP context
        var userId = GetCurrentUserId();
        
        if (userId == null)
        {
            // No user context - return empty set for safety
            return query.Where(x => false);
        }

        // Filter by owner and exclude soft-deleted items
        return query
            .Where(c => c.OwnerId == userId.Value)
            .Where(c => !c.IsDeleted);
    }

    /// <summary>
    /// Apply stable ordering for consistent pagination
    /// </summary>
    protected override IQueryable<ConversationReadModel> ApplyStableOrdering(IQueryable<ConversationReadModel> query)
    {
        // Order by UpdatedAt descending, then by Id for stability
        return query
            .OrderByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.Id);
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