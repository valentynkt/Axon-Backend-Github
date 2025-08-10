using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests that verify all error codes match the CHAT DOMAIN ERROR CATALOG exactly.
/// Per STORY CH-DOM-010, ensures production code aligns with catalog specifications.
/// </summary>
public class ErrorCatalogConformanceTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void ERROR_TITLE_EMPTY_ConversationTitleEmpty_ShouldUseCatalogCode()
    {
        // This test verifies the gap between current implementation and catalog
        // Current: "CHAT.TITLE.INVALID" | Catalog: "CHAT_CONVERSATION_TITLE_EMPTY"
        
        // Act
        var result = ConversationTitle.Create("");

        // Assert - Testing current vs catalog expectation
        if (result.IsFailure)
        {
            // This will likely fail and show the gap
            result.Error.Code.ShouldBe("CHAT_CONVERSATION_TITLE_EMPTY", 
                "Error code must match catalog specification");
        }
    }

    [Fact]
    public void ERROR_TITLE_TOO_LONG_ConversationTitleTooLong_ShouldUseCatalogCode()
    {
        // Current: "CHAT.TITLE.TOO_LONG" | Catalog: "CHAT_CONVERSATION_TITLE_TOO_LONG"
        
        // Act
        var result = ConversationTitle.Create(StringGenerators.Title.Length201);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_TITLE_TOO_LONG",
            "Error code must match catalog specification");
    }

    [Fact]
    public void ERROR_MESSAGE_CONTENT_EMPTY_MessageContentEmpty_ShouldUseCatalogCode()
    {
        // Current: "CHAT.MESSAGE.EMPTY" | Catalog: "CHAT_MESSAGE_CONTENT_EMPTY"
        
        // Act
        var result = MessageContent.Create("");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_MESSAGE_CONTENT_EMPTY",
            "Error code must match catalog specification");
    }

    [Fact]
    public void ERROR_MESSAGE_CONTENT_TOO_LONG_MessageContentTooLong_ShouldUseCatalogCode()
    {
        // Current: "CHAT.MESSAGE.TOO_LONG" | Catalog: "CHAT_MESSAGE_CONTENT_TOO_LONG"
        
        // Act
        var result = MessageContent.Create(StringGenerators.Content.Length100001);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_MESSAGE_CONTENT_TOO_LONG",
            "Error code must match catalog specification");
    }    [Fact]
    public void ERROR_MESSAGE_LIMIT_EXCEEDED_MessageLimitExceeded_ShouldUseCatalogCode()
    {
        // Current: "CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED" | Catalog: "CHAT_MESSAGE_LIMIT_EXCEEDED"
        
        // Act - Test via business rule directly
        var rule = new Rules.ConversationMessageLimitRule(10_000);

        // Assert - Business rule should use catalog code
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_LIMIT_EXCEEDED",
            "Error code must match catalog specification");
    }

    [Fact]
    public void ERROR_ASSISTANT_TURN_VIOLATION_AssistantTurnViolation_ShouldUseCatalogCode()
    {
        // Current: "CHAT.MESSAGE.TURN_TAKING_VIOLATION" | Catalog: "CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION"
        
        // Act - Create scenario with consecutive assistant messages
        var messages = new List<Message>
        {
            Message.Create(ConversationId.New(), MessageRole.Assistant, 
                MessageContent.Create("First").Value, 1, _clock.UtcNow)
        };
        
        var rule = new Rules.MessageTurnTakingRule(messages, MessageRole.Assistant);

        // Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION",
            "Error code must match catalog specification");
    }

    [Fact]
    public void ERROR_CONVERSATION_NOT_ACTIVE_ConversationNotActive_ShouldUseCatalogCode()
    {
        // Current: Various | Catalog: "CHAT_CONVERSATION_NOT_ACTIVE"
        
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithUserMessage("Hello")
            .Build().Value;

        conversation.Complete(_clock); // Make it not active

        // Act - Try to add message to completed conversation
        var content = MessageContent.Create("This should fail").Value;
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_NOT_ACTIVE",
            "Error code must match catalog specification");
    }

    [Fact]
    public void ERROR_CONVERSATION_EMPTY_ON_COMPLETE_EmptyConversationComplete_ShouldUseCatalogCode()
    {
        // Current: "CHAT.CONVERSATION.COMPLETE_REQUIRES_MESSAGES" | Catalog: "CHAT_CONVERSATION_EMPTY_ON_COMPLETE"
        
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        // Act
        var result = conversation.Complete(_clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_CONVERSATION_EMPTY_ON_COMPLETE",
            "Error code must match catalog specification");
    }
}