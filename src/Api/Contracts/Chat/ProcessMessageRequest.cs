namespace Axon.Api.Contracts.Chat;

/// <summary>
/// HTTP request for chat message processing
/// </summary>
public sealed record ProcessMessageRequest(
    string Message,
    string? ConversationId = null);