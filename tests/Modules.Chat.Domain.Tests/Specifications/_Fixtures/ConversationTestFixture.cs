using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

namespace Axon.Modules.Chat.Domain.Tests.Specifications._Fixtures;

public static class ConversationTestFixture
{
    private static readonly FixedClock FixedClock = new(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public static readonly UserId Owner1 = new(Guid.NewGuid());
    public static readonly UserId Owner2 = new(Guid.NewGuid());

    /// <summary>
    /// Creates test conversations with varied properties for comprehensive testing
    /// </summary>
    public static List<Conversation> CreateTestConversations()
    {
        var conversations = new List<Conversation>();

        // Owner1 - Active conversation with multiple messages
        var conv1 = Conversation.Start(Owner1, FixedClock, "My Active Chat").Value;
        conv1.AppendUserMessage("Hello", FixedClock);
        conv1.AppendAssistantMessage("Hi there", FixedClock);
        conv1.AppendUserMessage("How are you?", FixedClock);
        conversations.Add(conv1);

        // Owner1 - Completed conversation
        var conv2 = Conversation.Start(Owner1, FixedClock, "Completed Chat").Value;
        conv2.AppendUserMessage("Task done", FixedClock);
        conv2.Complete(FixedClock);
        conversations.Add(conv2);

        // Owner1 - Active conversation with no title (default)
        var conv3 = Conversation.Start(Owner1, FixedClock).Value;
        conv3.AppendUserMessage("Quick question", FixedClock);
        conversations.Add(conv3);

        // Owner2 - Active conversation with case-sensitive title
        var conv4 = Conversation.Start(Owner2, FixedClock, "IMPORTANT Project").Value;
        conv4.AppendUserMessage("Let's start", FixedClock);
        conv4.AppendAssistantMessage("Sure thing", FixedClock);
        conv4.AppendUserMessage("Great", FixedClock);
        conv4.AppendAssistantMessage("What next?", FixedClock);
        conv4.AppendUserMessage("Let me think", FixedClock);
        conversations.Add(conv4);

        // Owner2 - Active conversation with zero messages (edge case)
        var conv5 = Conversation.Start(Owner2, FixedClock, "Empty Chat").Value;
        conversations.Add(conv5);

        return conversations;
    }

    /// <summary>
    /// Creates conversations with specific time properties for time-based tests
    /// </summary>
    public static List<Conversation> CreateTimeBasedConversations()
    {
        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var conversations = new List<Conversation>();

        // Old conversation (created 10 days ago)
        var oldClock = new FixedClock(baseTime.AddDays(-10));
        var oldConv = Conversation.Start(Owner1, oldClock, "Old Chat").Value;
        oldConv.AppendUserMessage("Old message", oldClock);
        conversations.Add(oldConv);

        // Recent conversation (created 1 day ago)
        var recentClock = new FixedClock(baseTime.AddDays(-1));
        var recentConv = Conversation.Start(Owner1, recentClock, "Recent Chat").Value;
        recentConv.AppendUserMessage("Recent message", recentClock);
        conversations.Add(recentConv);

        // Current conversation (created now)
        var currentClock = new FixedClock(baseTime);
        var currentConv = Conversation.Start(Owner1, currentClock, "Current Chat").Value;
        conversations.Add(currentConv);

        return conversations;
    }
}