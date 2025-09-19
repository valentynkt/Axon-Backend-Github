using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Abstractions.Persistence;

/// <summary>
/// Read-only repository for conversation query operations.
/// Supports specification-based queries for flexible data retrieval
/// and includes optimized compiled queries for hot paths.
/// </summary>
public interface IConversationReadRepository : ISpecificationReadRepository<Conversation>
{
    /// <summary>
    /// Optimized method for getting conversations by owner using compiled queries.
    /// Should be used for the most common GetConversations scenarios.
    /// </summary>
    Task<IReadOnlyList<ConversationListItem>> GetConversationsForOwnerOptimizedAsync(
        AxonUserId ownerId, 
        int skip, 
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimized method for counting conversations by owner using compiled queries.
    /// </summary>
    Task<int> CountConversationsForOwnerOptimizedAsync(
        AxonUserId ownerId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimized method for counting conversations with title filter using compiled queries.
    /// </summary>
    Task<int> CountConversationsWithTitleOptimizedAsync(
        AxonUserId ownerId, 
        string titleContains,
        CancellationToken cancellationToken = default);
}