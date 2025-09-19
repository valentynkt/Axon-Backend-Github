// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ChatConversationSpecs.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Thin factory over concrete specs to avoid duplication in the domain.
/// </summary>
public static class ChatConversationSpecs
{
    public static ISpecification<Conversation> ActiveByOwner(AxonUserId ownerId)
        => new ActiveByOwnerSpec(ownerId);

    public static ISpecification<Conversation> RecentlyUpdatedByOwner(AxonUserId ownerId, DateTimeOffset sinceUtc)
        => new RecentlyUpdatedByOwnerSpec(ownerId, sinceUtc);

    public static ISpecification<Conversation> CreatedInRangeByOwner(AxonUserId ownerId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
        => new ConversationsCreatedBetweenForOwnerSpec(ownerId, fromUtc, toUtc);

    public static ISpecification<Conversation> Search(
        AxonUserId ownerId,
        string? term = null,
        ConversationStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? minMessages = null)
        => new SearchConversationsSpec(ownerId, term, status, from, to, minMessages);
}