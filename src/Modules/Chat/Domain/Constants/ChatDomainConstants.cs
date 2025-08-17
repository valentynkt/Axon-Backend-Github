namespace Axon.Modules.Chat.Domain.Constants;

/// <summary>
/// Central location for Chat domain constants to avoid magic numbers.
/// </summary>
internal static class ChatDomainConstants
{
    /// <summary>
    /// Message content limits
    /// </summary>
    public static class MessageContent
    {
        /// <summary>
        /// Maximum length for message content in characters
        /// </summary>
        public const int MaxLength = 100_000;
    }
    
    /// <summary>
    /// Conversation title limits  
    /// </summary>
    public static class ConversationTitle
    {
        /// <summary>
        /// Maximum length for conversation title in characters
        /// </summary>
        public const int MaxLength = 200;
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