using Axon.Modules.Chat.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class ConversationMessageLimitRuleTests
{
    private const int MaxMessages = 10_000;

    [Test]
    public void IsBroken_WhenCountBelowLimit_ShouldReturnFalse()
    {
        // Arrange
        var rule = new ConversationMessageLimitRule(9_999, MaxMessages);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenCountEqualsLimit_ShouldReturnTrue()
    {
        // Arrange
        var rule = new ConversationMessageLimitRule(10_000, MaxMessages);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED");
        rule.Message.ShouldBe($"Conversation cannot exceed {MaxMessages} messages.");
    }

    [Test]
    public void IsBroken_WhenCountExceedsLimit_ShouldReturnTrue()
    {
        // Arrange
        var rule = new ConversationMessageLimitRule(10_001, MaxMessages);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
    }

    [Test]
    public void IsBroken_WithCustomLimit_ShouldUseCustomValue()
    {
        // Arrange
        const int customLimit = 100;
        var rule = new ConversationMessageLimitRule(100, customLimit);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Message.ShouldBe($"Conversation cannot exceed {customLimit} messages.");
    }

    [Test]
    public void IsBroken_WithZeroCount_ShouldReturnFalse()
    {
        // Arrange
        var rule = new ConversationMessageLimitRule(0, MaxMessages);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }
}