using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class ConversationIdTests
{
    [Test]
    public void New_GivenNoParameters_ShouldCreateUniqueConversationIds()
    {
        // Arrange & Act
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Assert
        conversationId1.ShouldNotBe(conversationId2);
        conversationId1.Value.ShouldNotBe(conversationId2.Value);
    }

    [Test]
    public void Create_GivenValidGuid_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var result = ConversationId.Create(validGuid);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(validGuid);
    }

    [Test]
    public void Create_GivenEmptyGuid_ShouldReturnValidationError()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = ConversationId.Create(emptyGuid);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    [Test]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var conversationId = ConversationId.New();

        // Act
        var result = conversationId.ToString();

        // Assert
        result.ShouldBe(conversationId.Value.ToString());
    }

    [Test]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var conversationId1 = ConversationId.Create(guid).Value;
        var conversationId2 = ConversationId.Create(guid).Value;

        // Act & Assert
        conversationId1.Equals(conversationId2).ShouldBeTrue();
        (conversationId1 == conversationId2).ShouldBeTrue();
        (conversationId1 != conversationId2).ShouldBeFalse();
    }
}