// /Modules/Chat/Application/Common/Specifications/ConversationSpecs.cs
#nullable enable
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Application.Specifications.Conversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Common.Specifications;

/// <summary>
/// Fluent builder for conversation-related specifications.
/// Provides a clean, reusable API for building conversation queries with proper encapsulation.
/// </summary>
public static class ConversationSpecs
{
    /// <summary>
    /// Creates a specification for retrieving conversations owned by a user with optional filtering and sorting.
    /// </summary>
    /// <param name="ownerId">The user ID who owns the conversations</param>
    /// <param name="page">Pagination configuration</param>
    /// <param name="sortBy">Field to sort by (default: UpdatedAt)</param>
    /// <param name="sortDirection">Sort direction (default: Desc)</param>
    /// <param name="titleContains">Optional text filter for conversation titles</param>
    /// <returns>Paged specification for conversations owned by the user</returns>
    public static ISpecification<Conversation, ConversationListItem> ForOwner(
        UserId ownerId,
        Page page,
        ConversationSortBy sortBy = ConversationSortBy.UpdatedAt,
        SortDirection sortDirection = SortDirection.Desc,
        string? titleContains = null)
    {
        return new ConversationsForOwnerSpec(ownerId, page, sortBy, sortDirection, titleContains);
    }

    /// <summary>
    /// Creates a specification to check if a conversation exists and is accessible by the specified user.
    /// This is optimized for access control checks and doesn't return full conversation data.
    /// </summary>
    /// <param name="conversationId">The conversation ID to check access for</param>
    /// <param name="userId">The user ID to verify ownership</param>
    /// <returns>Specification for conversation access verification</returns>
    public static ISpecification<Conversation> AccessCheck(ConversationId conversationId, UserId userId)
    {
        return new ConversationAccessSpec(conversationId, userId);
    }

    /// <summary>
    /// Simple specification for conversation access verification.
    /// Used to check if a user has access to a specific conversation without loading full data.
    /// </summary>
    private sealed class ConversationAccessSpec : Specification<Conversation>
    {
        public ConversationAccessSpec(ConversationId conversationId, UserId userId)
        {
            Query
                .AsNoTracking()
                .Where(c => c.Id == conversationId && c.OwnerId == userId);
        }
    }
}