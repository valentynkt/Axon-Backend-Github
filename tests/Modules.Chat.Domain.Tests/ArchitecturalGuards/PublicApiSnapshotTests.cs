using System.Reflection;
using PublicApiGenerator;

namespace Axon.Modules.Chat.Domain.Tests.ArchitecturalGuards;

/// <summary>
/// Tests to ensure the public API surface of the Chat domain doesn't change unexpectedly.
/// When intentional changes are made, approve the new snapshot by updating the expected output.
/// </summary>
[TestFixture]
public class PublicApiSnapshotTests
{
    /// <summary>
    /// Captures a snapshot of all public types and members in the Chat domain assembly.
    /// If this test fails, review the changes:
    /// - If intentional: Update the expected API snapshot in this test
    /// - If unintentional: Fix the visibility modifiers to match the approved public surface
    /// </summary>
    [Test]
    public void ChatDomain_PublicApiSnapshot_ShouldMatchApprovedSurface()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;
        
        // Act - Generate the current public API
        var currentApi = assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
            AllowNamespacePrefixes = new[] { "Axon.Modules.Chat.Domain" },
            ExcludeAttributes = new[] { "System.Runtime.CompilerServices.CompilerGeneratedAttribute" }
        });

        // Expected public API surface (approved baseline)
        var expectedApi = GetExpectedApiSnapshot();
        
        // Assert
        currentApi.ShouldBe(expectedApi, 
            "The public API surface has changed. " +
            "If this change is intentional, update the expectedApi in this test method. " +
            "If unintentional, fix the visibility modifiers to match the approved public surface.");
    }

    /// <summary>
    /// Returns the approved public API snapshot.
    /// Update this when making intentional changes to the public surface.
    /// </summary>
    private static string GetExpectedApiSnapshot()
    {
        return """
namespace Axon.Modules.Chat.Domain
{
    public static class AssemblyMarker
    {
    }
}
namespace Axon.Modules.Chat.Domain.Aggregates.Conversation
{
    public sealed class Conversation : BuildingBlocks.Core.Domain.AggregateRoot<Axon.Modules.Chat.Domain.ValueObjects.ConversationId>
    {
        public int MessageCount { get; }
        public System.Collections.Generic.IReadOnlyList<Axon.Modules.Chat.Domain.Entities.Message> MessagesOrdered { get; }
        public Axon.Modules.Chat.Domain.ValueObjects.UserId OwnerId { get; }
        public Axon.Modules.Chat.Domain.Aggregates.Conversation.ConversationStatus Status { get; }
        public string Title { get; }
        public bool IsDefaultTitle { get; }
        public System.DateTimeOffset CreatedAtUtc { get; }
        public System.DateTimeOffset UpdatedAtUtc { get; }
        public bool IsActive { get; }
        public BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.Entities.Message> AppendAssistantMessage(Axon.Modules.Chat.Domain.ValueObjects.MessageContent content, Axon.Modules.Chat.Domain.Time.IClock clock) { }
        public BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.Entities.Message> AppendUserMessage(Axon.Modules.Chat.Domain.ValueObjects.MessageContent content, Axon.Modules.Chat.Domain.Time.IClock clock) { }
        public bool BelongsTo(Axon.Modules.Chat.Domain.ValueObjects.UserId userId) { }
        public BuildingBlocks.Core.Functional.Results.Result<BuildingBlocks.Core.Domain.Unit> Complete(Axon.Modules.Chat.Domain.Time.IClock clock) { }
        public static BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.Aggregates.Conversation.Conversation> Start(Axon.Modules.Chat.Domain.ValueObjects.UserId ownerId, string? titleOrNull, Axon.Modules.Chat.Domain.Time.IClock clock) { }
        public BuildingBlocks.Core.Functional.Results.Result<BuildingBlocks.Core.Domain.Unit> UpdateTitle(string newTitleValue, Axon.Modules.Chat.Domain.Time.IClock clock) { }
    }
    public enum ConversationStatus
    {
        Active = 1,
        Completed = 2,
    }
}
namespace Axon.Modules.Chat.Domain.Entities
{
    public sealed class Message : BuildingBlocks.Core.Domain.Entity<Axon.Modules.Chat.Domain.ValueObjects.MessageId>
    {
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; }
        public Axon.Modules.Chat.Domain.ValueObjects.MessageRole Role { get; }
        public Axon.Modules.Chat.Domain.ValueObjects.MessageContent Content { get; }
        public int Sequence { get; }
        public System.DateTimeOffset CreatedAtUtc { get; }
    }
}
namespace Axon.Modules.Chat.Domain.Events
{
    public sealed record AssistantMessageAppendedEvent : BuildingBlocks.Core.Domain.DomainEvent
    {
        public AssistantMessageAppendedEvent(Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId, Axon.Modules.Chat.Domain.ValueObjects.MessageId MessageId, int MessageSequence, string ContentPreview, System.DateTimeOffset AppendedAt) { }
        public System.DateTimeOffset AppendedAt { get; init; }
        public string ContentPreview { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.MessageId MessageId { get; init; }
        public int MessageSequence { get; init; }
    }
    public sealed record ConversationCompletedEvent : BuildingBlocks.Core.Domain.DomainEvent
    {
        public ConversationCompletedEvent(Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId, int MessageCount, System.DateTimeOffset CompletedAt) { }
        public System.DateTimeOffset CompletedAt { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; init; }
        public int MessageCount { get; init; }
    }
    public sealed record ConversationStartedEvent : BuildingBlocks.Core.Domain.DomainEvent
    {
        public ConversationStartedEvent(Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId, Axon.Modules.Chat.Domain.ValueObjects.UserId OwnerId, string Title, bool IsDefaultTitle, System.DateTimeOffset StartedAt) { }
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; init; }
        public bool IsDefaultTitle { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.UserId OwnerId { get; init; }
        public System.DateTimeOffset StartedAt { get; init; }
        public string Title { get; init; }
    }
    public sealed record ConversationTitleUpdatedEvent : BuildingBlocks.Core.Domain.DomainEvent
    {
        public ConversationTitleUpdatedEvent(Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId, string NewTitle, bool IsDefaultTitle, System.DateTimeOffset UpdatedAt) { }
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; init; }
        public bool IsDefaultTitle { get; init; }
        public string NewTitle { get; init; }
        public System.DateTimeOffset UpdatedAt { get; init; }
    }
    public sealed record UserMessageAppendedEvent : BuildingBlocks.Core.Domain.DomainEvent
    {
        public UserMessageAppendedEvent(Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId, Axon.Modules.Chat.Domain.ValueObjects.MessageId MessageId, int MessageSequence, string ContentPreview, System.DateTimeOffset AppendedAt) { }
        public System.DateTimeOffset AppendedAt { get; init; }
        public string ContentPreview { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.ConversationId ConversationId { get; init; }
        public Axon.Modules.Chat.Domain.ValueObjects.MessageId MessageId { get; init; }
        public int MessageSequence { get; init; }
    }
}
namespace Axon.Modules.Chat.Domain.Time
{
    public interface IClock
    {
        System.DateTimeOffset UtcNow { get; }
    }
    public sealed class SystemClock : Axon.Modules.Chat.Domain.Time.IClock
    {
        public SystemClock() { }
        public System.DateTimeOffset UtcNow { get; }
    }
}
namespace Axon.Modules.Chat.Domain.ValueObjects
{
    public sealed record ConversationId : BuildingBlocks.Core.Domain.StrongId<System.Guid>
    {
        public ConversationId(System.Guid value) { }
        public static Axon.Modules.Chat.Domain.ValueObjects.ConversationId New() { }
    }
    public sealed record ConversationTitle : BuildingBlocks.Core.Domain.ValueObject
    {
        public string Value { get; }
        public int Length { get; }
        public bool IsEmpty { get; }
        public static BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.ValueObjects.ConversationTitle> Create(string? value) { }
        public override string ToString() { }
        public override BuildingBlocks.Core.Functional.Validation.Validation<BuildingBlocks.Core.Domain.Unit> Validate() { }
    }
    public sealed record MessageContent : BuildingBlocks.Core.Domain.ValueObject
    {
        public string Value { get; }
        public int Length { get; }
        public bool IsEmpty { get; }
        public static BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.ValueObjects.MessageContent> Create(string? value) { }
        public override string ToString() { }
        public override BuildingBlocks.Core.Functional.Validation.Validation<BuildingBlocks.Core.Domain.Unit> Validate() { }
    }
    public sealed record MessageId : BuildingBlocks.Core.Domain.StrongId<System.Guid>
    {
        public MessageId(System.Guid value) { }
        public static Axon.Modules.Chat.Domain.ValueObjects.MessageId New() { }
    }
    public sealed record MessageRole : BuildingBlocks.Core.Domain.ValueObject
    {
        public string Value { get; }
        public static Axon.Modules.Chat.Domain.ValueObjects.MessageRole Assistant { get; }
        public static Axon.Modules.Chat.Domain.ValueObjects.MessageRole User { get; }
        public static BuildingBlocks.Core.Functional.Results.Result<Axon.Modules.Chat.Domain.ValueObjects.MessageRole> Create(string? value) { }
        public override string ToString() { }
        public override BuildingBlocks.Core.Functional.Validation.Validation<BuildingBlocks.Core.Domain.Unit> Validate() { }
    }
    public sealed record UserId : BuildingBlocks.Core.Domain.StrongId<System.Guid>
    {
        public UserId(System.Guid value) { }
        public static Axon.Modules.Chat.Domain.ValueObjects.UserId New() { }
    }
}
""".Trim();
    }
}