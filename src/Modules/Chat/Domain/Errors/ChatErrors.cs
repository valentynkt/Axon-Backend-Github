using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Domain.Errors;

/// <summary>
/// Domain errors for the Chat module
/// </summary>
public static class ChatErrors
{
    public static class AiClient
    {
        public static Error Unavailable => 
            Error.Unavailable("The AI service is currently unavailable", "Chat.AiClient.Unavailable");
        
        public static Error ProcessingTimeout => 
            Error.Timeout("The AI request processing timed out", "Chat.AiClient.ProcessingTimeout");
        
        public static Error InvalidResponse => 
            Error.Serialization("The AI service returned an invalid response", "Chat.AiClient.InvalidResponse");
        
        public static Error InvalidConfiguration => 
            Error.Configuration("The AI service configuration is invalid", "Chat.AiClient.InvalidConfiguration");
        
        public static Error RateLimited => 
            Error.RateLimit("Too many requests to the AI service", "Chat.AiClient.RateLimited");
    }
    
    public static class Message
    {
        public static Error InvalidContent => 
            Error.Validation("The message content is invalid", "Chat.Message.InvalidContent");
        
        public static Error TooLong => 
            Error.Validation("The message content exceeds maximum length", "Chat.Message.TooLong");
        
        public static Error Empty => 
            Error.Validation("The message content cannot be empty", "Chat.Message.Empty");
    }
    
    public static class Conversation
    {
        public static Error NotFound => 
            Error.NotFound("The conversation was not found", "Chat.Conversation.NotFound");
        
        public static Error AlreadyExists => 
            Error.Conflict("A conversation with this ID already exists", "Chat.Conversation.AlreadyExists");
        
        public static Error InvalidState => 
            Error.PreconditionFailed("The conversation is in an invalid state for this operation", "Chat.Conversation.InvalidState");
    }
}