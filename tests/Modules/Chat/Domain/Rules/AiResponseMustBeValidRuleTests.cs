// Test Infrastructure - Common Test Base Classes and Helpers
using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

[TestFixture]
public class AiResponseMustBeValidRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : AiResponseMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithValidResponseId_ShouldReturnFalse()
        {
            // Arrange
            var validResponseId = "valid-response-id";
            var rule = new AiResponseMustBeValidRule(validResponseId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid response ID");
        }

        [Test]
        public void IsBroken_WithNullResponseId_ShouldReturnTrue()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule(null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with null response ID");
        }

        [Test]
        public void IsBroken_WithEmptyString_ShouldReturnTrue()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty string");
        }

        [Test]
        public void IsBroken_WithWhitespaceString_ShouldReturnTrue()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule("   ");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace string");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : AiResponseMustBeValidRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule(null);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : AiResponseMustBeValidRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule("test");

            // Act & Assert
            rule.Code.ShouldBe(ChatDomainErrors.AiProcessing.InvalidResponseIdCode);
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule("test");

            // Act & Assert
            rule.Message.ShouldBe(ChatDomainErrors.AiProcessing.InvalidResponseIdMessage);
        }
    }

    [TestFixture]
    public class EdgeCaseTests : AiResponseMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithGuidAsString_ShouldReturnFalse()
        {
            // Arrange
            var guidResponseId = Guid.NewGuid().ToString();
            var rule = new AiResponseMustBeValidRule(guidResponseId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with GUID as response ID");
        }

        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var responseId = "response-id-123_abc!@#";
            var rule = new AiResponseMustBeValidRule(responseId);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with special characters in response ID");
        }
    }

    [TestFixture]
    public class ConsistencyTests : AiResponseMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule(null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new AiResponseMustBeValidRule(null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }
}