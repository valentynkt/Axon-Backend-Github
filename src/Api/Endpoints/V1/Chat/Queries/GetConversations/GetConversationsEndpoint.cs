using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

internal sealed class GetConversationsEndpoint(IMediator mediator, ILogger<GetConversationsEndpoint> logger)
    : BaseMappedEndpoint<GetConversationsRequestDto, GetConversationsResponseDto>(logger)
{
    private readonly IMediator _mediator = mediator;

    public override void Configure()
    {
        Get("/api/v1/conversations");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get paginated list of conversations";
            s.Description = "Retrieves a paginated list of conversations with optional sorting and filtering";
            s.Responses[200] = "Returns the paginated list of conversations";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[500] = "Internal server error";
        });

        Tags("Chat");
    }

    protected override async Task<Result<GetConversationsResponseDto, Error>> ExecuteAsync(
        GetConversationsRequestDto request,
        CancellationToken ct)
    {
        // Use fluent mapping chain: Request → Query → Execute → Response
        return await MapExecuteMap<GetConversationsQuery, Paged<ConversationListItem>>(
            request,
            (query, cancellationToken) => _mediator.Send(query, cancellationToken),
            ct);
    }
}