using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class ConversationMustBeActiveRuleTests
{
    [Test]
    public void IsBroken_WhenStatusIsActive_ShouldReturnFalse()
    {
        // Arrange
        var rule = new ConversationMustBeActiveRule(ConversationStatus.Active);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenStatusIsCompleted_ShouldReturnTrue()
    {
        // Arrange
        var rule = new ConversationMustBeActiveRule(ConversationStatus.Completed);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeTrue();
        rule.Code.ShouldBe("CHAT.CONVERSATION.NOT_ACTIVE");
        rule.Message.ShouldBe("Conversation must be active.");
    }
}