// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/SearchConversationsSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class SearchConversationsSpec : Specification<Conversation>
{
    public SearchConversationsSpec(
        UserId ownerId,
        string? term,
        ConversationStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? minMessages)
    {
        // Owner is always required
        Query.Where(c => c.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowered = term.Trim().ToLowerInvariant();
            Query.Where(c => c.Title != null && c.Title.ToLower().Contains(lowered));
        }

        if (status.HasValue)
        {
            var st = status.Value;
            Query.Where(c => c.Status == st);
        }

        if (from.HasValue && to.HasValue)
        {
            var f = from.Value;
            var t = to.Value;
            if (f > t) (f, t) = (t, f);
            Query.Where(c => c.CreatedAt >= f && c.CreatedAt <= t);
        }
        else if (from.HasValue)
        {
            var f = from.Value;
            Query.Where(c => c.CreatedAt >= f);
        }
        else if (to.HasValue)
        {
            var t = to.Value;
            Query.Where(c => c.CreatedAt <= t);
        }

        if (minMessages.HasValue)
        {
            var min = Math.Max(0, minMessages.Value);
            // Uses EF-visible navigation (Conversation.Messages)
            Query.Where(c => c.Messages.Count >= min);
        }
    }
}