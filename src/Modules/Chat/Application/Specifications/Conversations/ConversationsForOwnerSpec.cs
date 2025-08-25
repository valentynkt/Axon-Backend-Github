using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Application.Specifications.Base;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Specifications.Conversations;

/// <summary>
/// Specification for retrieving conversations owned by a specific user with filtering, sorting, and pagination.
/// Composes multiple smaller specifications for better separation of concerns.
/// Projects to ConversationListItem for optimized data transfer.
/// </summary>
public sealed class ConversationsForOwnerSpec : PagedSpecification<Conversation, ConversationListItem>
{
    public ConversationsForOwnerSpec(
        UserId ownerId,
        Page page,
        ConversationSortBy sortBy = ConversationSortBy.UpdatedAt,
        SortDirection sortDirection = SortDirection.Desc,
        string? titleContains = null)
        : base(page)
    {
        // Apply ownership filter - only conversations owned by the specified user
        ConfigureQuery()
            .Where(c => c.OwnerId == ownerId);

        // Apply text search filter if provided
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            var searchTerm = titleContains.Trim();
            
            // Use the extracted search logic through composition
            // Note: For now, inline the search logic until we can properly compose specifications
            // TODO: Implement proper specification composition pattern
            if (IsFullTextSearchEnabled())
            {
                var normalizedSearchTerm = searchTerm.Replace(" ", " & ", StringComparison.Ordinal);
                ConfigureQuery()
                    .Where(c => c.Title != null && 
                               Microsoft.EntityFrameworkCore.EF.Functions.ToTsVector("english", c.Title)
                                   .Matches(Microsoft.EntityFrameworkCore.EF.Functions.PlainToTsQuery("english", normalizedSearchTerm)));
            }
            else
            {
                var titleFilter = searchTerm.ToLower();
                ConfigureQuery()
                    .Where(c => c.Title != null && 
                               Microsoft.EntityFrameworkCore.EF.Functions.Like(c.Title.ToLower(), $"%{titleFilter}%"));
            }
        }

        // Apply sorting with stable secondary sort
        ApplySorting(sortBy, sortDirection);

        // Project to ConversationListItem for optimized data transfer
        ConfigureQuery()
            .Select(c => new ConversationListItem(
                c.Id.Value,
                c.Title ?? string.Empty,
                c.CreatedAt.DateTime,
                c.UpdatedAt.HasValue ? c.UpdatedAt.Value.DateTime : c.CreatedAt.DateTime,
                c.LastAiResponseId.HasValue ? c.LastAiResponseId.Value.Value : null
            ));
    }

    private void ApplySorting(ConversationSortBy sortBy, SortDirection sortDirection)
    {
        // Primary sort
        switch (sortBy)
        {
            case ConversationSortBy.UpdatedAt:
                if (sortDirection == SortDirection.Asc)
                {
                    ConfigureQuery().OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id.Value);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id.Value);
                }
                break;

            case ConversationSortBy.CreatedAt:
                if (sortDirection == SortDirection.Asc)
                {
                    ConfigureQuery().OrderBy(c => c.CreatedAt).ThenBy(c => c.UpdatedAt).ThenBy(c => c.Id.Value);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id.Value);
                }
                break;

            case ConversationSortBy.Title:
                if (sortDirection == SortDirection.Asc)
                {
                    ConfigureQuery().OrderBy(c => c.Title ?? string.Empty).ThenByDescending(c => c.UpdatedAt).ThenBy(c => c.Id.Value);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.Title ?? string.Empty).ThenByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id.Value);
                }
                break;

            default:
                // Default to UpdatedAt desc
                ConfigureQuery().OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id.Value);
                break;
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