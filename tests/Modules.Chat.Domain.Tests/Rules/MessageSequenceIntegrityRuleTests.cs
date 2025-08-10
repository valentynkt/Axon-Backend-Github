using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class MessageSequenceIntegrityRuleTests
{
    [Test]
    public void IsBroken_WithEmptyList_ShouldReturnFalse()
    {
        // Arrange
        var messages = new List<Message>();
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WithCorrectSequence_ShouldReturnFalse()
    {
        // Arrange
        var messages = new List<Message>
        {
            CreateMessage(1),
            CreateMessage(2),
            CreateMessage(3)
        };
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WithGapInSequence_ShouldReturnTrue()
    {
        // Arrange - sequence 1, 2, 4 (missing 3)
        var messages = new List<Message>
        {
            CreateMessage(1),
            CreateMessage(2),
            CreateMessage(4)
        };
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT.MESSAGE.SEQUENCE_VIOLATION");
        rule.Message.ShouldBe("Message sequence must be consecutive starting from 1.");
    }

    [Test]
    public void IsBroken_WithOutOfOrderSequence_ShouldReturnTrue()
    {
        // Arrange - sequence 2, 1
        var messages = new List<Message>
        {
            CreateMessage(2),
            CreateMessage(1)
        };
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WithSequenceNotStartingAtOne_ShouldReturnTrue()
    {
        // Arrange - sequence starts at 2
        var messages = new List<Message>
        {
            CreateMessage(2),
            CreateMessage(3)
        };
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WithSingleCorrectMessage_ShouldReturnFalse()
    {
        // Arrange
        var messages = new List<Message> { CreateMessage(1) };
        var rule = new MessageSequenceIntegrityRule(messages);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WithNullList_ShouldReturnFalse()
    {
        // Arrange
        var rule = new MessageSequenceIntegrityRule(null!);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    private static Message CreateMessage(int sequence)
    {
        return new Message(
            MessageId.New(),
            MessageRole.User,
            MessageContent.Create("Test content").Value,
            sequence,
            DateTimeOffset.UtcNow
        );
    }
}