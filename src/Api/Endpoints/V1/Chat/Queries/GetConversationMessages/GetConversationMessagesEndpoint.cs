using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversationMessages;

internal sealed class GetConversationMessagesEndpoint(IMediator mediator, ILogger<GetConversationMessagesEndpoint> logger)
    : BaseMappedEndpoint<GetConversationMessagesRequestDto, GetConversationMessagesResponseDto>(logger)
{
    private readonly IMediator _mediator = mediator;
    
    public override void Configure()
    {
        Get("/api/v1/conversations/{conversationId}/messages");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Get paginated messages for a conversation";
            s.Description = "Retrieves messages from a specific conversation with pagination support";
            s.Responses[200] = "Returns the paginated list of messages";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not own the conversation";
            s.Responses[404] = "Conversation not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Chat");
    }

    protected override async Task<Result<GetConversationMessagesResponseDto, Error>> ExecuteAsync(GetConversationMessagesRequestDto request, CancellationToken ct)
    {
        return await MapExecuteMap<GetConversationMessagesQuery, Paged<ConversationMessageItem>>(
            request,
            (query, cancellationToken) => _mediator.Send(query, cancellationToken),
            ct);
    }
}