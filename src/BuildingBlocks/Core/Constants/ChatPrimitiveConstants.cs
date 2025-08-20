namespace Axon.BuildingBlocks.Core.Constants;

/// <summary>
/// Constants used by Chat Primitive Value Objects.
/// Centralized location for all validation limits and constraints.
/// </summary>
public static class ChatPrimitiveConstants
{
    /// <summary>
    /// Message content limits
    /// </summary>
    public static class MessageContent
    {
        /// <summary>
        /// Maximum length for message content in characters
        /// </summary>
        public const int MaxLength = 16_000;
    }
    
    /// <summary>
    /// Conversation title limits  
    /// </summary>
    public static class ConversationTitle
    {
        /// <summary>
        /// Maximum length for conversation title in characters
        /// </summary>
        public const int MaxLength = 120;
    }
    
    /// <summary>
    /// AI Response ID limits
    /// </summary>
    public static class AiResponseId
    {
        /// <summary>
        /// Maximum length for AI response ID in characters
        /// </summary>
        public const int MaxLength = 128;
        
        /// <summary>
        /// Minimum length for AI response ID in characters
        /// </summary>
        public const int MinLength = 1;
    }
    
    /// <summary>
    /// Conversation limits
    /// </summary>
    public static class Conversation
    {
        /// <summary>
        /// Maximum number of messages per conversation
        /// </summary>
        public const int MaxMessages = 10_000;
        
        /// <summary>
        /// Length for content preview in characters
        /// </summary>
        public const int ContentPreviewLength = 100;
    }
}