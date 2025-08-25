using BuildingBlocks.Web.Contracts;
using FastEndpoints;

namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Request DTO for retrieving paginated messages from a conversation.
/// </summary>
public sealed record GetConversationMessagesRequestDto : BasePagedRequest
{
    /// <summary>
    /// The ID of the conversation to retrieve messages from
    /// </summary>
    public Guid ConversationId { get; init; }

    /// <summary>
    /// Include deleted messages in the results. Defaults to false if not specified.
    /// </summary>
    [QueryParam]
    public bool? IncludeDeleted { get; init; }
}