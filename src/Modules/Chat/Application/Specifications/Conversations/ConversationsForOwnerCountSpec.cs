using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

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

        // Apply title filter if provided (case-insensitive contains)
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            var titleFilter = titleContains.Trim().ToLower();
            Query.Where(c => c.Title != null && c.Title.ToLower().Contains(titleFilter));
        }
    }
}