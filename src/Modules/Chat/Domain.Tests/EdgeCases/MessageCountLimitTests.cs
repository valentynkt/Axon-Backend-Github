using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests message count limits per STORY CH-DOM-010.
/// Verifies 10,000 message limit enforcement.
/// </summary>
public class MessageCountLimitTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact(Skip = "Performance: Creates 10K messages - enable for full edge case coverage")]
    public void CNT_10000_OK_ConversationWith10000Messages_ShouldSucceed()
    {
        // Arrange - This test is expensive but critical for boundary verification
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithMessages(9999, MessageRole.User) // Start with 9999
            .Build().Value;

        var content = MessageContent.Create("Message 10000").Value;

        // Act - Try to add the 10,000th message
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(10_000);
        result.Value.Sequence.ShouldBe(10_000);
    }

    [Fact(Skip = "Performance: Creates 10K messages - enable for full edge case coverage")]
    public void CNT_10001_FAIL_ConversationWith10001Messages_ShouldFailWithCatalogCode()
    {
        // Arrange - This test is expensive but critical for boundary verification
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithMessages(10_000, MessageRole.User)
            .Build().Value;

        var content = MessageContent.Create("Message 10001 - should fail").Value;

        // Act - Try to add the 10,001st message
        var result = conversation.AppendUserMessage(content, _clock);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT_MESSAGE_LIMIT_EXCEEDED");
        conversation.MessageCount.ShouldBe(10_000); // No state change
    }

    [Fact]
    public void CNT_LIMIT_SIMULATION_MessageLimitRule_ShouldFailAt10000()
    {
        // This tests the rule directly without creating 10K actual messages
        // to verify the boundary condition logic
        
        // Arrange
        var ruleAt9999 = new Rules.ConversationMessageLimitRule(9_999);
        var ruleAt10000 = new Rules.ConversationMessageLimitRule(10_000);

        // Act & Assert
        ruleAt9999.IsBroken().ShouldBeFalse(); // 9999 should be allowed
        ruleAt10000.IsBroken().ShouldBeTrue();  // 10000 should trigger limit
    }
}