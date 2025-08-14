 
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Command to start a new conversation
/// </summary>
public sealed record StartConversationCommand(
    string Title) : IRequest<Result<StartConversationResponse>>;