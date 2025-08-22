// /Users/valentynkit/Repos/Axon-Backend/src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs
#nullable enable
using Axon.Api.Contracts.Chat;
using Axon.Api.Mappers;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Mappers;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Endpoints.Chat;

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
    private readonly IRequestMapper<ChatTurnRequestDto, ProcessMessageRequest> _requestMapper;
    private readonly IResponseMapper<ProcessMessageResponse, ChatTurnResponseDto> _responseMapper;

    public ChatTurnEndpoint(
        IChatCommandDispatcher dispatcher,
        IRequestMapper<ChatTurnRequestDto, ProcessMessageRequest> requestMapper,
        IResponseMapper<ProcessMessageResponse, ChatTurnResponseDto> responseMapper,
        ILogger<ChatTurnEndpoint> logger) : base(logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _requestMapper = requestMapper ?? throw new ArgumentNullException(nameof(requestMapper));
        _responseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
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
        // 1) API → Application mapping
        var appReqResult = await _requestMapper.MapAsync(request, ct);
        if (appReqResult.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(appReqResult.Error);

        // 2) Business flow via dispatcher (Application layer)
        var appResResult = await _dispatcher.ProcessMessageAsync(appReqResult.Value, ct);
        if (appResResult.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(appResResult.Error);

        // 3) Application → API mapping
        var apiResResult = await _responseMapper.MapAsync(appResResult.Value, ct);
        return apiResResult;
    }
}
