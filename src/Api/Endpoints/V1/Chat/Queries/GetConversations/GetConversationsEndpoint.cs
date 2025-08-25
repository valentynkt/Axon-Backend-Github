using Axon.Api.Contracts.V1.Chat;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

internal sealed class GetConversationsEndpoint(IMediator mediator, ILogger<GetConversationsEndpoint> logger)
    : BaseQueryEndpoint<GetConversationsRequestDto, GetConversationsResponseDto>(logger)
{
    private readonly IMediator _mediator = mediator;

    protected override string GetRoute() => "/api/v1/conversations";

    protected override string[] GetTags() => ["Chat"];

    protected override Action<EndpointSummary> GetSummary() => s =>
    {
        s.Summary = "Get paginated list of conversations";
        s.Description = "Retrieves a paginated list of conversations with optional sorting and filtering";
        s.Responses[200] = "Returns the paginated list of conversations";
        s.Responses[400] = "Invalid request parameters";
        s.Responses[500] = "Internal server error";
    };

    protected override async Task<Result<GetConversationsResponseDto, Error>> HandleQueryAsync(
        GetConversationsRequestDto request, 
        CancellationToken ct)
    {
        // This is a placeholder - the actual implementation should use MediatR to send the query
        // For now, returning a placeholder response
        return Result.Success<GetConversationsResponseDto, Error>(
            new GetConversationsResponseDto(
                Items: [],
                PageNumber: request.PageNumber ?? 1,
                PageSize: request.PageSize ?? 20,
                TotalCount: 0,
                TotalPages: 0,
                HasPrevious: false,
                HasNext: false,
                Count: 0,
                IsEmpty: true,
                FirstItemIndex: 0,
                LastItemIndex: 0
            ));
    }
}