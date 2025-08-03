using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class MessageIdTests
{
    [Test]
    public void New_GivenNoParameters_ShouldCreateUniqueMessageIds()
    {
        // Arrange & Act
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Assert
        messageId1.ShouldNotBe(messageId2);
        messageId1.Value.ShouldNotBe(messageId2.Value);
    }

    [Test]
    public void Create_GivenValidGuid_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var result = MessageId.Create(validGuid);

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
        var result = MessageId.Create(emptyGuid);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    [Test]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var messageId = MessageId.New();

        // Act
        var result = messageId.ToString();

        // Assert
        result.ShouldBe(messageId.Value.ToString());
    }

    [Test]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.Create(guid).Value;
        var messageId2 = MessageId.Create(guid).Value;

        // Act & Assert
        messageId1.Equals(messageId2).ShouldBeTrue();
        (messageId1 == messageId2).ShouldBeTrue();
        (messageId1 != messageId2).ShouldBeFalse();
    }

    [Test]
    public void GetHashCode_WithSameValue_ShouldReturnSameHashCode()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.Create(guid).Value;
        var messageId2 = MessageId.Create(guid).Value;

        // Act & Assert
        messageId1.GetHashCode().ShouldBe(messageId2.GetHashCode());
    }
}