using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
public sealed class MessageIdTests
{
    #region New() Factory Method Tests

    [Test]
    public void New_ShouldCreateUniqueMessageIds()
    {
        // Arrange & Act
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Assert
        messageId1.ShouldNotBe(messageId2);
        messageId1.Value.ShouldNotBe(messageId2.Value);
        messageId1.Value.ShouldNotBe(Guid.Empty);
        messageId2.Value.ShouldNotBe(Guid.Empty);
    }

    [Test]
    public void New_ShouldCreateNonEmptyGuids()
    {
        // Arrange & Act
        var messageId = MessageId.New();

        // Assert
        messageId.Value.ShouldNotBe(Guid.Empty);
    }

    #endregion

    #region From(Guid) Factory Method Tests

    [Test]
    public void From_GivenValidGuid_ShouldReturnMessageIdWithCorrectValue()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var messageId = MessageId.From(validGuid);

        // Assert
        messageId.Value.ShouldBe(validGuid);
    }

    [Test]
    public void From_GivenEmptyGuid_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => MessageId.From(emptyGuid))
            .Message.ShouldContain("MessageId cannot be empty GUID");
    }

    #endregion

    #region FromString(string) Factory Method Tests

    [Test]
    public void FromString_GivenValidGuidString_ShouldReturnSuccessWithCorrectValue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = MessageId.FromString(guidString);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(guid);
    }

    [Test]
    public void FromString_GivenValidGuidStringWithDashes_ShouldReturnSuccess()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString("D"); // With dashes

        // Act
        var result = MessageId.FromString(guidString);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(guid);
    }

    [Test]
    public void FromString_GivenValidGuidStringWithoutDashes_ShouldReturnSuccess()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString("N"); // Without dashes

        // Act
        var result = MessageId.FromString(guidString);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(guid);
    }

    [Test]
    public void FromString_GivenEmptyString_ShouldReturnValidationError()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act
        var result = MessageId.FromString(emptyString);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.EMPTY");
        result.Error.Message.ShouldBe("MessageId cannot be empty.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void FromString_GivenNullString_ShouldReturnValidationError()
    {
        // Arrange
        string nullString = null!;

        // Act
        var result = MessageId.FromString(nullString);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.EMPTY");
        result.Error.Message.ShouldBe("MessageId cannot be empty.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void FromString_GivenWhitespaceString_ShouldReturnValidationError()
    {
        // Arrange
        var whitespaceString = "   ";

        // Act
        var result = MessageId.FromString(whitespaceString);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.EMPTY");
        result.Error.Message.ShouldBe("MessageId cannot be empty.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void FromString_GivenInvalidGuidFormat_ShouldReturnValidationError()
    {
        // Arrange
        var invalidString = "not-a-guid";

        // Act
        var result = MessageId.FromString(invalidString);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.INVALID_FORMAT");
        result.Error.Message.ShouldBe("MessageId has invalid format.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void FromString_GivenEmptyGuidString_ShouldReturnValidationError()
    {
        // Arrange
        var emptyGuidString = Guid.Empty.ToString();

        // Act
        var result = MessageId.FromString(emptyGuidString);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.EMPTY");
        result.Error.Message.ShouldBe("MessageId cannot be empty GUID.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void FromString_GivenPartiallyValidGuid_ShouldReturnValidationError()
    {
        // Arrange
        var partialGuid = "550e8400-e29b-41d4-a716-44665544000"; // Missing one character

        // Act
        var result = MessageId.FromString(partialGuid);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.ID.INVALID_FORMAT");
        result.Error.Message.ShouldBe("MessageId has invalid format.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    #endregion

    #region Equality Tests

    [Test]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.From(guid);
        var messageId2 = MessageId.From(guid);

        // Act & Assert
        messageId1.Equals(messageId2).ShouldBeTrue();
        (messageId1 == messageId2).ShouldBeTrue();
        (messageId1 != messageId2).ShouldBeFalse();
    }

    [Test]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Act & Assert
        messageId1.Equals(messageId2).ShouldBeFalse();
        (messageId1 == messageId2).ShouldBeFalse();
        (messageId1 != messageId2).ShouldBeTrue();
    }

    [Test]
    public void GetHashCode_WithSameValue_ShouldReturnSameHashCode()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.From(guid);
        var messageId2 = MessageId.From(guid);

        // Act & Assert
        messageId1.GetHashCode().ShouldBe(messageId2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Test]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId = MessageId.From(guid);

        // Act
        var result = messageId.ToString();

        // Assert
        result.ShouldBe(guid.ToString());
    }

    [Test]
    public void ToString_RoundTrip_ShouldBeSuccessful()
    {
        // Arrange
        var originalId = MessageId.New();
        var stringRepresentation = originalId.ToString();

        // Act
        var roundTripResult = MessageId.FromString(stringRepresentation);

        // Assert
        roundTripResult.IsSuccess.ShouldBeTrue();
        roundTripResult.Value.ShouldBe(originalId);
    }

    #endregion

    #region Domain Purity Tests

    [Test]
    public void MessageId_ShouldNotHaveJsonAttributes()
    {
        // Arrange & Act
        var type = typeof(MessageId);
        var attributes = type.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonConverterAttribute), false);

        // Assert
        attributes.ShouldBeEmpty("Domain entities should not have JSON serialization concerns");
    }

    [Test]
    public void MessageId_ShouldNotHaveSystemTextJsonDependencies()
    {
        // Arrange & Act
        var type = typeof(MessageId);
        var assembly = type.Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();

        // Assert
        referencedAssemblies.ShouldNotContain(ra => ra.Name!.Contains("System.Text.Json"), 
            "Domain layer should not directly reference JSON serialization libraries");
    }

    #endregion

    #region Implicit Conversion Tests

    [Test]
    public void ImplicitConversion_ToGuid_ShouldReturnUnderlyingValue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId = MessageId.From(guid);

        // Act
        Guid convertedGuid = messageId;

        // Assert
        convertedGuid.ShouldBe(guid);
    }

    #endregion
}