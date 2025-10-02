using Axon.Api.Contracts.V1.Chat;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

internal sealed class GetConversationsEndpoint : BaseResultEndpoint<GetConversationsRequestDto, GetConversationsResponseDto>
{
    private readonly IMediator _mediator;

    public GetConversationsEndpoint(IMediator mediator, ILogger<GetConversationsEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Get("/api/v1/chat/conversations");
        Policies("DynamicOrAxon");

        Summary(s =>
        {
            s.Summary = "Get paginated list of conversations";
            s.Description = "Retrieves a paginated list of conversations with optional sorting and filtering";
            s.Responses[200] = "Returns the paginated list of conversations";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[404] = "Resource not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Chat");
    }

    protected override async Task<Result<GetConversationsResponseDto, Error>> ExecuteAsync(
        GetConversationsRequestDto request,
        CancellationToken ct)
    {
        // Map request to query using Mapster
        var queryResult = request.AdaptSafely<GetConversationsQuery>();
        if (queryResult.IsFailure)
            return Result.Failure<GetConversationsResponseDto, Error>(queryResult.Error);

        var domainResult = await _mediator.Send(queryResult.Value, ct);
        if (domainResult.IsFailure)
            return Result.Failure<GetConversationsResponseDto, Error>(domainResult.Error);

        // Map domain result to response using Mapster
        var responseResult = domainResult.Value.AdaptSafely<GetConversationsResponseDto>();
        return responseResult;
    }
}
