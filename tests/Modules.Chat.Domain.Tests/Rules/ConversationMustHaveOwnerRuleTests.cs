using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Rules")]
public sealed class ConversationMustHaveOwnerRuleTests
{
    [Test]
    public void IsBroken_WhenOwnerIdIsNull_ShouldReturnTrue()
    {
        // Arrange
        var rule = new ConversationMustHaveOwnerRule(null!);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeTrue();
        rule.Code.ShouldBe("CHAT.CONVERSATION.OWNER_REQUIRED");
        rule.Message.ShouldBe("Conversation must have an owner.");
    }

    [Test]
    public void IsBroken_WhenOwnerIdIsEmptyGuid_ShouldReturnTrue()
    {
        // Arrange
        var emptyUserId = UserId.From(Guid.Empty);
        var rule = new ConversationMustHaveOwnerRule(emptyUserId);

        // Act & Assert
        Should.Throw<ArgumentException>(() => UserId.From(Guid.Empty))
            .Message.ShouldContain("UserId cannot be empty GUID");
    }

    [Test]
    public void IsBroken_WhenOwnerIdIsValid_ShouldReturnFalse()
    {
        // Arrange
        var validUserId = UserId.New();
        var rule = new ConversationMustHaveOwnerRule(validUserId);

        // Act
        var isBroken = rule.IsBroken();

        // Assert
        isBroken.ShouldBeFalse();
    }

    [Test]
    public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
    {
        // Arrange
        var validUserId = UserId.New();
        var rule = new ConversationMustHaveOwnerRule(validUserId);

        // Act
        var syncResult = rule.IsBroken();
        var asyncResult = await rule.IsBrokenAsync();

        // Assert
        asyncResult.ShouldBe(syncResult);
        asyncResult.ShouldBeFalse();
    }
}