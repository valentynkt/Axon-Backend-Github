using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using Mapster;

namespace Axon.Api.Endpoints.V1.Chat.Commands.ChatTurn;

/// <summary>
/// Universal chat endpoint (one turn). If ConversationId is null → starts a new conversation; otherwise appends.
/// Uses mappers for clean API↔Application separation and dispatcher for business flow.
/// </summary>
public sealed class ChatTurnEndpoint
    : BaseResultEndpoint<ChatTurnRequestDto, ChatTurnResponseDto>
{
    private const string Route = "/api/v1/chat/turns";
    private const string Tag = "Chat";

    private readonly IChatCommandDispatcher _dispatcher;

    public ChatTurnEndpoint(
        IChatCommandDispatcher dispatcher,
        ILogger<ChatTurnEndpoint> logger) : base(logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public override void Configure()
    {
        Post(Route);
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Send a chat message (start or continue a conversation).";
            s.Description =
                "If 'conversationId' is omitted, a new conversation starts; otherwise the message is appended.";
            s.ExampleRequest = new ChatTurnRequestDto
            {
                ConversationId = null,
                Message = "Hello! What can you do?"
            };
            s.Responses[200] = "Message processed; assistant response returned.";
            s.Responses[400] = "Bad request (validation).";
            s.Responses[422] = "Business rule violation.";
            s.Responses[500] = "Processing failed (server error).";
        });

        Tags(Tag);
    }

    protected override async Task<Result<ChatTurnResponseDto, Error>> ExecuteAsync(
        ChatTurnRequestDto request,
        CancellationToken ct)
    {
        // 1) API → Application mapping using Mapster
        var command = request.Adapt<ProcessMessageRequest>();

        // 2) Business flow via dispatcher (Application layer)
        var appResResult = await _dispatcher.ProcessMessageAsync(command, ct);
        if (appResResult.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(appResResult.Error);

        // 3) Application → API mapping using Mapster
        var response = appResResult.Value.Adapt<ChatTurnResponseDto>();
        return Result.Success<ChatTurnResponseDto, Error>(response);
    }
}
