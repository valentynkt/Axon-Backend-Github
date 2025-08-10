using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class MessageContentTests
{
    public class CreateMethod
    {
        [Fact]
        public void Should_ReturnFailure_When_ValueIsNull()
        {
            // Act
            var result = MessageContent.Create(null);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.MESSAGE.NULL");
            result.Error.Message.ShouldBe("Message content cannot be null");
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueIsEmpty()
        {
            // Act
            var result = MessageContent.Create("");

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.MESSAGE.EMPTY");
            result.Error.Message.ShouldBe("Message content cannot be empty");
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueIsWhitespaceOnly()
        {
            // Act
            var result = MessageContent.Create("   ");

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.MESSAGE.EMPTY");
            result.Error.Message.ShouldBe("Message content cannot be empty");
        }

        [Fact]
        public void Should_TrimWhitespaceAndReturnSuccess()
        {
            // Arrange
            var input = "  Hello World  ";

            // Act
            var result = MessageContent.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("Hello World");
            result.Value.Length.ShouldBe(11);
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsExactly100000Characters()
        {
            // Arrange
            var input = new string('a', 100_000);

            // Act
            var result = MessageContent.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(input);
            result.Value.Length.ShouldBe(100_000);
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueExceeds100000Characters()
        {
            // Arrange
            var input = new string('a', 100_001);

            // Act
            var result = MessageContent.Create(input);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.MESSAGE.TOO_LONG");
            result.Error.Message.ShouldBe("Message content cannot exceed 100000 characters");
        }

        [Fact]
        public void Should_ReturnFailure_When_TrimmedValueExceeds100000Characters()
        {
            // Arrange
            var input = "  " + new string('a', 100_001) + "  ";

            // Act
            var result = MessageContent.Create(input);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.MESSAGE.TOO_LONG");
            result.Error.Message.ShouldBe("Message content cannot exceed 100000 characters");
        }

        [Fact]
        public void Should_ReturnSuccess_When_TrimmedValueIs100000Characters()
        {
            // Arrange
            var input = "  " + new string('a', 100_000) + "  ";

            // Act
            var result = MessageContent.Create(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(new string('a', 100_000));
            result.Value.Length.ShouldBe(100_000);
        }

        [Theory]
        [InlineData("Hello World")]
        [InlineData("Message with numbers 123")]
        [InlineData("Message\nwith\nnewlines")]
        [InlineData("Message\twith\ttabs")]
        [InlineData("Message with special characters !@#$%")]
        [InlineData("UPPERCASE MESSAGE")]
        [InlineData("lowercase message")]
        [InlineData("Mixed Case Message")]
        public void Should_ReturnSuccess_For_ValidContent(string content)
        {
            // Act
            var result = MessageContent.Create(content);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe(content.Trim());
        }
    }

    public class PreviewMethod
    {
        [Fact]
        public void Should_ReturnFullContent_When_ContentIsShorterThanMax()
        {
            // Arrange
            var content = MessageContent.Create("Hello World").Value;

            // Act
            var preview = content.Preview(20);

            // Assert
            preview.ShouldBe("Hello World");
        }

        [Fact]
        public void Should_ReturnSubstring_When_ContentIsLongerThanMax()
        {
            // Arrange
            var content = MessageContent.Create("Hello World Test Message").Value;

            // Act
            var preview = content.Preview(10);

            // Assert
            preview.ShouldBe("Hello Worl");
        }

        [Fact]
        public void Should_UseDefaultMaxOf100_When_NoParameterProvided()
        {
            // Arrange
            var longContent = new string('a', 150);
            var content = MessageContent.Create(longContent).Value;

            // Act
            var preview = content.Preview();

            // Assert
            preview.ShouldBe(new string('a', 100));
            preview.Length.ShouldBe(100);
        }

        [Fact]
        public void Should_ReturnEmptyString_When_MaxIsZero()
        {
            // Arrange
            var content = MessageContent.Create("Hello World").Value;

            // Act
            var preview = content.Preview(0);

            // Assert
            preview.ShouldBe("");
        }

        [Fact]
        public void Should_ReturnEmptyString_When_MaxIsNegative()
        {
            // Arrange
            var content = MessageContent.Create("Hello World").Value;

            // Act
            var preview = content.Preview(-5);

            // Assert
            preview.ShouldBe("");
        }

        [Fact]
        public void Should_ReturnExactSubstring_Without_Ellipsis()
        {
            // Arrange
            var content = MessageContent.Create("Hello World Test").Value;

            // Act
            var preview = content.Preview(5);

            // Assert
            preview.ShouldBe("Hello");
            preview.ShouldNotContain("...");
        }

        [Fact]
        public void Should_HandleUnicodeCharacters()
        {
            // Arrange
            var content = MessageContent.Create("Hello 🌍 World 🚀").Value;

            // Act
            var preview = content.Preview(10);

            // Assert
            preview.ShouldBe("Hello 🌍 W");
        }
    }

    public class PropertiesAndMethods
    {
        [Fact]
        public void Length_Should_ReturnCorrectValue()
        {
            // Arrange
            var content = MessageContent.Create("Hello World").Value;
            
            // Act & Assert
            content.Length.ShouldBe(11);
        }

        [Fact]
        public void ToString_Should_ReturnValue()
        {
            // Arrange
            var content = MessageContent.Create("Hello World").Value;
            
            // Act & Assert
            content.ToString().ShouldBe("Hello World");
        }
    }

    public class ValidationMethod
    {
        [Fact]
        public void Should_ReturnValid_For_ValidContent()
        {
            // Arrange
            var content = MessageContent.Create("Valid content").Value;

            // Act
            var validation = content.Validate();

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
            var content1 = MessageContent.Create("Hello World").Value;
            var content2 = MessageContent.Create("Hello World").Value;

            // Act & Assert
            content1.ShouldBe(content2);
            content1.GetHashCode().ShouldBe(content2.GetHashCode());
        }

        [Fact]
        public void Should_NotBeEqual_When_ValuesAreDifferent()
        {
            // Arrange
            var content1 = MessageContent.Create("Hello World").Value;
            var content2 = MessageContent.Create("Goodbye World").Value;

            // Act & Assert
            content1.ShouldNotBe(content2);
        }

        [Fact]
        public void Should_BeEqual_When_BothHaveSameTrimmedValue()
        {
            // Arrange
            var content1 = MessageContent.Create("Hello World").Value;
            var content2 = MessageContent.Create("  Hello World  ").Value; // Trimmed to same value

            // Act & Assert
            content1.ShouldBe(content2);
            content1.GetHashCode().ShouldBe(content2.GetHashCode());
        }
    }
}