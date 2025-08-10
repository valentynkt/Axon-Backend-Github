using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests message content edge cases and boundary conditions per STORY CH-DOM-010.
/// Focuses on content limits, turn-taking rules, preview boundaries.
/// </summary>
public class MessageContentEdgeCaseTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void MSG_A1_OK_AssistantAsFirstMessage_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        var content = MessageContent.Create("Hello! I'm here to help.").Value;

        // Act
        var result = conversation.AppendAssistantMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Role.IsAssistant.ShouldBeTrue();
        result.Value.Sequence.ShouldBe(1);
        conversation.MessageCount.ShouldBe(1);
    }

    [Fact]
    public void MSG_AA_FAIL_ConsecutiveAssistantMessages_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithAssistantMessage("First assistant message")
            .Build().Value;

        var content = MessageContent.Create("Second assistant message").Value;

        // Act
        var result = conversation.AppendAssistantMessage(content, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION");
        conversation.MessageCount.ShouldBe(1); // No state change on failure
    }

    [Fact]
    public void MSG_UU_OK_ConsecutiveUserMessages_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithUserMessage("First user message")
            .Build().Value;

        var content = MessageContent.Create("Second user message").Value;

        // Act
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Sequence.ShouldBe(2);
        conversation.MessageCount.ShouldBe(2);
    }

    [Fact]
    public void MSG_CONT_1_OK_ContentLength1_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        var content = MessageContent.Create(StringGenerators.Content.Length1).Value;

        // Act
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.Value.ShouldBe(StringGenerators.Content.Length1);
    }

    [Fact]
    public void MSG_CONT_100K_OK_ContentLength100000_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        var content = MessageContent.Create(StringGenerators.Content.Length100K).Value;

        // Act
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.Value.Length.ShouldBe(100_000);
    }

    [Fact]
    public void MSG_CONT_0_FAIL_EmptyContent_ShouldFailWithCatalogCode()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        // Act
        var contentResult = MessageContent.Create(string.Empty);

        // Assert
        contentResult.IsFailure.ShouldBeTrue();
        contentResult.Error.Code.ShouldBe("CHAT_MESSAGE_CONTENT_EMPTY");
    }

    [Fact]
    public void MSG_CONT_100001_FAIL_ContentLength100001_ShouldFailWithCatalogCode()
    {
        // Act
        var contentResult = MessageContent.Create(StringGenerators.Content.Length100001);

        // Assert
        contentResult.IsFailure.ShouldBeTrue();
        contentResult.Error.Code.ShouldBe("CHAT_MESSAGE_CONTENT_TOO_LONG");
    }
}