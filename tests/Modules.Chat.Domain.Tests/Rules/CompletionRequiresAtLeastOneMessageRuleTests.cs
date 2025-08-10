using Axon.Modules.Chat.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class CompletionRequiresAtLeastOneMessageRuleTests
{
    [Test]
    public void IsBroken_WhenMessageCountIsZero_ShouldReturnTrue()
    {
        // Arrange
        var rule = new CompletionRequiresAtLeastOneMessageRule(0);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_EMPTY_ON_COMPLETE");
        rule.Message.ShouldBe("Cannot complete an empty conversation.");
    }

    [Test]
    public void IsBroken_WhenMessageCountIsOne_ShouldReturnFalse()
    {
        // Arrange
        var rule = new CompletionRequiresAtLeastOneMessageRule(1);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenMessageCountIsMany_ShouldReturnFalse()
    {
        // Arrange
        var rule = new CompletionRequiresAtLeastOneMessageRule(100);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
    {
        // Arrange
        var rule = new CompletionRequiresAtLeastOneMessageRule(5);

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeFalse();
    }
}