using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Application.Specifications.Conversations;

/// <summary>
/// Specification for counting conversations owned by a specific user with optional filtering.
/// Used for pagination metadata - no sorting or paging applied.
/// </summary>
public sealed class ConversationsForOwnerCountSpec : Specification<Conversation>
{
    public ConversationsForOwnerCountSpec(
        UserId ownerId,
        string? titleContains = null)
    {
        // Apply AsNoTracking for read-only count query
        Query.AsNoTracking();

        // Apply owner filter - only conversations owned by the specified user
        Query.Where(c => c.OwnerId == ownerId);

        // Apply text search filter if provided (same logic as ConversationsForOwnerSpec for consistency)
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            var searchTerm = titleContains.Trim();
            
            // Try PostgreSQL full-text search first for better relevance and performance
            // Falls back to LIKE search if full-text search is not available
            if (IsFullTextSearchEnabled())
            {
                // Use PostgreSQL full-text search with ranking for better results
                var normalizedSearchTerm = searchTerm.Replace(" ", " & ", StringComparison.Ordinal);
                Query.Where(c => c.Title != null && 
                               EF.Functions.ToTsVector("english", c.Title)
                                   .Matches(EF.Functions.PlainToTsQuery("english", normalizedSearchTerm)));
            }
            else
            {
                // Fallback to case-insensitive LIKE search
                var titleFilter = searchTerm.ToLower();
                Query.Where(c => c.Title != null && 
                               EF.Functions.Like(c.Title.ToLower(), $"%{titleFilter}%"));
            }
        }
    }

    /// <summary>
    /// Determines if PostgreSQL full-text search is available and enabled.
    /// This is a simple heuristic - in production, you might want to check actual database capabilities.
    /// </summary>
    private static bool IsFullTextSearchEnabled()
    {
        // For now, assume full-text search is available in PostgreSQL environments
        // In a real scenario, you might want to check the database provider or configuration
        return true; // PostgreSQL with full-text search capabilities
    }
}