// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/StartConversation/StartConversationHandler.cs
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
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Starts a new conversation for the current user, appends the user's first message,
/// and orchestrates the AI processing and assistant reply. Returns a unified response.
/// Idempotency is handled by pipeline behavior.
/// </summary>
public sealed class StartConversationHandler : BaseChatIdempotentCommandHandler<StartConversationCommand, ProcessMessageResponse>
{
    public StartConversationHandler(
        ICurrentUserService currentUserService,
        IConversationRepository repository,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider,
        ILogger<StartConversationHandler> logger)
        : base(currentUserService, repository, messageOrchestrator, timeProvider, logger)
    {
    }

    public override async Task<Result<ProcessMessageResponse, Error>> Handle(
        StartConversationCommand command,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing start conversation command");

        var authResult = await GetAuthenticatedAxonUserIdAsync(cancellationToken);
        if (authResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(authResult.Error);

        var conversationResult = await CreateConversationAsync(
            authResult.Value,
            cancellationToken);

        return await conversationResult
            .Bind(conversation => ProcessUserMessageAsync(conversation, command.Message, cancellationToken));
    }
}
