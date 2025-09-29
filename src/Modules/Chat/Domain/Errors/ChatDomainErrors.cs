namespace Axon.Modules.Chat.Domain.Errors;

/// <summary>
/// Centralized error codes and messages for the Chat domain.
/// This ensures consistency across all layers and provides a single source of truth for domain errors.
/// </summary>
public static class ChatDomainErrors
{
    /// <summary>
    /// Authentication and authorization related errors.
    /// </summary>
    public static class Authentication
    {
        public const string UnauthenticatedCode = "CHAT.AUTH.UNAUTHENTICATED";
        public const string UnauthenticatedMessage = "User must be authenticated to perform chat operations.";
        
        public const string InvalidUserIdCode = "CHAT.AUTH.INVALID_USERID";
        public const string InvalidUserIdMessage = "Invalid current user id.";
    }

    /// <summary>
    /// Message content related errors.
    /// </summary>
    public static class Message
    {
        public const string InvalidContentCode = "CHAT.MESSAGE.INVALID";
        public const string InvalidContentMessage = "Message content is invalid.";
    }

    /// <summary>
    /// Conversation related errors.
    /// </summary>
    public static class Conversation
    {
        public const string NotFoundCode = "CHAT.CONVERSATION.NOT_FOUND";
        public const string NotFoundMessage = "Conversation not found.";
        
        public const string AccessDeniedCode = "CHAT.CONVERSATION.ACCESS_DENIED";
        public const string AccessDeniedMessage = "Conversation does not belong to the current user.";
        
        public const string IdEmptyCode = "CHAT.ID.EMPTY";
        public const string IdEmptyMessage = "ConversationId cannot be empty.";
    }

    /// <summary>
    /// AI processing related errors.
    /// </summary>
    public static class AiProcessing
    {
        public const string ProcessingFailedCode = "CHAT.AI.PROCESSING_FAILED";
        public const string ProcessingFailedMessage = "AI processing failed. Please retry.";
        
        public const string UnexpectedErrorCode = "CHAT.AI.UNEXPECTED_ERROR";
        public const string UnexpectedErrorMessage = "An unexpected error occurred during AI processing. Please retry.";
        
        public const string InvalidResponseIdCode = "CHAT.AI.INVALID_RESPONSE_ID";
        public const string InvalidResponseIdMessage = "AI response ID cannot be empty.";

        public const string NetworkErrorCode = "CHAT.AI.NETWORK_ERROR";
        public const string NetworkErrorMessage = "Network error occurred during AI processing.";

        public const string ServiceUnavailableCode = "CHAT.AI.SERVICE_UNAVAILABLE";
        public const string ServiceUnavailableMessage = "AI service is temporarily unavailable.";

        public const string TimeoutCode = "CHAT.AI.TIMEOUT";
        public const string TimeoutMessage = "AI processing request timed out.";
    }

    /// <summary>
    /// Message processing orchestration errors.
    /// </summary>
    public static class Processing
    {
        public const string UnexpectedErrorCode = "CHAT.MESSAGE_PROCESSING.UNEXPECTED_ERROR";
        public const string UnexpectedErrorMessage = "An unexpected error occurred during message processing. Please retry.";

        public const string PersistenceFailedCode = "CHAT.MESSAGE_PROCESSING.PERSISTENCE_FAILED";
        public const string PersistenceFailedMessage = "Failed to persist conversation updates.";

        public const string ConcurrencyConflictCode = "CHAT.MESSAGE_PROCESSING.CONCURRENCY_CONFLICT";
        public const string ConcurrencyConflictMessage = "Conversation was modified by another process.";

        public const string TimeoutCode = "CHAT.MESSAGE_PROCESSING.TIMEOUT";
        public const string TimeoutMessage = "Operation timed out during message processing.";

        public const string McpResolutionFailedCode = "CHAT.MESSAGE_PROCESSING.MCP_RESOLUTION_FAILED";
        public const string McpResolutionFailedMessage = "Failed to resolve MCP server configuration.";
    }

    /// <summary>
    /// General dispatch errors.
    /// </summary>
    public static class Dispatch
    {
        public const string UnexpectedErrorCode = "CHAT_DISPATCH_ERROR";
        public const string UnexpectedErrorMessage = "An unexpected error occurred while processing the message.";
        
        public const string InvalidConversationIdCode = "INVALID_CONVERSATION_ID";
        public const string InvalidConversationIdMessage = "The provided conversation ID is invalid.";
    }
}