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
            var searchTerm = titleContains.Trim().ToLower();
            
            // Use simple Contains for better LINQ-to-SQL translation
            ConfigureQuery()
                .Where(c => c.Title != null && c.Title.ToLower().Contains(searchTerm));
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
                    ConfigureQuery().OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id);
                }
                break;

            case ConversationSortBy.CreatedAt:
                if (sortDirection == SortDirection.Asc)
                {
                    ConfigureQuery().OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);
                }
                break;

            case ConversationSortBy.Title:
                if (sortDirection == SortDirection.Asc)
                {
                    ConfigureQuery().OrderBy(c => c.Title).ThenBy(c => c.UpdatedAt).ThenBy(c => c.Id);
                }
                else
                {
                    ConfigureQuery().OrderByDescending(c => c.Title).ThenByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id);
                }
                break;

            default:
                // Default to UpdatedAt desc
                ConfigureQuery().OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id);
                break;
        }
    }

}