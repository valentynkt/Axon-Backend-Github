using Axon.Api.Contracts.V1.Chat;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesEndpoint(IMediator mediator, ILogger<GetConversationMessagesEndpoint> logger)
    : BaseQueryEndpoint<GetConversationMessagesRequestDto, GetConversationMessagesResponseDto>(logger)
{
    private readonly IMediator _mediator = mediator;

    protected override string GetRoute() => "/api/v1/conversations/{conversationId}/messages";

    protected override string[] GetTags() => ["Chat"];

    protected override Action<EndpointSummary> GetSummary() => s =>
    {
        s.Summary = "Get paginated messages for a conversation";
        s.Description = "Retrieves messages from a specific conversation with pagination support";
        s.Responses[200] = "Returns the paginated list of messages";
        s.Responses[400] = "Invalid request parameters";
        s.Responses[401] = "User not authenticated";
        s.Responses[403] = "User does not own the conversation";
        s.Responses[404] = "Conversation not found";
        s.Responses[500] = "Internal server error";
    };

    protected override async Task<Result<GetConversationMessagesResponseDto, Error>> HandleQueryAsync(
        GetConversationMessagesRequestDto request, 
        CancellationToken ct)
    {
        // This is a placeholder - the actual implementation should use MediatR to send the query
        // For now, returning a placeholder response
        return Result.Success<GetConversationMessagesResponseDto, Error>(
            new GetConversationMessagesResponseDto(
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