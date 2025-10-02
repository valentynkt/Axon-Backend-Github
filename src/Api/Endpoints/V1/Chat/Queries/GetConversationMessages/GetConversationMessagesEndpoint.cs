using Axon.Api.Contracts.V1.Chat;
using Axon.Api.Modules;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using BuildingBlocks.Web.Endpoints.Base;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesEndpoint(IMediator mediator, ILogger<GetConversationMessagesEndpoint> logger)
    : BaseChatQueryEndpoint<GetConversationMessagesRequestDto, GetConversationMessagesResponseDto, GetConversationMessagesQuery, Paged<ConversationMessageItem>>(mediator, logger)
{
    protected override string GetRoute() => "/api/v1/chat/conversations/{conversationId}/messages";

    protected override string GetSummary() => "Get paginated messages for a conversation";

    protected override string GetDescription() => "Retrieves messages from a specific conversation with pagination support";

    protected override string GetSuccessResponse() => "Returns the paginated list of messages";
}