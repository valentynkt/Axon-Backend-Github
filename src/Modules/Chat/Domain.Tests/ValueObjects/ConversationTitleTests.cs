using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
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
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.TITLE.INVALID");
            result.Error.Message.ShouldBe("Title is invalid");
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsEmpty()
        {
            // Act
            var result = ConversationTitle.Create("");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("");
            result.Value.IsEmpty.ShouldBeTrue();
            result.Value.Length.ShouldBe(0);
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsWhitespaceOnly()
        {
            // Act
            var result = ConversationTitle.Create("   ");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("");
            result.Value.IsEmpty.ShouldBeTrue();
            result.Value.Length.ShouldBe(0);
        }

        [Fact]
        public void Should_TrimLeadingAndTrailingWhitespace()
        {
            // Arrange
            var input = "  Hello World  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("Hello World");
            result.Value.Length.ShouldBe(11);
        }

        [Fact]
        public void Should_PreserveInternalWhitespaceAndCase()
        {
            // Arrange
            var input = "  Hello   World Test  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("Hello   World Test");
            result.Value.Length.ShouldBe(18);
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsExactly200Characters()
        {
            // Arrange
            var input = new string('a', 200);

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(input);
            result.Value.Length.ShouldBe(200);
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueExceeds200Characters()
        {
            // Arrange
            var input = new string('a', 201);

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.TITLE.TOO_LONG");
            result.Error.Message.ShouldBe("Title cannot exceed 200 characters");
        }

        [Fact]
        public void Should_ReturnFailure_When_TrimmedValueExceeds200Characters()
        {
            // Arrange
            var input = "  " + new string('a', 201) + "  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.TITLE.TOO_LONG");
            result.Error.Message.ShouldBe("Title cannot exceed 200 characters");
        }

        [Fact]
        public void Should_ReturnSuccess_When_TrimmedValueIs200Characters()
        {
            // Arrange
            var input = "  " + new string('a', 200) + "  ";

            // Act
            var result = ConversationTitle.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(new string('a', 200));
            result.Value.Length.ShouldBe(200);
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
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(title.Trim());
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
            result.Value.IsEmpty.ShouldBeTrue();
        }

        [Fact]
        public void IsEmpty_Should_ReturnFalse_For_NonEmptyTitle()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.IsEmpty.ShouldBeFalse();
        }

        [Fact]
        public void Length_Should_ReturnCorrectValue()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.Length.ShouldBe(10);
        }

        [Fact]
        public void ToString_Should_ReturnValue()
        {
            // Arrange
            var result = ConversationTitle.Create("Test Title");
            
            // Act & Assert
            result.Value.ToString().ShouldBe("Test Title");
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
            validation.IsValid.ShouldBeTrue();
        }

        [Fact]
        public void Should_ReturnValid_For_EmptyTitle()
        {
            // Arrange
            var title = ConversationTitle.Create("").Value;

            // Act
            var validation = title.Validate();

            // Assert
            validation.IsValid.ShouldBeTrue();
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
            title1.ShouldBe(title2);
            title1.GetHashCode().ShouldBe(title2.GetHashCode());
        }

        [Fact]
        public void Should_NotBeEqual_When_ValuesAreDifferent()
        {
            // Arrange
            var title1 = ConversationTitle.Create("Test Title 1").Value;
            var title2 = ConversationTitle.Create("Test Title 2").Value;

            // Act & Assert
            title1.ShouldNotBe(title2);
        }

        [Fact]
        public void Should_BeEqual_When_BothAreEmpty()
        {
            // Arrange
            var title1 = ConversationTitle.Create("").Value;
            var title2 = ConversationTitle.Create("   ").Value; // Trimmed to empty

            // Act & Assert
            title1.ShouldBe(title2);
            title1.GetHashCode().ShouldBe(title2.GetHashCode());
        }
    }
}