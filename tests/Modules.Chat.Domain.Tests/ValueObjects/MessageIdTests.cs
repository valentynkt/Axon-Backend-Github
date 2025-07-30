using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class MessageIdTests : DomainTestBase
{
    [Test]
    public void New_GivenNoParameters_ShouldCreateUniqueMessageIds()

    {
        // Arrange & Act
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Assert
        messageId1.Value.ShouldNotBe(Guid.Empty);
        messageId2.Value.ShouldNotBe(Guid.Empty);
        messageId1.Value.ShouldNotBe(messageId2.Value);
    }

    [Test]
    public void Create_GivenValidGuid_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = ValidGuid();


        // Act
        var result = MessageId.Create(validGuid);

        // Assert
        result.ShouldBeSuccessAnd(messageId => 
            messageId.Value.ShouldBe(validGuid));
    }

    [Test]
    public void Create_GivenEmptyGuid_ShouldReturnValidationError()

    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = MessageId.Create(emptyGuid);

        // Assert
        result.ShouldBeValidationFailure("MessageId cannot be empty");
    }

    [Test]
    public void Create_GivenValidGuidString_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var validGuid = ValidGuid();

        var validGuidString = validGuid.ToString();

        // Act
        var result = MessageId.Create(validGuidString);

        // Assert
        result.ShouldBeSuccessAnd(messageId => 
            messageId.Value.ShouldBe(validGuid));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_GivenNullOrWhitespaceString_ShouldReturnValidationError(string? input)

    {
        // Act
        var result = MessageId.Create(input);

        // Assert
        result.ShouldBeValidationFailure("MessageId string cannot be null or empty");
    }

    [Test]
    [TestCase("not-a-guid")]
    [TestCase("12345")]
    [TestCase("invalid-guid-format")]
    [TestCase("123e4567-e89b-12d3-a456-42661417400")]  // Missing last character
    public void Create_GivenInvalidGuidString_ShouldReturnValidationError(string invalidGuidString)

    {
        // Act
        var result = MessageId.Create(invalidGuidString);

        // Assert
        result.ShouldBeValidationFailure("MessageId must be a valid GUID format");
    }

    [Test]
    public void ToString_GivenValidMessageId_ShouldReturnGuidString()
    {
        // Arrange
        var guid = ValidGuid();
        var messageId = MessageId.Create(guid).ShouldBeSuccessWithValue();


        // Act
        var result = messageId.ToString();

        // Assert
        result.ShouldBe(guid.ToString());
    }

    [Test]
    public void ImplicitOperator_GivenValidMessageId_ShouldConvertToGuid()
    {
        // Arrange
        var originalGuid = ValidGuid();
        var messageId = MessageId.Create(originalGuid).ShouldBeSuccessWithValue();


        // Act
        Guid convertedGuid = messageId;

        // Assert
        convertedGuid.ShouldBe(originalGuid);
    }

    [Test]
    public void Equality_GivenSameGuidValues_ShouldReturnTrue()
    {
        // Arrange
        var guid = ValidGuid();
        var messageId1 = MessageId.Create(guid).ShouldBeSuccessWithValue();
        var messageId2 = MessageId.Create(guid).ShouldBeSuccessWithValue();

        // Act & Assert
        messageId1.ShouldBe(messageId2);
        messageId1.Equals(messageId2).ShouldBeTrue();
        (messageId1 == messageId2).ShouldBeTrue();
        (messageId1 != messageId2).ShouldBeFalse();
    }

    [Test]
    public void Equality_GivenDifferentGuidValues_ShouldReturnFalse()
    {
        // Arrange
        var messageId1 = ValidMessageId();
        var messageId2 = ValidMessageId();

        // Act & Assert
        messageId1.ShouldNotBe(messageId2);
        messageId1.Equals(messageId2).ShouldBeFalse();
        (messageId1 == messageId2).ShouldBeFalse();
        (messageId1 != messageId2).ShouldBeTrue();
    }

    [Test]
    public void GetHashCode_GivenSameGuidValues_ShouldReturnSameValue()
    {
        // Arrange
        var guid = ValidGuid();
        var messageId1 = MessageId.Create(guid).ShouldBeSuccessWithValue();
        var messageId2 = MessageId.Create(guid).ShouldBeSuccessWithValue();


        // Act
        var hashCode1 = messageId1.GetHashCode();
        var hashCode2 = messageId2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Test]
    public void Factory_ShouldCreateEqualMessageIdsFromSameGuid()
    {
        // Arrange & Act
        var (messageId1, messageId2) = ChatDomainFactory.EqualMessageIds();

        // Assert
        messageId1.ShouldBe(messageId2);
        messageId1.GetHashCode().ShouldBe(messageId2.GetHashCode());
    }

    [Test]
    public void Factory_ShouldCreateUniqueMessageIds()
    {
        // Arrange & Act
        var messageIds = ChatDomainFactory.ValidMessageIds(5).ToList();

        // Assert
        messageIds.Count.ShouldBe(5);
        messageIds.ShouldBeUnique();
        messageIds.ShouldAllBe(id => id.Value != Guid.Empty);

    }
}