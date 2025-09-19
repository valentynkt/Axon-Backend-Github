// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class UserIdMustBeValidFormatRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : UserIdMustBeValidFormatRuleTests
    {
        [Test]
        public void IsBroken_WithValidGuid_ShouldReturnFalse()
        {
            // Arrange
            var validAxonUserId = Guid.NewGuid().ToString();
            var rule = new UserIdMustBeValidFormatRule(validAxonUserId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid GUID format");
        }

        [Test]
        public void IsBroken_WithNullAxonUserId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule(null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with null userId");
        }

        [Test]
        public void IsBroken_WithEmptyString_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty string");
        }

        [Test]
        public void IsBroken_WithWhitespaceString_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("   ");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace string");
        }

        [Test]
        public void IsBroken_WithInvalidGuidFormat_ShouldReturnTrue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("invalid-guid-format");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with invalid GUID format");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : UserIdMustBeValidFormatRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var validAxonUserId = Guid.NewGuid().ToString();
            var rule = new UserIdMustBeValidFormatRule(validAxonUserId);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : UserIdMustBeValidFormatRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("test");

            // Act & Assert
            rule.Code.ShouldBe(ChatDomainErrors.Authentication.InvalidUserIdCode);
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("test");

            // Act & Assert
            rule.Message.ShouldBe(ChatDomainErrors.Authentication.InvalidUserIdMessage);
        }
    }

    [TestFixture]
    public class EdgeCaseTests : UserIdMustBeValidFormatRuleTests
    {
        [Test]
        public void IsBroken_WithGuidInUpperCase_ShouldReturnFalse()
        {
            // Arrange
            var validAxonUserId = Guid.NewGuid().ToString().ToUpperInvariant();
            var rule = new UserIdMustBeValidFormatRule(validAxonUserId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with uppercase GUID");
        }

        [Test]
        public void IsBroken_WithGuidWithHyphens_ShouldReturnFalse()
        {
            // Arrange
            var validAxonUserId = Guid.NewGuid().ToString("D"); // Default format with hyphens
            var rule = new UserIdMustBeValidFormatRule(validAxonUserId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with hyphenated GUID format");
        }
    }

    [TestFixture]
    public class ConsistencyTests : UserIdMustBeValidFormatRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("invalid-format");

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new UserIdMustBeValidFormatRule("invalid-format");

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }
}