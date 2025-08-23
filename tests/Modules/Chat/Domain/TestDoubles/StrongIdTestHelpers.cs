using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.TestDoubles;

/// <summary>
/// Test helpers for creating StronglyTypedId instances in tests.
/// Provides factory methods and utilities for working with domain IDs.
/// </summary>
public static class StrongIdTestHelpers
{
    /// <summary>
    /// Creates a sequence of unique ConversationIds for testing.
    /// </summary>
    public static List<ConversationId> CreateConversationIds(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => ConversationId.New())
            .ToList();
    }

    /// <summary>
    /// Creates a sequence of unique MessageIds for testing.
    /// </summary>
    public static List<MessageId> CreateMessageIds(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => MessageId.New())
            .ToList();
    }

    /// <summary>
    /// Creates a sequence of unique UserIds for testing.
    /// </summary>
    public static List<UserId> CreateUserIds(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => UserId.New())
            .ToList();
    }

    /// <summary>
    /// Creates a sequence of unique AiResponseIds for testing.
    /// </summary>
    public static List<AiResponseId> CreateAiResponseIds(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => AiResponseId.From($"ai-response-{i:D6}-{Guid.NewGuid():N}"))
            .ToList();
    }

    /// <summary>
    /// Creates deterministic ConversationIds for reproducible tests.
    /// Uses predictable GUIDs based on seed values.
    /// </summary>
    public static ConversationId CreateDeterministicConversationId(int seed = 1)
    {
        var deterministicGuid = CreateDeterministicGuid(seed);
        return new ConversationId(deterministicGuid);
    }

    /// <summary>
    /// Creates deterministic MessageIds for reproducible tests.
    /// </summary>
    public static MessageId CreateDeterministicMessageId(int seed = 1)
    {
        var deterministicGuid = CreateDeterministicGuid(seed + 1000);
        return new MessageId(deterministicGuid);
    }

    /// <summary>
    /// Creates deterministic UserIds for reproducible tests.
    /// </summary>
    public static UserId CreateDeterministicUserId(int seed = 1)
    {
        var deterministicGuid = CreateDeterministicGuid(seed + 2000);
        return new UserId(deterministicGuid);
    }

    /// <summary>
    /// Creates deterministic AiResponseIds for reproducible tests.
    /// </summary>
    public static AiResponseId CreateDeterministicAiResponseId(int seed = 1)
    {
        return AiResponseId.From($"ai-response-test-{seed:D6}");
    }

    /// <summary>
    /// Validates that a collection of IDs are all unique.
    /// </summary>
    public static void AssertAllIdsAreUnique<T>(IEnumerable<T> ids) where T : struct
    {
        var idList = ids.ToList();
        var uniqueIds = idList.Distinct().ToList();
        
        uniqueIds.Count.ShouldBe(idList.Count, 
            $"Expected all {typeof(T).Name} instances to be unique, but found duplicates");
    }

    /// <summary>
    /// Creates a deterministic GUID from a seed value.
    /// Useful for reproducible test scenarios.
    /// </summary>
    private static Guid CreateDeterministicGuid(int seed)
    {
        var random = new Random(seed);
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }

    /// <summary>
    /// Creates test data representing different user scenarios.
    /// </summary>
    public static class Users
    {
        public static readonly UserId Owner = TestConstants.Users.DefaultOwnerId;
        public static readonly UserId AlternativeUser = TestConstants.Users.AlternativeOwnerId;
        public static readonly UserId ThirdUser = TestConstants.Users.ThirdOwnerId;
        
        public static UserId CreateRandomUser() => UserId.New();
        
        public static List<UserId> CreateUserGroup(int count) => CreateUserIds(count);
    }

    /// <summary>
    /// Creates test data for conversation scenarios.
    /// </summary>
    public static class Conversations
    {
        public static ConversationId CreateForUser(UserId userId)
        {
            // Create a conversation ID that's deterministically linked to the user
            // This can be useful for testing relationships
            var userGuid = userId.Value;
            var conversationBytes = userGuid.ToByteArray();
            
            // Modify some bytes to create a related but different GUID
            conversationBytes[0] = (byte)(conversationBytes[0] ^ 0xFF);
            conversationBytes[1] = (byte)(conversationBytes[1] ^ 0xAA);
            
            return new ConversationId(new Guid(conversationBytes));
        }
        
        public static List<ConversationId> CreateBatch(int count) => CreateConversationIds(count);
    }

    /// <summary>
    /// Creates test data for AI response scenarios.
    /// </summary>
    public static class AiResponses
    {
        public static AiResponseId CreateWithPrefix(string prefix = "test")
        {
            return AiResponseId.From($"{prefix}-{Guid.NewGuid():N}");
        }
        
        public static List<AiResponseId> CreateSequence(int count, string prefix = "seq")
        {
            return Enumerable.Range(1, count)
                .Select(i => AiResponseId.From($"{prefix}-{i:D4}"))
                .ToList();
        }
        
        public static AiResponseId CreateForMessage(MessageId messageId)
        {
            // Create an AI response ID that's related to a specific message
            return AiResponseId.From($"msg-{messageId.Value:N}");
        }
    }
}