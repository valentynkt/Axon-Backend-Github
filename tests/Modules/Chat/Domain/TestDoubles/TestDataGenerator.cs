using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.TestDoubles;

/// <summary>
/// Provides realistic test data generation using Bogus faker.
/// Creates domain-appropriate data that respects business rules and constraints.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Generates a valid conversation title within length limits.
    /// </summary>
    public static string GenerateConversationTitle()
    {
        return _faker.Lorem.Sentence(wordCount: _faker.Random.Int(2, 8))
            .TrimEnd('.')
            .Truncate(TestConstants.Limits.MaxConversationTitleLength);
    }

    /// <summary>
    /// Generates a realistic message content within length limits.
    /// </summary>
    public static string GenerateMessageContent(int minWords = 5, int maxWords = 50)
    {
        return _faker.Lorem.Sentence(wordCount: _faker.Random.Int(minWords, maxWords))
            .Truncate(TestConstants.Limits.MaxMessageContentLength);
    }

    /// <summary>
    /// Generates a long message content for testing limits.
    /// </summary>
    public static string GenerateLongMessageContent()
    {
        return _faker.Lorem.Paragraphs( 10, separator: "\n\n")
            .Truncate(TestConstants.Limits.MaxMessageContentLength - 10); // Leave some buffer
    }

    /// <summary>
    /// Generates a realistic AI response ID.
    /// </summary>
    public static string GenerateAiResponseId()
    {
        return $"ai-{_faker.Random.Guid().ToString("N")[..20]}";
    }

    /// <summary>
    /// Generates a realistic user ID string.
    /// </summary>
    public static string GenerateAxonUserId()
    {
        return $"user-{_faker.Random.Guid().ToString("N")[..12]}";
    }

    /// <summary>
    /// Generates multiple conversation titles for batch testing.
    /// </summary>
    public static IEnumerable<string> GenerateConversationTitles(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateConversationTitle());
    }

    /// <summary>
    /// Generates multiple message contents for batch testing.
    /// </summary>
    public static IEnumerable<string> GenerateMessageContents(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateMessageContent());
    }

    /// <summary>
    /// Generates alternating user and assistant messages for realistic conversations.
    /// </summary>
    public static IEnumerable<(MessageRole role, string content, AiResponseId? aiResponseId)> GenerateConversationFlow(int messageCount)
{
    for (int i = 0; i < messageCount; i++)
    {
        bool isUser = i % 2 == 0;
        var role = isUser ? MessageRole.User : MessageRole.Assistant;
        var content = isUser 
            ? GenerateUserMessage() 
            : GenerateAssistantMessage();
        var aiResponseId = isUser ? (AiResponseId?)null : new AiResponseId(GenerateAiResponseId());

        yield return (role, content, aiResponseId);
    }
}

    /// <summary>
    /// Generates a realistic user message (questions, requests, statements).
    /// </summary>
    public static string GenerateUserMessage()
    {
        var templates = new Func<string>[]
        {
            () => _faker.Lorem.Sentence() + "?",
            () => "Can you help me with " + string.Join(" ", _faker.Lorem.Words(num: 3)) + "?",
            () => "I need to " + _faker.Lorem.Sentence().ToLowerInvariant().TrimEnd('.'),
            () => _faker.Lorem.Sentence(),
            () => "What is " + string.Join(" ", _faker.Lorem.Words(num: 2)) + "?",
            () => "How do I " + string.Join(" ", _faker.Lorem.Words(num: 3)) + "?"
        };

        return _faker.Random.ArrayElement(templates)()
            .Truncate(TestConstants.Limits.MaxMessageContentLength);
    }

    /// <summary>
    /// Generates a realistic assistant response (helpful, informative).
    /// </summary>
    public static string GenerateAssistantMessage()
    {
        var templates = new Func<string>[]
        {
            () => "I can help you with that. " + _faker.Lorem.Sentence(),
            () => "Here's what you need to know: " + _faker.Lorem.Paragraph(),
            () => "Based on your question, " + _faker.Lorem.Sentence(),
            () => _faker.Lorem.Paragraph(),
            () => "To accomplish this, you should " + _faker.Lorem.Sentence(),
            () => "The answer is " + _faker.Lorem.Sentence()
        };

        return _faker.Random.ArrayElement(templates)()
            .Truncate(TestConstants.Limits.MaxMessageContentLength);
    }
}

/// <summary>
/// Extension methods for string manipulation in test data generation.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}