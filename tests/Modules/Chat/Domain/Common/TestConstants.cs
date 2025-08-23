namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Centralized constants for Chat Domain tests.
/// Provides reusable test data, limits, and configuration values.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Test data for users and ownership
    /// </summary>
    public static class Users
    {
        public static readonly UserId DefaultOwnerId = UserId.New();
        public static readonly UserId AlternativeOwnerId = UserId.New();
        public static readonly UserId ThirdOwnerId = UserId.New();
        
        public const string DefaultOwnerIdString = "test-user-1";
        public const string AlternativeOwnerIdString = "test-user-2";
        public const string ThirdOwnerIdString = "test-user-3";
    }

    /// <summary>
    /// Test data for conversations
    /// </summary>
    public static class Conversations
    {
        public const string DefaultTitle = "Test Conversation";
        public const string AlternativeTitle = "Another Test Conversation";
        public const string LongTitle = "This is a very long conversation title that approaches the maximum limit";
        public const string MaxLengthTitle = "This is exactly 120 characters long title that reaches the maximum allowed length for conversation titles in our system.";
        public const string TooLongTitle = "This is exactly 121 characters long title that exceeds the maximum allowed length for conversation titles in our system!";
        
        public static readonly string EmptyTitle = string.Empty;
        public static readonly string WhitespaceTitle = "   ";
        public static readonly string ValidTitleWithSpaces = "  Valid Title  ";
        public static readonly string ExpectedTrimmedTitle = "Valid Title";
    }

    /// <summary>
    /// Test data for messages
    /// </summary>
    public static class Messages
    {
        public const string DefaultUserMessage = "Hello, this is a test user message.";
        public const string DefaultAssistantMessage = "Hello, this is a test assistant response.";
        public const string ShortMessage = "Hi";
        public const string LongMessage = "This is a very long message that contains a lot of text to test various scenarios with longer content.";
        
        public static readonly string MaxLengthMessage = new('a', 16_000);
        public static readonly string TooLongMessage = new('b', 16_001);
        public static readonly string EmptyMessage = string.Empty;
        public static readonly string WhitespaceMessage = "   ";
        public static readonly string ValidMessageWithSpaces = "  Valid Message  ";
        public static readonly string ExpectedTrimmedMessage = "Valid Message";
    }

    /// <summary>
    /// Test data for AI responses
    /// </summary>
    public static class AiResponses
    {
        public static readonly AiResponseId DefaultAiResponseId = AiResponseId.From("ai-response-123");
        public static readonly AiResponseId AlternativeAiResponseId = AiResponseId.From("ai-response-456");
        public static readonly AiResponseId ThirdAiResponseId = AiResponseId.From("ai-response-789");
        
        public const string DefaultAiResponseIdString = "ai-response-123";
        public const string AlternativeAiResponseIdString = "ai-response-456";
        public const string ThirdAiResponseIdString = "ai-response-789";
    }

    /// <summary>
    /// Test limits and constraints
    /// </summary>
    public static class Limits
    {
        public const int MaxConversationMessages = 10_000;
        public const int MaxMessageContentLength = 16_000;
        public const int MaxConversationTitleLength = 120;
        public const int ContentPreviewLength = 100;
    }

    /// <summary>
    /// Test scenarios for edge cases
    /// </summary>
    public static class EdgeCases
    {
        public static readonly string OneBelowMaxTitle = new('a', TestConstants.Limits.MaxConversationTitleLength - 1);
        public static readonly string ExactMaxTitle = new('a', TestConstants.Limits.MaxConversationTitleLength);
        public static readonly string OneOverMaxTitle = new('a', TestConstants.Limits.MaxConversationTitleLength + 1);
        
        public static readonly string OneBelowMaxMessage = new('a', TestConstants.Limits.MaxMessageContentLength - 1);
        public static readonly string ExactMaxMessage = new('a', TestConstants.Limits.MaxMessageContentLength);
        public static readonly string OneOverMaxMessage = new('a', TestConstants.Limits.MaxMessageContentLength + 1);
    }

    /// <summary>
    /// DateTime constants for testing
    /// </summary>
    public static class DateTimes
    {
        public static readonly DateTimeOffset DefaultTestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset AlternativeTestTime = new(2024, 2, 20, 14, 45, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset FutureTestTime = new(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);
    }
}