using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Contracts.Dispatching;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using FastEndpoints;

namespace Axon.Api.Endpoints.V1.Chat.Commands.ChatTurn;

/// <summary>
/// Universal chat endpoint (one turn). If ConversationId is null → starts a new conversation; otherwise appends.
/// Uses Mapster for clean API↔Application separation and dispatcher for business flow.
/// </summary>
public sealed class ChatTurnEndpoint : BaseResultEndpoint<ChatTurnRequestDto, ChatTurnResponseDto>
{
    private readonly IChatCommandDispatcher _dispatcher;

    public ChatTurnEndpoint(IChatCommandDispatcher dispatcher, ILogger<ChatTurnEndpoint> logger)
        : base(logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public override void Configure()
    {
        Post("/api/v1/chat/turns");
        Policies("DynamicOrAxon");  // Accept both Dynamic and Axon tokens

        Summary(s =>
        {
            s.Summary = "Send a chat message (start or continue a conversation)";
            s.Description = "If 'conversationId' is omitted, a new conversation starts; otherwise the message is appended";
            s.Responses[200] = "Message processed; assistant response returned";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[422] = "Business rule violation";
            s.Responses[500] = "Internal server error";
        });

        Tags("Chat");
    }

    protected override async Task<Result<ChatTurnResponseDto, Error>> ExecuteAsync(
        ChatTurnRequestDto request,
        CancellationToken ct)
    {
        // Map request to command using Mapster
        var commandResult = request.AdaptSafely<ProcessMessageRequest>();
        if (commandResult.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(commandResult.Error);

        // Execute via dispatcher (NOT MediatR)
        var domainResult = await _dispatcher.ProcessMessageAsync(commandResult.Value, ct);
        if (domainResult.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(domainResult.Error);

        // Map domain result to response using Mapster
        var responseResult = domainResult.Value.AdaptSafely<ChatTurnResponseDto>();
        return responseResult;
    }
}
