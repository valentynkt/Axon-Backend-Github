using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Specifications;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Factory for common combinations of conversation specifications.
/// Provides composable, reusable query patterns.
/// </summary>
public static class ChatConversationSpecs
{
    /// <summary>
    /// Active conversations for a specific owner.
    /// </summary>
    public static Specification<Conversation> ActiveByOwner(UserId ownerId)
        => new ConversationsByOwnerSpec(ownerId)
            .And(new ConversationsByStatusSpec(ConversationStatus.Active));

    /// <summary>
    /// Active conversations for an owner that were updated since the specified time.
    /// </summary>
    public static Specification<Conversation> RecentlyUpdatedByOwner(UserId ownerId, DateTimeOffset sinceUtc)
        => ActiveByOwner(ownerId)
            .And(new ConversationsUpdatedSinceSpec(sinceUtc));

    /// <summary>
    /// Conversations for an owner created within the specified time range.
    /// </summary>
    public static Specification<Conversation> CreatedInRangeByOwner(UserId ownerId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
        => new ConversationsByOwnerSpec(ownerId)
            .And(new ConversationsCreatedBetweenSpec(fromUtc, toUtc));

    /// <summary>
    /// Comprehensive search combining multiple optional filters.
    /// Only provided filters are applied; null values are ignored.
    /// </summary>
    public static Specification<Conversation> Search(
        UserId ownerId,
        string? term = null,
        ConversationStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? minMessages = null)
    {
        Specification<Conversation> spec = new ConversationsByOwnerSpec(ownerId);

        if (!string.IsNullOrWhiteSpace(term))
            spec = spec.And(new ConversationTitleContainsSpec(term));

        if (status.HasValue)
            spec = spec.And(new ConversationsByStatusSpec(status.Value));

        if (from.HasValue && to.HasValue)
            spec = spec.And(new ConversationsCreatedBetweenSpec(from.Value, to.Value));
        else if (from.HasValue)
            spec = spec.And(new ConversationsCreatedBetweenSpec(from.Value, DateTimeOffset.MaxValue));
        else if (to.HasValue)
            spec = spec.And(new ConversationsCreatedBetweenSpec(DateTimeOffset.MinValue, to.Value));

        if (minMessages.HasValue)
            spec = spec.And(new ConversationsWithMinimumMessagesSpec(minMessages.Value));

        return spec;
    }
}