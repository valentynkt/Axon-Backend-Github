using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]  
[Category("Domain")]
[Category("Rules")]
public sealed class MessageTurnTakingRuleTests
{
    [Test]
    public void IsBroken_WhenEmptyListAndAssistant_ShouldReturnFalse()
    {
        // Arrange - Assistant CAN be first message per spec
        var messages = new List<Message>();
        var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenEmptyListAndUser_ShouldReturnFalse()
    {
        // Arrange
        var messages = new List<Message>();
        var rule = new MessageTurnTakingRule(messages, MessageRole.User);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenLastUserAndNewAssistant_ShouldReturnFalse()
    {
        // Arrange
        var messages = new List<Message>
        {
            CreateMessage(MessageRole.User, 1)
        };
        var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenLastAssistantAndNewAssistant_ShouldReturnTrue()
    {
        // Arrange
        var messages = new List<Message>
        {
            CreateMessage(MessageRole.Assistant, 1)
        };
        var rule = new MessageTurnTakingRule(messages, MessageRole.Assistant);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT.MESSAGE.TURN_TAKING_VIOLATION");
        rule.Message.ShouldBe("Assistant cannot reply twice in a row.");
    }

    [Test]
    public void IsBroken_WhenLastUserAndNewUser_ShouldReturnFalse()
    {
        // Arrange - Consecutive users are allowed
        var messages = new List<Message>
        {
            CreateMessage(MessageRole.User, 1)
        };
        var rule = new MessageTurnTakingRule(messages, MessageRole.User);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WithNullMessagesList_ShouldTreatAsEmpty()
    {
        // Arrange
        var rule = new MessageTurnTakingRule(null!, MessageRole.Assistant);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    private static Message CreateMessage(MessageRole role, int sequence)
    {
        return new Message(
            MessageId.New(),
            role,
            MessageContent.Create("Test content").Value,
            sequence,
            DateTimeOffset.UtcNow
        );
    }
}