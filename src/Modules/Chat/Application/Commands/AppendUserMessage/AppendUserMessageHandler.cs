// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageHandler.cs
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Abstractions.Authentication;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Commands;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Rules;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Appends a user message and orchestrates the assistant reply processing.
/// Idempotency is handled by pipeline behavior, not here.
/// </summary>
public sealed class AppendUserMessageHandler : BaseChatIdempotentCommandHandler<AppendUserMessageCommand, ProcessMessageResponse>
{
    public AppendUserMessageHandler(
        ICurrentUserService currentUserService,
        IConversationRepository repository,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider,
        ILogger<AppendUserMessageHandler> logger)
        : base(currentUserService, repository, messageOrchestrator, timeProvider, logger)
    {
    }

    public override async Task<Result<ProcessMessageResponse, Error>> Handle(
        AppendUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing append user message command for conversation {ConversationId}",
            command.ConversationId.Value);

        var conversationResult = await LoadAndValidateConversationAsync(
            command.ConversationId, 
            GetAuthenticatedUserId(), 
            cancellationToken);

        return await conversationResult
            .Bind(conversation => ProcessUserMessageAsync(conversation, command.Content, cancellationToken));
    }
}
