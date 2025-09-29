using System.Text.Json;
using System.Text.Json.Serialization;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Provides snapshot testing capability for complex Chat domain states.
/// Useful for validating entire aggregate states after complex operations.
/// </summary>
public class TestDataSnapshot
{
    private readonly JsonSerializerOptions _jsonOptions;

    public TestDataSnapshot()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ConversationIdConverter(),
                new MessageIdConverter(),
                new AxonUserIdConverter(),
                new AiResponseIdConverter(),
                new MessageContentConverter()
            }
        };
    }

    /// <summary>
    /// Creates a snapshot of a conversation's current state.
    /// </summary>
    public ConversationSnapshot CaptureConversation(Conversation conversation)
    {
        return new ConversationSnapshot
        {
            Id = conversation.Id.Value.ToString(),
            OwnerId = conversation.OwnerId.Value.ToString(),
            Title = conversation.Title ?? string.Empty,
            Status = conversation.Status.ToString(),
            MessageCount = conversation.GetMessageCount(),
            LastAiResponseId = conversation.LastAiResponseId?.Value,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt ?? DateTimeOffset.MinValue,
            IsDeleted = conversation.IsDeleted,
            Messages = conversation.GetAllMessages().Select(CaptureMessage).ToList(),
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a snapshot of a message's state.
    /// </summary>
    private MessageSnapshot CaptureMessage(Message message)
    {
        return new MessageSnapshot
        {
            Id = message.Id.Value.ToString(),
            Sequence = message.Sequence,
            Role = message.Role.ToString(),
            Content = message.Content.Value,
            ContentLength = message.Content.Value.Length,
            AiResponseId = message.AiResponseId?.Value,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt ?? DateTimeOffset.MinValue,
            IsDeleted = message.IsDeleted
        };
    }

    /// <summary>
    /// Compares two snapshots and returns differences.
    /// </summary>
    public SnapshotComparison Compare(ConversationSnapshot before, ConversationSnapshot after)
    {
        var comparison = new SnapshotComparison
        {
            Before = before,
            After = after,
            ComparedAt = DateTimeOffset.UtcNow
        };

        // Check basic property changes
        if (before.Title != after.Title)
            comparison.Differences.Add($"Title changed from '{before.Title}' to '{after.Title}'");

        if (before.Status != after.Status)
            comparison.Differences.Add($"Status changed from '{before.Status}' to '{after.Status}'");

        if (before.MessageCount != after.MessageCount)
            comparison.Differences.Add($"Message count changed from {before.MessageCount} to {after.MessageCount}");

        if (before.LastAiResponseId != after.LastAiResponseId)
            comparison.Differences.Add($"LastAiResponseId changed from '{before.LastAiResponseId}' to '{after.LastAiResponseId}'");

        // Check message differences
        var messagesDiff = CompareMessages(before.Messages, after.Messages);
        comparison.Differences.AddRange(messagesDiff);

        comparison.HasDifferences = comparison.Differences.Any();

        return comparison;
    }

    private List<string> CompareMessages(List<MessageSnapshot> before, List<MessageSnapshot> after)
    {
        var differences = new List<string>();

        // Check added messages
        var beforeIds = before.Select(m => m.Id).ToHashSet();
        var afterIds = after.Select(m => m.Id).ToHashSet();

        var added = afterIds.Except(beforeIds);
        var removed = beforeIds.Except(afterIds);

        foreach (var id in added)
        {
            var message = after.First(m => m.Id == id);
            differences.Add($"Message added: Seq {message.Sequence}, Role: {message.Role}");
        }

        foreach (var id in removed)
        {
            var message = before.First(m => m.Id == id);
            differences.Add($"Message removed: Seq {message.Sequence}, Role: {message.Role}");
        }

        // Check modified messages
        var commonIds = beforeIds.Intersect(afterIds);
        foreach (var id in commonIds)
        {
            var beforeMsg = before.First(m => m.Id == id);
            var afterMsg = after.First(m => m.Id == id);

            if (beforeMsg.Content != afterMsg.Content)
                differences.Add($"Message {beforeMsg.Sequence} content changed");

            if (beforeMsg.IsDeleted != afterMsg.IsDeleted)
                differences.Add($"Message {beforeMsg.Sequence} deletion status changed to {afterMsg.IsDeleted}");
        }

        return differences;
    }

    /// <summary>
    /// Serializes a snapshot to JSON for storage or comparison.
    /// </summary>
    public string SerializeSnapshot(ConversationSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, _jsonOptions);
    }

    /// <summary>
    /// Deserializes a snapshot from JSON.
    /// </summary>
    public ConversationSnapshot? DeserializeSnapshot(string json)
    {
        return JsonSerializer.Deserialize<ConversationSnapshot>(json, _jsonOptions);
    }

    #region Snapshot Models

    public class ConversationSnapshot
    {
        public required string Id { get; init; }
        public required string OwnerId { get; init; }
        public required string Title { get; init; }
        public required string Status { get; init; }
        public required int MessageCount { get; init; }
        public string? LastAiResponseId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public bool IsDeleted { get; init; }
        public required List<MessageSnapshot> Messages { get; init; }
        public DateTimeOffset CapturedAt { get; init; }
    }

    public class MessageSnapshot
    {
        public required string Id { get; init; }
        public required int Sequence { get; init; }
        public required string Role { get; init; }
        public required string Content { get; init; }
        public required int ContentLength { get; init; }
        public string? AiResponseId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public bool IsDeleted { get; init; }
    }

    public class SnapshotComparison
    {
        public required ConversationSnapshot Before { get; init; }
        public required ConversationSnapshot After { get; init; }
        public List<string> Differences { get; } = new();
        public bool HasDifferences { get; set; }
        public DateTimeOffset ComparedAt { get; init; }
    }

    #endregion

    #region JSON Converters

    private class ConversationIdConverter : JsonConverter<ConversationId>
    {
        public override ConversationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new ConversationId(Guid.Parse(value!));
        }

        public override void Write(Utf8JsonWriter writer, ConversationId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value.ToString());
        }
    }

    private class MessageIdConverter : JsonConverter<MessageId>
    {
        public override MessageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new MessageId(Guid.Parse(value!));
        }

        public override void Write(Utf8JsonWriter writer, MessageId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value.ToString());
        }
    }

    private class AxonUserIdConverter : JsonConverter<AxonUserId>
    {
        public override AxonUserId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new AxonUserId(Guid.Parse(value!));
        }

        public override void Write(Utf8JsonWriter writer, AxonUserId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    private class AiResponseIdConverter : JsonConverter<AiResponseId>
    {
        public override AiResponseId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new AiResponseId(value!);
        }

        public override void Write(Utf8JsonWriter writer, AiResponseId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    private class MessageContentConverter : JsonConverter<MessageContent>
    {
        public override MessageContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return MessageContent.From(value!);
        }

        public override void Write(Utf8JsonWriter writer, MessageContent value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    #endregion
}