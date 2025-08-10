using Axon.Modules.Chat.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class TitleProvidedMustBeValidRuleTests
{
    private const int MaxTitleLength = 200;

    [Test]
    public void IsBroken_WhenTitleIsNull_ShouldReturnFalse()
    {
        // Arrange
        var rule = new TitleProvidedMustBeValidRule(null);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleIsEmpty_ShouldReturnFalse()
    {
        // Arrange
        var rule = new TitleProvidedMustBeValidRule("");

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleIsOneCharacter_ShouldReturnFalse()
    {
        // Arrange
        var rule = new TitleProvidedMustBeValidRule("A");

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleIsExactlyMaxLength_ShouldReturnFalse()
    {
        // Arrange
        var title = new string('A', MaxTitleLength);
        var rule = new TitleProvidedMustBeValidRule(title);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleExceedsMaxLength_ShouldReturnTrue()
    {
        // Arrange
        var title = new string('A', MaxTitleLength + 1);
        var rule = new TitleProvidedMustBeValidRule(title);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_TOO_LONG");
        rule.Message.ShouldBe("Title cannot exceed 200 characters.");
    }

    [Test]
    public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
    {
        // Arrange
        var rule = new TitleProvidedMustBeValidRule("Valid title");

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
    }
}