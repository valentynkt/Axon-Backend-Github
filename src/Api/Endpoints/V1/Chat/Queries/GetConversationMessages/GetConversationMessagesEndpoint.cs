using Axon.Api.Contracts.V1.Chat;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesEndpoint : BaseResultEndpoint<GetConversationMessagesRequestDto, GetConversationMessagesResponseDto>
{
    private readonly IMediator _mediator;

    public GetConversationMessagesEndpoint(IMediator mediator, ILogger<GetConversationMessagesEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Get("/api/v1/chat/conversations/{conversationId}/messages");
        Policies("DynamicOrAxon");

        Summary(s =>
        {
            s.Summary = "Get paginated messages for a conversation";
            s.Description = "Retrieves messages from a specific conversation with pagination support";
            s.Responses[200] = "Returns the paginated list of messages";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[404] = "Resource not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Chat");
    }

    protected override async Task<Result<GetConversationMessagesResponseDto, Error>> ExecuteAsync(
        GetConversationMessagesRequestDto request,
        CancellationToken ct)
    {
        // Map request to query using Mapster
        var queryResult = request.AdaptSafely<GetConversationMessagesQuery>();
        if (queryResult.IsFailure)
            return Result.Failure<GetConversationMessagesResponseDto, Error>(queryResult.Error);

        var domainResult = await _mediator.Send(queryResult.Value, ct);
        if (domainResult.IsFailure)
            return Result.Failure<GetConversationMessagesResponseDto, Error>(domainResult.Error);

        // Map domain result to response using Mapster
        var responseResult = domainResult.Value.AdaptSafely<GetConversationMessagesResponseDto>();
        return responseResult;
    }
}
