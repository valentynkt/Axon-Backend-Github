using Axon.Modules.Chat.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class TitleUpdateMustBeValidRuleTests
{
    private const int MaxTitleLength = 200;

    [Test]
    public void IsBroken_WhenTitleIsNull_ShouldReturnTrue()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule(null);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_EMPTY");
        rule.Message.ShouldBe("Title cannot be empty.");
    }

    [Test]
    public void IsBroken_WhenTitleIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule("");

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_EMPTY");
    }

    [Test]
    public void IsBroken_WhenTitleIsWhitespace_ShouldReturnTrue()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule("   ");

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_EMPTY");
    }

    [Test]
    public void IsBroken_WhenTitleIsOneCharacter_ShouldReturnFalse()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule("A");

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleIsExactlyMaxLength_ShouldReturnFalse()
    {
        // Arrange
        var title = new string('a', MaxTitleLength);
        var rule = new TitleUpdateMustBeValidRule(title);

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTitleExceedsMaxLength_ShouldReturnTrue()
    {
        // Arrange
        var title = new string('a', MaxTitleLength + 1);
        var rule = new TitleUpdateMustBeValidRule(title);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_TOO_LONG");
        rule.Message.ShouldBe("Title cannot exceed 200 characters.");
    }

    [Test]
    public void IsBroken_WhenTitleHasWhitespaceButValidAfterTrim_ShouldReturnFalse()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule("  Valid Title  ");

        // Act & Assert
        rule.IsBroken().ShouldBeFalse();
    }

    [Test]
    public void IsBroken_WhenTrimmedTitleExceedsMaxLength_ShouldReturnTrue()
    {
        // Arrange
        var title = "  " + new string('a', MaxTitleLength + 1) + "  ";
        var rule = new TitleUpdateMustBeValidRule(title);

        // Act & Assert
        rule.IsBroken().ShouldBeTrue();
        rule.Code.ShouldBe("CHAT_CONVERSATION_TITLE_TOO_LONG");
    }

    [Test]
    public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
    {
        // Arrange
        var rule = new TitleUpdateMustBeValidRule("Valid Title");

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeFalse();
    }
}