using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Tests.Events._Fixtures;

/// <summary>
/// Test fixtures and data for domain event testing
/// </summary>
public static class EventTestFixture
{
    // Fixed clock for deterministic testing
    public static readonly FixedClock TestClock = new(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
    
    // Test identifiers
    public static readonly ConversationId TestConversationId = new(Guid.Parse("12345678-1234-5678-9abc-123456789012"));
    public static readonly UserId TestUserId = new(Guid.Parse("87654321-4321-8765-cba9-876543210987"));
    public static readonly MessageId TestMessageId1 = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly MessageId TestMessageId2 = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    
    // Content strings for preview testing
    public const string ShortContent = "Hello, world!"; // 13 chars
    public const string ExactlyHundredChars = "This is exactly one hundred characters long including spaces and punctuation marks right here."; // 100 chars
    public const string OnePastHundredChars = "This is exactly one hundred and one characters long including spaces and punctuation marks here!"; // 101 chars
    public const string VeryLongContent = "This is a very long message that exceeds one hundred characters by a significant amount. It contains multiple sentences and should be truncated at exactly one hundred characters when creating the content preview. The rest of this content should not appear in the preview.";
    
    // Title strings for testing
    public const string ValidTitle = "My Test Conversation";
    public const string LongTitle = "This is a very long title that is close to but under the 200 character limit for conversation titles in our domain model";
    public const string TwoHundredCharTitle = "This title is exactly two hundred characters long and should be at the maximum allowed length for conversation titles in our domain model. It is designed to test boundary conditions.";
    public const string TooLongTitle = "This title is longer than two hundred characters and should be rejected by the domain validation rules. It exceeds the maximum allowed length and will cause validation errors when used.";
    
    /// <summary>
    /// Creates a test conversation with the specified parameters
    /// </summary>
    public static Conversation CreateTestConversation(string? title = null, UserId? owner = null)
    {
        var ownerId = owner ?? TestUserId;
        var conversation = title != null 
            ? Conversation.Start(ownerId, TestClock, title).Value
            : Conversation.Start(ownerId, TestClock).Value;
            
        return conversation;
    }
    
    /// <summary>
    /// Creates a conversation with messages for testing completion scenarios
    /// </summary>
    public static Conversation CreateConversationWithMessages(int messageCount = 1)
    {
        var conversation = CreateTestConversation();
        
        for (int i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                conversation.AppendUserMessage($"User message {i + 1}", TestClock);
            }
            else
            {
                conversation.AppendAssistantMessage($"Assistant message {i + 1}", TestClock);
            }
        }
        
        return conversation;
    }
    
    /// <summary>
    /// Gets content preview following the exact rule: first 100 chars, no ellipsis
    /// </summary>
    public static string GetExpectedPreview(string content)
    {
        return content.Length <= 100 ? content : content[..100];
    }
    
    /// <summary>
    /// Validates that a content preview follows the canonical rule
    /// </summary>
    public static bool IsValidPreview(string content, string preview)
    {
        var expected = GetExpectedPreview(content);
        return preview == expected;
    }
}