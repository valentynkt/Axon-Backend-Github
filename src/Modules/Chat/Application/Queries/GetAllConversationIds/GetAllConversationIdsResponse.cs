namespace Axon.Modules.Chat.Application.Queries.GetAllConversationIds;

/// <summary>
/// Response containing conversation IDs and pagination info
/// </summary>
public sealed record GetAllConversationIdsResponse(
    IReadOnlyList<Guid> Ids,
    bool HasMore);