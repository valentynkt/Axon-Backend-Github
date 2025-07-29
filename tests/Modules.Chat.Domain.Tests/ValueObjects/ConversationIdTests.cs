using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using FluentAssertions;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class ConversationIdTests
{
    [Fact]
    public void New_ShouldCreateUniqueConversationId_GivenNoParameters()
    {
        // Arrange & Act
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Assert
        conversationId1.Value.Should().NotBe(Guid.Empty);
        conversationId2.Value.Should().NotBe(Guid.Empty);
        conversationId1.Value.Should().NotBe(conversationId2.Value);
    }

    [Fact]
    public void Create_ShouldReturnSuccessResult_GivenValidGuid()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var result = ConversationId.Create(validGuid);

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
        var result = ConversationId.Create(emptyGuid);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("ConversationId cannot be empty");
    }

    [Fact]
    public void Create_ShouldReturnSuccessResult_GivenValidGuidString()
    {
        // Arrange
        var validGuid = Guid.NewGuid();
        var validGuidString = validGuid.ToString();

        // Act
        var result = ConversationId.Create(validGuidString);

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
        var result = ConversationId.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("ConversationId string cannot be null or empty");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("invalid-guid-format")]
    [InlineData("123e4567-e89b-12d3-a456-42661417400")]  // Missing last character
    public void Create_ShouldReturnValidationError_GivenInvalidGuidString(string invalidGuidString)
    {
        // Act
        var result = ConversationId.Create(invalidGuidString);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("ConversationId must be a valid GUID format");
    }

    [Fact]
    public void ToString_ShouldReturnGuidString_GivenValidConversationId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var conversationId = ConversationId.Create(guid).Value;

        // Act
        var result = conversationId.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToGuid_GivenValidConversationId()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();
        var conversationId = ConversationId.Create(originalGuid).Value;

        // Act
        Guid convertedGuid = conversationId;

        // Assert
        convertedGuid.Should().Be(originalGuid);
    }

    [Fact]
    public void Equality_ShouldReturnTrue_GivenSameGuidValues()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var conversationId1 = ConversationId.Create(guid).Value;
        var conversationId2 = ConversationId.Create(guid).Value;

        // Act & Assert
        conversationId1.Should().Be(conversationId2);
        conversationId1.Equals(conversationId2).Should().BeTrue();
        (conversationId1 == conversationId2).Should().BeTrue();
        (conversationId1 != conversationId2).Should().BeFalse();
    }

    [Fact]
    public void Equality_ShouldReturnFalse_GivenDifferentGuidValues()
    {
        // Arrange
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Act & Assert
        conversationId1.Should().NotBe(conversationId2);
        conversationId1.Equals(conversationId2).Should().BeFalse();
        (conversationId1 == conversationId2).Should().BeFalse();
        (conversationId1 != conversationId2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameValue_GivenSameGuidValues()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var conversationId1 = ConversationId.Create(guid).Value;
        var conversationId2 = ConversationId.Create(guid).Value;

        // Act
        var hashCode1 = conversationId1.GetHashCode();
        var hashCode2 = conversationId2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }
}