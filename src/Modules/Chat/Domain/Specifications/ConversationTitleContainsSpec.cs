// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationTitleContainsSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationTitleContainsSpec : Specification<Conversation>
{
    public ConversationTitleContainsSpec(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return;

        var lowered = term.Trim().ToLowerInvariant();
        Query.Where(c => c.Title != null && c.Title.ToLower().Contains(lowered));
    }
}