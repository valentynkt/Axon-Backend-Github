using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Command to start a new conversation with optional title
/// </summary>
public sealed record StartConversationCommand(string? Title) : CommandBase<StartConversationResponse>;