using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.Errors;

/// <summary>
/// Domain-specific errors for the Chat module
/// </summary>
/// <summary>
/// Domain-specific errors for the Chat module
/// </summary>
public static class ChatErrors
{
    /// <summary>
    /// Errors related to message processing
    /// </summary>
    public static class Message
    {
        public static Error Empty => Error.Validation(
            "Message content cannot be empty", 
            "CHAT_MESSAGE_EMPTY");
            
        public static Error TooLong(int maxLength) => Error.Validation(
            $"Message content cannot exceed {maxLength} characters", 
            "CHAT_MESSAGE_TOO_LONG");
            
        public static Error InvalidFormat => Error.Validation(
            "Message content contains invalid characters or formatting", 
            "CHAT_MESSAGE_INVALID_FORMAT");
    }
    
    /// <summary>
    /// Errors related to MCP server operations
    /// </summary>
    public static class McpServer
    {
        public static Error InvalidUrl => Error.Validation(
            "MCP server URL must be a valid HTTPS URL", 
            "CHAT_MCP_INVALID_URL");
            
        public static Error Unreachable(string serverUrl) => Error.ExternalService(
            $"MCP server at {serverUrl} is unreachable", 
            "CHAT_MCP_UNREACHABLE");
            
        public static Error AuthenticationFailed(string serverUrl) => Error.ExternalService(
            $"Authentication failed for MCP server at {serverUrl}", 
            "CHAT_MCP_AUTH_FAILED");
            
        public static Error ToolNotFound(string toolName) => Error.NotFound(
            $"Tool '{toolName}' not found on MCP server", 
            "CHAT_MCP_TOOL_NOT_FOUND");
            
        public static Error ToolExecutionFailed(string toolName, string reason) => Error.ExternalService(
            $"Tool '{toolName}' execution failed: {reason}", 
            "CHAT_MCP_TOOL_EXECUTION_FAILED");
    }
    
    /// <summary>
    /// Errors related to AI client operations
    /// </summary>
    public static class AiClient
    {
        public static Error Unavailable => Error.ExternalService(
            "AI client is currently unavailable", 
            "CHAT_AI_CLIENT_UNAVAILABLE");
            
        public static Error RateLimited => Error.ExternalService(
            "AI client rate limit exceeded", 
            "CHAT_AI_CLIENT_RATE_LIMITED");
            
        public static Error InvalidResponse => Error.ExternalService(
            "AI client returned an invalid response", 
            "CHAT_AI_CLIENT_INVALID_RESPONSE");
            
        public static Error ProcessingTimeout => Error.ExternalService(
            "AI client processing timeout exceeded", 
            "CHAT_AI_CLIENT_TIMEOUT");
    }
    
    /// <summary>
    /// Errors related to conversations - London School TDD domain invariants
    /// </summary>
    public static class Conversation
    {
        public static Error NotFound(string conversationId) => Error.NotFound(
            $"Conversation '{conversationId}' not found", 
            "CHAT_CONVERSATION_NOT_FOUND");
            
        public static Error InvalidContext => Error.Validation(
            "Conversation context is invalid or corrupted", 
            "CHAT_CONVERSATION_INVALID_CONTEXT");

        /// <summary>
        /// Error when attempting to perform operations without specifying the conversation owner
        /// </summary>
        public static Error OwnerRequired => Error.Validation(
            "Conversation owner must be specified", 
            "CHAT_CONVERSATION_OWNER_REQUIRED");

        /// <summary>
        /// Error when message content violates domain rules
        /// </summary>
        public static Error InvalidMessageContent(string reason) => Error.Validation(
            $"Invalid message content: {reason}", 
            "CHAT_CONVERSATION_INVALID_MESSAGE_CONTENT");

        /// <summary>
        /// Error when message sequence is out of order or invalid
        /// </summary>
        public static Error SequenceViolation(int expected, int actual) => Error.Validation(
            $"Message sequence violation: expected {expected}, but got {actual}", 
            "CHAT_CONVERSATION_SEQUENCE_VIOLATION");

        /// <summary>
        /// Error when user attempts to access conversation they don't own
        /// </summary>
        public static Error NotConversationOwner(string userId, string conversationId) => Error.Forbidden(
            $"User '{userId}' is not the owner of conversation '{conversationId}'", 
            "CHAT_CONVERSATION_NOT_OWNER");

        /// <summary>
        /// Error when attempting to start a conversation without proper initialization
        /// </summary>
        public static Error CannotStart(string reason) => Error.Validation(
            $"Cannot start conversation: {reason}", 
            "CHAT_CONVERSATION_CANNOT_START");

        /// <summary>
        /// Error when attempting to add messages to an inactive conversation
        /// </summary>
        public static Error ConversationNotActive => Error.Validation(
            "Cannot add messages to inactive conversation", 
            "CHAT_CONVERSATION_NOT_ACTIVE");
    }
}