using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.ValueObjects;

public sealed class MessageRoleTests
{
    public class FromStringMethod
    {
        [Fact]
        public void Should_ReturnFailure_When_ValueIsNull()
        {
            // Act
            var result = MessageRole.FromString(null);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.ROLE.INVALID");
            result.Error.Message.ShouldBe("Invalid message role (allowed: user, assistant)");
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueIsEmpty()
        {
            // Act
            var result = MessageRole.FromString("");

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.ROLE.INVALID");
            result.Error.Message.ShouldBe("Invalid message role (allowed: user, assistant)");
        }

        [Fact]
        public void Should_ReturnFailure_When_ValueIsWhitespaceOnly()
        {
            // Act
            var result = MessageRole.FromString("   ");

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.ROLE.INVALID");
            result.Error.Message.ShouldBe("Invalid message role (allowed: user, assistant)");
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsUser()
        {
            // Act
            var result = MessageRole.FromString("user");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("user");
            result.Value.IsUser.ShouldBeTrue();
            result.Value.IsAssistant.ShouldBeFalse();
        }

        [Fact]
        public void Should_ReturnSuccess_When_ValueIsAssistant()
        {
            // Act
            var result = MessageRole.FromString("assistant");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("assistant");
            result.Value.IsUser.ShouldBeFalse();
            result.Value.IsAssistant.ShouldBeTrue();
        }

        [Theory]
        [InlineData("USER")]
        [InlineData("User")]
        [InlineData("UsEr")]
        [InlineData("uSeR")]
        public void Should_ReturnSuccess_When_ValueIsUser_CaseInsensitive(string input)
        {
            // Act
            var result = MessageRole.FromString(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("user");
            result.Value.IsUser.ShouldBeTrue();
            result.Value.IsAssistant.ShouldBeFalse();
        }

        [Theory]
        [InlineData("ASSISTANT")]
        [InlineData("Assistant")]
        [InlineData("AssiStant")]
        [InlineData("aSSISTANT")]
        public void Should_ReturnSuccess_When_ValueIsAssistant_CaseInsensitive(string input)
        {
            // Act
            var result = MessageRole.FromString(input);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("assistant");
            result.Value.IsUser.ShouldBeFalse();
            result.Value.IsAssistant.ShouldBeTrue();
        }

        [Fact]
        public void Should_TrimWhitespaceAndReturnSuccess_For_User()
        {
            // Act
            var result = MessageRole.FromString("  user  ");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("user");
            result.Value.IsUser.ShouldBeTrue();
        }

        [Fact]
        public void Should_TrimWhitespaceAndReturnSuccess_For_Assistant()
        {
            // Act
            var result = MessageRole.FromString("  assistant  ");

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.Value.ShouldBe("assistant");
            result.Value.IsAssistant.ShouldBeTrue();
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("system")]
        [InlineData("bot")]
        [InlineData("human")]
        [InlineData("invalid")]
        [InlineData("users")]
        [InlineData("assistants")]
        [InlineData("user123")]
        [InlineData("assistant_bot")]
        public void Should_ReturnFailure_For_InvalidRoles(string invalidRole)
        {
            // Act
            var result = MessageRole.FromString(invalidRole);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("CHAT.ROLE.INVALID");
            result.Error.Message.ShouldBe("Invalid message role (allowed: user, assistant)");
        }
    }

    public class PredefinedRoles
    {
        [Fact]
        public void User_Should_ReturnUserRole()
        {
            // Act
            var role = MessageRole.User;

            // Assert
            role.Value.ShouldBe("user");
            role.IsUser.ShouldBeTrue();
            role.IsAssistant.ShouldBeFalse();
        }

        [Fact]
        public void Assistant_Should_ReturnAssistantRole()
        {
            // Act
            var role = MessageRole.Assistant;

            // Assert
            role.Value.ShouldBe("assistant");
            role.IsUser.ShouldBeFalse();
            role.IsAssistant.ShouldBeTrue();
        }

        [Fact]
        public void PredefinedRoles_Should_BeEqualToFromStringResults()
        {
            // Arrange
            var userFromString = MessageRole.FromString("user").Value;
            var assistantFromString = MessageRole.FromString("assistant").Value;

            // Act & Assert
            MessageRole.User.ShouldBe(userFromString);
            MessageRole.Assistant.ShouldBe(assistantFromString);
        }
    }

    public class ConvenienceProperties
    {
        [Fact]
        public void IsUser_Should_ReturnTrue_For_UserRole()
        {
            // Arrange
            var role = MessageRole.User;

            // Act & Assert
            role.IsUser.ShouldBeTrue();
        }

        [Fact]
        public void IsUser_Should_ReturnFalse_For_AssistantRole()
        {
            // Arrange
            var role = MessageRole.Assistant;

            // Act & Assert
            role.IsUser.ShouldBeFalse();
        }

        [Fact]
        public void IsAssistant_Should_ReturnTrue_For_AssistantRole()
        {
            // Arrange
            var role = MessageRole.Assistant;

            // Act & Assert
            role.IsAssistant.ShouldBeTrue();
        }

        [Fact]
        public void IsAssistant_Should_ReturnFalse_For_UserRole()
        {
            // Arrange
            var role = MessageRole.User;

            // Act & Assert
            role.IsAssistant.ShouldBeFalse();
        }

        [Fact]
        public void ConvenienceProperties_Should_WorkWith_FromString_Results()
        {
            // Arrange
            var userRole = MessageRole.FromString("USER").Value;
            var assistantRole = MessageRole.FromString("ASSISTANT").Value;

            // Act & Assert
            userRole.IsUser.ShouldBeTrue();
            userRole.IsAssistant.ShouldBeFalse();
            
            assistantRole.IsUser.ShouldBeFalse();
            assistantRole.IsAssistant.ShouldBeTrue();
        }
    }

    public class PropertiesAndMethods
    {
        [Fact]
        public void ToString_Should_ReturnValue_For_User()
        {
            // Arrange
            var role = MessageRole.User;
            
            // Act & Assert
            role.ToString().ShouldBe("user");
        }

        [Fact]
        public void ToString_Should_ReturnValue_For_Assistant()
        {
            // Arrange
            var role = MessageRole.Assistant;
            
            // Act & Assert
            role.ToString().ShouldBe("assistant");
        }
    }

    public class ValidationMethod
    {
        [Fact]
        public void Should_ReturnValid_For_UserRole()
        {
            // Arrange
            var role = MessageRole.User;

            // Act
            var validation = role.Validate();

            // Assert
            validation.IsValid.ShouldBeTrue();
        }

        [Fact]
        public void Should_ReturnValid_For_AssistantRole()
        {
            // Arrange
            var role = MessageRole.Assistant;

            // Act
            var validation = role.Validate();

            // Assert
            validation.IsValid.ShouldBeTrue();
        }

        [Fact]
        public void Should_ReturnValid_For_FromString_Results()
        {
            // Arrange
            var userRole = MessageRole.FromString("user").Value;
            var assistantRole = MessageRole.FromString("assistant").Value;

            // Act
            var userValidation = userRole.Validate();
            var assistantValidation = assistantRole.Validate();

            // Assert
            userValidation.IsValid.ShouldBeTrue();
            assistantValidation.IsValid.ShouldBeTrue();
        }
    }

    public class EqualityAndHashing
    {
        [Fact]
        public void Should_BeEqual_When_BothAreUserRoles()
        {
            // Arrange
            var role1 = MessageRole.User;
            var role2 = MessageRole.FromString("user").Value;

            // Act & Assert
            role1.ShouldBe(role2);
            role1.GetHashCode().ShouldBe(role2.GetHashCode());
        }

        [Fact]
        public void Should_BeEqual_When_BothAreAssistantRoles()
        {
            // Arrange
            var role1 = MessageRole.Assistant;
            var role2 = MessageRole.FromString("assistant").Value;

            // Act & Assert
            role1.ShouldBe(role2);
            role1.GetHashCode().ShouldBe(role2.GetHashCode());
        }

        [Fact]
        public void Should_NotBeEqual_When_RolesAreDifferent()
        {
            // Arrange
            var userRole = MessageRole.User;
            var assistantRole = MessageRole.Assistant;

            // Act & Assert
            userRole.ShouldNotBe(assistantRole);
        }

        [Fact]
        public void Should_BeEqual_Regardless_Of_Case_Used_In_FromString()
        {
            // Arrange
            var role1 = MessageRole.FromString("user").Value;
            var role2 = MessageRole.FromString("USER").Value;

            // Act & Assert
            role1.ShouldBe(role2);
            role1.GetHashCode().ShouldBe(role2.GetHashCode());
        }
    }
}