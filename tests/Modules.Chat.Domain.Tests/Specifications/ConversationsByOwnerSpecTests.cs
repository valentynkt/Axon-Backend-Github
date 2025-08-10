using Axon.Modules.Chat.Domain.Specifications;
using Axon.Modules.Chat.Domain.Tests.Specifications._Fixtures;

namespace Axon.Modules.Chat.Domain.Tests.Specifications;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Specification")]
public class ConversationsByOwnerSpecTests
{
    [Test]
    public void ToExpression_ShouldReturnCorrectExpression()
    {
        // Arrange
        var spec = new ConversationsByOwnerSpec(ConversationTestFixture.Owner1);
        var conversations = ConversationTestFixture.CreateTestConversations();

        // Act
        var result = conversations.AsQueryable().Where(spec.ToExpression()).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldAllBe(c => c.OwnerId == ConversationTestFixture.Owner1);
    }

    [Test]
    public void Constructor_WithNullOwnerId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ConversationsByOwnerSpec(null!))
            .ParamName.ShouldBe("ownerId");
    }

    [Test]
    public void IsSatisfiedBy_WithMatchingOwner_ShouldReturnTrue()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsByOwnerSpec(ConversationTestFixture.Owner1);
        var targetConversation = conversations.First(c => c.OwnerId == ConversationTestFixture.Owner1);

        // Act & Assert
        spec.IsSatisfiedBy(targetConversation).ShouldBeTrue();
    }

    [Test]
    public void IsSatisfiedBy_WithDifferentOwner_ShouldReturnFalse()
    {
        // Arrange
        var conversations = ConversationTestFixture.CreateTestConversations();
        var spec = new ConversationsByOwnerSpec(ConversationTestFixture.Owner1);
        var differentOwnerConversation = conversations.First(c => c.OwnerId == ConversationTestFixture.Owner2);

        // Act & Assert
        spec.IsSatisfiedBy(differentOwnerConversation).ShouldBeFalse();
    }
}