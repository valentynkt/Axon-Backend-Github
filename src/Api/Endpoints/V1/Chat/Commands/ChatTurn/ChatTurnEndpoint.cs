using Axon.Api.Contracts.V1.Chat;
using Axon.Api.Modules;
using Axon.Modules.Chat.Application.Contracts.Dispatching;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Api.Endpoints.V1.Chat.Commands.ChatTurn;

/// <summary>
/// Universal chat endpoint (one turn). If ConversationId is null → starts a new conversation; otherwise appends.
/// Uses Mapster for clean API↔Application separation and dispatcher for business flow.
/// </summary>
public sealed class ChatTurnEndpoint(IChatCommandDispatcher dispatcher, ILogger<ChatTurnEndpoint> logger)
    : BaseChatCommandEndpoint<ChatTurnRequestDto, ChatTurnResponseDto, ProcessMessageRequest, ProcessMessageResponse>(logger)
{
    private readonly IChatCommandDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    protected override string GetRoute() => "/api/v1/chat/turns";

    protected override string GetSummary() => "Send a chat message (start or continue a conversation)";

    protected override string GetDescription() => "If 'conversationId' is omitted, a new conversation starts; otherwise the message is appended";

    protected override string GetSuccessResponse() => "Message processed; assistant response returned";

    protected override async Task<Result<ProcessMessageResponse, Error>> ExecuteCommand(ProcessMessageRequest command, CancellationToken ct)
    {
        return await _dispatcher.ProcessMessageAsync(command, ct);
    }
}
