using Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases;

/// <summary>
/// Tests sequence integrity across mixed success/failure operations per STORY CH-DOM-010.
/// Verifies that only successful operations affect sequence numbers.
/// </summary>
public class SequenceIntegrityEdgeCaseTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void SEQ_MIX_INTEGRITY_MixedSuccessFailureOperations_ShouldMaintainSequenceIntegrity()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .Build().Value;

        var validContent = MessageContent.Create("Valid message").Value;
        
        // Act & Assert - Mix successful and failed operations
        
        // Success: Add user message (should be sequence 1)
        var result1 = conversation.AppendUserMessage(validContent, _clock);
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.Sequence.ShouldBe(1);
        conversation.MessageCount.ShouldBe(1);

        // Success: Add assistant message (should be sequence 2)
        var result2 = conversation.AppendAssistantMessage(validContent, _clock);
        result2.IsSuccess.ShouldBeTrue();
        result2.Value.Sequence.ShouldBe(2);
        conversation.MessageCount.ShouldBe(2);

        // Failure: Try to add another assistant message (should fail, no sequence increment)
        var result3 = conversation.AppendAssistantMessage(validContent, _clock);
        result3.IsFailure.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(2); // No change

        // Success: Add user message (should be sequence 3, not 4)
        var result4 = conversation.AppendUserMessage(validContent, _clock);
        result4.IsSuccess.ShouldBeTrue();
        result4.Value.Sequence.ShouldBe(3);
        conversation.MessageCount.ShouldBe(3);

        // Verify all sequences are contiguous 1, 2, 3
        var messages = conversation.MessagesOrdered.ToList();
        messages.Select(m => m.Sequence).ShouldBeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void SEQ_CONSECUTIVE_FAILURES_MultipleFailures_ShouldNotAffectNextSuccessSequence()
    {
        // Arrange
        var conversation = ConversationBuilder.New()
            .WithOwner(_ownerId)
            .WithClock(_clock)
            .WithAssistantMessage("First assistant") // Sequence 1
            .Build().Value;

        var validContent = MessageContent.Create("Valid content").Value;

        // Act - Multiple consecutive failures
        var fail1 = conversation.AppendAssistantMessage(validContent, _clock);
        var fail2 = conversation.AppendAssistantMessage(validContent, _clock);
        var fail3 = conversation.AppendAssistantMessage(validContent, _clock);

        // All should fail
        fail1.IsFailure.ShouldBeTrue();
        fail2.IsFailure.ShouldBeTrue();
        fail3.IsFailure.ShouldBeTrue();
        conversation.MessageCount.ShouldBe(1);

        // Now add a valid user message
        var success = conversation.AppendUserMessage(validContent, _clock);

        // Assert
        success.IsSuccess.ShouldBeTrue();
        success.Value.Sequence.ShouldBe(2); // Next in sequence after 1
        conversation.MessageCount.ShouldBe(2);
    }
}