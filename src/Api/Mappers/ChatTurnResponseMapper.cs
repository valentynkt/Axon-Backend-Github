#nullable enable
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Mappers;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Mappers;

/// <summary>
/// Maps Application ProcessMessageResponse -> API ChatTurnResponseDto
/// </summary>
public sealed class ChatTurnResponseMapper
    : BaseMapper, IResponseMapper<ProcessMessageResponse, ChatTurnResponseDto>
{
    public ChatTurnResponseMapper(ILogger<ChatTurnResponseMapper> logger) : base(logger) { }

    public Task<Result<ChatTurnResponseDto, Error>> MapAsync(
        ProcessMessageResponse domainResult,
        CancellationToken cancellationToken = default)
    {
        var conversationId = domainResult.ConversationId.Value;
        var userMessageId = domainResult.UserMessageId.Value;
        var assistantMessageId = domainResult.AssistantMessageId.Value;
        var assistantMessage = domainResult.AssistantMessage.ToString()!;
        
        var api = new ChatTurnResponseDto(conversationId, userMessageId, assistantMessageId, assistantMessage);

        return Task.FromResult(Result.Success<ChatTurnResponseDto, Error>(api));
    }
}