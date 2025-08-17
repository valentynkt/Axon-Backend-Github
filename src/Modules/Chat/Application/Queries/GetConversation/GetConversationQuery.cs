using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Query to retrieve a full conversation with all messages
/// </summary>
public sealed record GetConversationQuery(Guid ConversationId) : QueryBase<GetConversationResponse>
{
    /// <summary>
    /// Enable caching for conversation data
    /// </summary>
    public override bool UseCache => true;
    
    /// <summary>
    /// Cache for 5 minutes - conversations are relatively stable
    /// </summary>
    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Include conversation ID in cache key for uniqueness
    /// </summary>
    public override string CacheKeyPrefix => $"{base.CacheKeyPrefix}:{ConversationId}";
}