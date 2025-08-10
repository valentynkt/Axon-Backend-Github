using Axon.Modules.Chat.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class MessageContentWithinLimitsRuleTests
{
    private const int MaxLength = 100_000;

    [Test]
    public void IsBroken_WhenContentIsNull_ShouldReturnTrue()
    {
        // Arrange
        var rule = new MessageContentWithinLimitsRule(null);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_CONTENT_EMPTY");
        rule.Message.ShouldBe("Message content cannot be empty.");
    }

    [Test]
    public void IsBroken_WhenContentIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        var rule = new MessageContentWithinLimitsRule("");

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_CONTENT_EMPTY");
    }

    [Test]
    public void IsBroken_WhenContentIsWhitespace_ShouldReturnTrue()
    {
        // Arrange
        var rule = new MessageContentWithinLimitsRule("   ");

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_CONTENT_EMPTY");
    }

    [Test]
    public void IsBroken_WhenContentIsOneCharacter_ShouldReturnFalse()
    {
        // Arrange
        var rule = new MessageContentWithinLimitsRule("a");

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenContentIsExactlyMaxLength_ShouldReturnFalse()
    {
        // Arrange
        var content = new string('a', MaxLength);
        var rule = new MessageContentWithinLimitsRule(content);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenContentExceedsMaxLength_ShouldReturnTrue()
    {
        // Arrange
        var content = new string('a', MaxLength + 1);
        var rule = new MessageContentWithinLimitsRule(content);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_MESSAGE_CONTENT_TOO_LONG");
        rule.Message.ShouldBe($"Message content cannot exceed {MaxLength} characters.");
    }
}