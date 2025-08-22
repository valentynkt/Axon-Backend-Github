#nullable enable
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Mappers;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Mappers;

/// <summary>
/// Maps API ChatTurnRequestDto -> Application ProcessMessageRequest
/// </summary>
public sealed class ChatTurnRequestMapper
    : BaseMapper, IRequestMapper<ChatTurnRequestDto, ProcessMessageRequest>
{
    public ChatTurnRequestMapper(ILogger<ChatTurnRequestMapper> logger) : base(logger) { }

    public Task<Result<ProcessMessageRequest, Error>> MapAsync(
        ChatTurnRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var appReq = new ProcessMessageRequest
        {
            ConversationId = request.ConversationId,
            Message = request.Message
        };

        return Task.FromResult(Result.Success<ProcessMessageRequest, Error>(appReq));
    }
}