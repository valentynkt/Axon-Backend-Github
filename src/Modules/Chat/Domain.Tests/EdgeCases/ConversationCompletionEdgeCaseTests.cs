using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests conversation completion edge cases per STORY CH-DOM-010.
/// Verifies completion requirements and state transitions.
/// </summary>
public class ConversationCompletionEdgeCaseTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void CMP_OK_CompleteConversationWithMessages_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithUserMessage("Hello")
            .WithAssistantMessage("Hi there!")
            .Build().Value;

        // Act
        var result = conversation.Complete(_clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Status.ShouldBe(ConversationStatus.Completed);
        conversation.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void CMP_EMPTY_FAIL_CompleteConversationWithNoMessages_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        // Act
        var result = conversation.Complete(_clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_EMPTY_ON_COMPLETE");
        conversation.Status.ShouldBe(ConversationStatus.Active); // No state change
    }

    [Fact]
    public void CMP_AGAIN_FAIL_CompleteAlreadyCompletedConversation_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithUserMessage("Hello")
            .Build().Value;

        var firstComplete = conversation.Complete(_clock);
        firstComplete.IsSuccess.ShouldBeTrue();

        // Act - Try to complete again
        var result = conversation.Complete(_clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_NOT_ACTIVE");
        conversation.Status.ShouldBe(ConversationStatus.Completed); // State unchanged
    }
}