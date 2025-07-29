using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using FluentAssertions;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class MessageIdTests
{
    [Fact]
    public void New_ShouldCreateUniqueMessageId_GivenNoParameters()
    {
        // Arrange & Act
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Assert
        messageId1.Value.Should().NotBe(Guid.Empty);
        messageId2.Value.Should().NotBe(Guid.Empty);
        messageId1.Value.Should().NotBe(messageId2.Value);
    }

    [Fact]
    public void Create_ShouldReturnSuccessResult_GivenValidGuid()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var result = MessageId.Create(validGuid);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(validGuid);
    }

    [Fact]
    public void Create_ShouldReturnValidationError_GivenEmptyGuid()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = MessageId.Create(emptyGuid);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MessageId cannot be empty");
    }

    [Fact]
    public void Create_ShouldReturnSuccessResult_GivenValidGuidString()
    {
        // Arrange
        var validGuid = Guid.NewGuid();
        var validGuidString = validGuid.ToString();

        // Act
        var result = MessageId.Create(validGuidString);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(validGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldReturnValidationError_GivenNullOrWhitespaceString(string? input)
    {
        // Act
        var result = MessageId.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MessageId string cannot be null or empty");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("invalid-guid-format")]
    [InlineData("123e4567-e89b-12d3-a456-42661417400")]  // Missing last character
    public void Create_ShouldReturnValidationError_GivenInvalidGuidString(string invalidGuidString)
    {
        // Act
        var result = MessageId.Create(invalidGuidString);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("MessageId must be a valid GUID format");
    }

    [Fact]
    public void ToString_ShouldReturnGuidString_GivenValidMessageId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId = MessageId.Create(guid).Value;

        // Act
        var result = messageId.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToGuid_GivenValidMessageId()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();
        var messageId = MessageId.Create(originalGuid).Value;

        // Act
        Guid convertedGuid = messageId;

        // Assert
        convertedGuid.Should().Be(originalGuid);
    }

    [Fact]
    public void Equality_ShouldReturnTrue_GivenSameGuidValues()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.Create(guid).Value;
        var messageId2 = MessageId.Create(guid).Value;

        // Act & Assert
        messageId1.Should().Be(messageId2);
        messageId1.Equals(messageId2).Should().BeTrue();
        (messageId1 == messageId2).Should().BeTrue();
        (messageId1 != messageId2).Should().BeFalse();
    }

    [Fact]
    public void Equality_ShouldReturnFalse_GivenDifferentGuidValues()
    {
        // Arrange
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Act & Assert
        messageId1.Should().NotBe(messageId2);
        messageId1.Equals(messageId2).Should().BeFalse();
        (messageId1 == messageId2).Should().BeFalse();
        (messageId1 != messageId2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameValue_GivenSameGuidValues()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var messageId1 = MessageId.Create(guid).Value;
        var messageId2 = MessageId.Create(guid).Value;

        // Act
        var hashCode1 = messageId1.GetHashCode();
        var hashCode2 = messageId2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }
}