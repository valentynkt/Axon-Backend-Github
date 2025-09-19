using Axon.BuildingBlocks.Core.Constants;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Centralized constants for Chat Domain tests.
/// Provides reusable test data, limits, and configuration values.
/// Synchronized with actual domain constants from ChatPrimitiveConstants.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Test data for users and ownership
    /// </summary>
    public static class Users
    {
        public static readonly AxonUserId DefaultOwnerId = AxonUserId.New();
        public static readonly AxonUserId AlternativeOwnerId = AxonUserId.New();
        public static readonly AxonUserId ThirdOwnerId = AxonUserId.New();
        
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
        // These should be exactly the right lengths based on actual domain constants
        public static readonly string MaxLengthTitle = new('a', Limits.MaxConversationTitleLength);
        public static readonly string TooLongTitle = new('a', Limits.MaxConversationTitleLength + 1);
        
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
        public static readonly AiResponseId DefaultAiResponseId = new AiResponseId("ai-response-123");
        public static readonly AiResponseId AlternativeAiResponseId = new AiResponseId("ai-response-456");
        public static readonly AiResponseId ThirdAiResponseId = new AiResponseId("ai-response-789");
        
        public const string DefaultAiResponseIdString = "ai-response-123";
        public const string AlternativeAiResponseIdString = "ai-response-456";
        public const string ThirdAiResponseIdString = "ai-response-789";
    }

    /// <summary>
    /// Test limits and constraints - synchronized with actual domain constants
    /// </summary>
    public static class Limits
    {
        public const int MaxConversationMessages = ChatPrimitiveConstants.ConversationDefault.MaxMessages;
        public const int MaxMessageContentLength = ChatPrimitiveConstants.MessageContentDefault.MaxLength;
        public const int MaxMessageLength = ChatPrimitiveConstants.MessageContentDefault.MaxLength; // Alias for compatibility
        public const int MaxConversationTitleLength = ChatPrimitiveConstants.ConversationTitleDefault.MaxLength;
        public const int ContentPreviewLength = ChatPrimitiveConstants.ConversationDefault.ContentPreviewLength;
        public const int MinAiResponseIdLength = ChatPrimitiveConstants.AiResponseIdDefault.MinLength;
        public const int MaxAiResponseIdLength = ChatPrimitiveConstants.AiResponseIdDefault.MaxLength;
    }

    /// <summary>
    /// Test scenarios for edge cases - precise boundary testing
    /// </summary>
    public static class EdgeCases
    {
        // Title edge cases
        public static readonly string OneBelowMaxTitle = new('a', Limits.MaxConversationTitleLength - 1);
        public static readonly string ExactMaxTitle = new('a', Limits.MaxConversationTitleLength);
        public static readonly string OneOverMaxTitle = new('a', Limits.MaxConversationTitleLength + 1);
        
        // Message content edge cases  
        public static readonly string OneBelowMaxMessage = new('a', Limits.MaxMessageContentLength - 1);
        public static readonly string ExactMaxMessage = new('a', Limits.MaxMessageContentLength);
        public static readonly string OneOverMaxMessage = new('a', Limits.MaxMessageContentLength + 1);
        
        // AI Response ID edge cases
        public static readonly string MinAiResponseId = new('x', Limits.MinAiResponseIdLength);
        public static readonly string MaxAiResponseId = new('y', Limits.MaxAiResponseIdLength);
        public static readonly string TooLongAiResponseId = new('z', Limits.MaxAiResponseIdLength + 1);
        public static readonly string EmptyAiResponseId = string.Empty;
    }

    /// <summary>
    /// DateTime constants for testing
    /// </summary>
    public static class DateTimes
    {
        public static readonly DateTimeOffset DefaultTestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset AlternativeTestTime = new(2024, 2, 20, 14, 45, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset FutureTestTime = new(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);
        public static readonly DateTimeOffset PastTestTime = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Error codes and messages for testing error scenarios
    /// </summary>
    public static class ErrorCodes
    {
        public const string ChatRoleInvalid = "CHAT.ROLE.INVALID";
        public const string ChatMessageEmpty = "CHAT.MESSAGE.EMPTY";
        public const string ChatMessageInvalid = "CHAT.MESSAGE.INVALID";
        public const string ChatConversationTitleRequired = "CHAT.CONVERSATION.TITLE.REQUIRED";
        public const string ChatConversationTitleInvalid = "CHAT.CONVERSATION.TITLE.INVALID";
    }

    /// <summary>
    /// Common error messages for testing
    /// </summary>
    public static class ErrorMessages
    {
        public const string InvalidMessageRole = "Invalid message role (allowed: user, assistant).";
        public const string EmptyMessageContent = "Message content cannot be empty or whitespace.";
        public const string TitleCannotBeEmpty = "Title cannot be empty.";
        public const string ConversationMustBeActive = "Conversation must be active to accept new messages.";
        public const string UserMustFollowAssistant = "User message must follow assistant message (turn-taking rule).";
        public const string AssistantMustFollowUser = "Assistant message must follow user message (turn-taking rule).";
    }

    /// <summary>
    /// Specifications test data
    /// </summary>
    public static class Specifications
    {
        public const string SearchTerm = "test";
        public const string TitleContains = "conversation";
        public const int MinimumMessages = 5;
        public static readonly DateTimeOffset CreatedAfter = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset CreatedBefore = new(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);
        public static readonly DateTimeOffset UpdatedSince = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Business rule test scenarios
    /// </summary>
    public static class BusinessRules
    {
        public const int ValidSequence = 1;
        public const int InvalidSequence = 0;
        public const int ValidMessageCount = 1;
        public const int MaxValidMessageCount = Limits.MaxConversationMessages;
        public const int ExceededMessageCount = Limits.MaxConversationMessages + 1;
    }
}