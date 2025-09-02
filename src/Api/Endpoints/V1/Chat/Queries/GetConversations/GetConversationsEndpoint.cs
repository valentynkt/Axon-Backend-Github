using Axon.Api.Contracts.V1.Chat;
using Axon.Api.Modules;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using BuildingBlocks.Web.Endpoints.Base;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

internal sealed class GetConversationsEndpoint(IMediator mediator, ILogger<GetConversationsEndpoint> logger)
    : BaseChatQueryEndpoint<GetConversationsRequestDto, GetConversationsResponseDto, GetConversationsQuery, Paged<ConversationListItem>>(mediator, logger)
{
    protected override string GetRoute() => "/api/v1/conversations";

    protected override string GetSummary() => "Get paginated list of conversations";

    protected override string GetDescription() => "Retrieves a paginated list of conversations with optional sorting and filtering";

    protected override string GetSuccessResponse() => "Returns the paginated list of conversations";
}