using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Internal.Text;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationTitleContainsSpec : Specification<Conversation>
{
    private readonly string _normalizedTerm;

    public ConversationTitleContainsSpec(string? term)
    {
        _normalizedTerm = TextSlices.NormalizeForSearchConst(term);
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        if (_normalizedTerm == string.Empty)
            return c => true;

        return c => c.Title != null && c.Title.ToLowerInvariant().Contains(_normalizedTerm);
    }
}