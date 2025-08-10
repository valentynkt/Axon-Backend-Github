using Axon.Modules.Chat.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class ConversationTitleTests
{
    public class CreateMethod
    {
        [Fact]
        public void Should_ReturnFailure_When_ValueIsNull()
        {
            // Act
            var result = ConversationTitle.Create(null);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("CHAT.TITLE.INVALID");
            result.Error.Message.Should().Be("Title is invalid");
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsEmpty()
        {
            // Act
            var result = ConversationTitle.Create("");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be("");
            result.Value.IsEmpty.Should().BeTrue();
            result.Value.Length.Should().Be(0);
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsWhitespaceOnly()
        {
            // Act
            var result = ConversationTitle.Create("   ");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be("");
            result.Value.IsEmpty.Should().BeTrue();
            result.Value.Length.Should().Be(0);
        }

        [Fact]
        public void Should_TrimLeadingAndTrailingWhitespace()
        {
            // Arrange
            var input = "  Hello World  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be("Hello World");
            result.Value.Length.Should().Be(11);
        }

        [Fact]
        public void Should_PreserveInternalWhitespaceAndCase()
        {
            // Arrange
            var input = "  Hello   World Test  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be("Hello   World Test");
            result.Value.Length.Should().Be(18);
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsExactly200Characters()
        {
            // Arrange
            var input = new string('a', 200);

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(input);
            result.Value.Length.Should().Be(200);
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueExceeds200Characters()
        {
            // Arrange
            var input = new string('a', 201);

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("CHAT.TITLE.TOO_LONG");
            result.Error.Message.Should().Be("Title cannot exceed 200 characters");
        }

        [Fact]
        public void Should_ReturnFailure_When_TrimmedValueExceeds200Characters()
        {
            // Arrange
            var input = "  " + new string('a', 201) + "  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("CHAT.TITLE.TOO_LONG");
            result.Error.Message.Should().Be("Title cannot exceed 200 characters");
        }

        [Fact]
        public void Should_ReturnSuccess_When_TrimmedValueIs200Characters()
        {
            // Arrange
            var input = "  " + new string('a', 200) + "  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(new string('a', 200));
            result.Value.Length.Should().Be(200);
        }

        [Theory]
        [InlineData("Valid Title")]
        [InlineData("Title with Numbers 123")]
        [InlineData("Title-with-hyphens")]
        [InlineData("Title_with_underscores")]
        [InlineData("Title with Special @#$% Characters")]
        [InlineData("UPPERCASE TITLE")]
        [InlineData("lowercase title")]
        [InlineData("Mixed Case Title")]
        public void Should_ReturnSuccess_For_ValidTitles(string title)
        {
            // Act
            var result = ConversationTitle.Create(title);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(title.Trim());
        }
    }

    public class PropertiesAndMethods
    {
        [Fact]
        public void IsEmpty_Should_ReturnTrue_For_EmptyTitle()
        {
            // Arrange
            var result = ConversationTitle.Create("");
            
            // Act & Assert
            result.Value.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void IsEmpty_Should_ReturnFalse_For_NonEmptyTitle()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.IsEmpty.Should().BeFalse();
        }

        [Fact]
        public void Length_Should_ReturnCorrectValue()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.Length.Should().Be(10);
        }

        [Fact]
        public void ToString_Should_ReturnValue()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.ToString().Should().Be("Test Title");
        }
    }

    public class ValidationMethod
    {
        [Fact]
        public void Should_ReturnValid_For_ValidTitle()
        {
            // Arrange
            var title = ConversationTitle.Create("Valid Title").Value;

            // Act
            var validation = title.Validate();

            // Assert
            validation.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Should_ReturnValid_For_EmptyTitle()
        {
            // Arrange
            var title = ConversationTitle.Create("").Value;

            // Act
            var validation = title.Validate();

            // Assert
            validation.IsValid.Should().BeTrue();
        }
    }

    public class EqualityAndHashing
    {
        [Fact]
        public void Should_BeEqual_When_ValuesAreEqual()
        {
            // Arrange
            var title1 = ConversationTitle.Create("Test Title").Value;
            var title2 = ConversationTitle.Create("Test Title").Value;

            // Act & Assert
            title1.Should().Be(title2);
            title1.GetHashCode().Should().Be(title2.GetHashCode());
        }

        [Fact]
        public void Should_NotBeEqual_When_ValuesAreDifferent()
        {
            // Arrange
            var title1 = ConversationTitle.Create("Test Title 1").Value;
            var title2 = ConversationTitle.Create("Test Title 2").Value;

            // Act & Assert
            title1.Should().NotBe(title2);
        }

        [Fact]
        public void Should_BeEqual_When_BothAreEmpty()
        {
            // Arrange
            var title1 = ConversationTitle.Create("").Value;
            var title2 = ConversationTitle.Create("   ").Value; // Trimmed to empty

            // Act & Assert
            title1.Should().Be(title2);
            title1.GetHashCode().Should().Be(title2.GetHashCode());
        }
    }
}