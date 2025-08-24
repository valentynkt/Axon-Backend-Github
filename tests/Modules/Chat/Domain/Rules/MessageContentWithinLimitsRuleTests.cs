using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for MessageContentWithinLimitsRule that validates message content constraints via MessageContent value object.
/// Tests content validation, length limits, and edge cases.
/// </summary>
[TestFixture]
public class MessageContentWithinLimitsRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void IsBroken_WithValidContent_ShouldReturnFalse()
        {
            // Arrange
            var validContent = TestConstants.Messages.DefaultUserMessage;
            var rule = new MessageContentWithinLimitsRule(validContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid message content");
        }

        [Test]
        public void IsBroken_WithEmptyString_ShouldReturnTrue()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty string content");
        }

        [Test]
        public void IsBroken_WithNullContent_ShouldReturnTrue()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule(null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with null content");
        }

        [Test]
        public void IsBroken_WithWhitespaceOnlyContent_ShouldReturnTrue()
        {
            // Arrange
            var whitespaceContent = "   \t\n   ";
            var rule = new MessageContentWithinLimitsRule(whitespaceContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace-only content");
        }

        [Test]
        public void IsBroken_WithContentAtMaxLength_ShouldReturnFalse()
        {
            // Arrange
            var maxLengthContent = TestConstants.EdgeCases.ExactMaxMessage;
            var rule = new MessageContentWithinLimitsRule(maxLengthContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with content at exact maximum length");
        }

        [Test]
        public void IsBroken_WithContentExceedingMaxLength_ShouldReturnTrue()
        {
            // Arrange
            var tooLongContent = TestConstants.EdgeCases.OneOverMaxMessage;
            var rule = new MessageContentWithinLimitsRule(tooLongContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with content exceeding maximum length");
        }

        [Test]
        public void IsBroken_WithValidContentContainingSpaces_ShouldReturnFalse()
        {
            // Arrange
            var validContent = TestConstants.Messages.ValidMessageWithSpaces;
            var rule = new MessageContentWithinLimitsRule(validContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid content containing spaces");
        }

        [Test]
        public void IsBroken_WithShortValidContent_ShouldReturnFalse()
        {
            // Arrange
            var shortContent = TestConstants.Messages.ShortMessage;
            var rule = new MessageContentWithinLimitsRule(shortContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with short valid content");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var content = TestConstants.Messages.DefaultUserMessage;
            var rule = new MessageContentWithinLimitsRule(content);

            // Act
            var syncResult = rule.IsBroken();
            var asyncResult = await rule.IsBrokenAsync();

            // Assert
            asyncResult.ShouldBe(syncResult, "IsBrokenAsync should return same result as IsBroken");
        }

        [Test]
        public async Task IsBrokenAsync_WithCancellationToken_ShouldComplete()
        {
            // Arrange
            var content = TestConstants.Messages.DefaultUserMessage;
            var rule = new MessageContentWithinLimitsRule(content);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid content");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule("test");

            // Act & Assert
            rule.Code.ShouldBe("CHAT.MESSAGE.CONTENT.INVALID");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule("test");

            // Act & Assert
            rule.Message.ShouldBe("Message content is invalid.");
        }
    }

    [TestFixture]
    public class ConstructorTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void Constructor_WithValidContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new MessageContentWithinLimitsRule("valid content");

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.MESSAGE.CONTENT.INVALID");
        }

        [Test]
        public void Constructor_WithNullContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new MessageContentWithinLimitsRule(null);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with null content");
        }

        [Test]
        public void Constructor_WithEmptyContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new MessageContentWithinLimitsRule(string.Empty);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with empty content");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var contentWithSpecialChars = "Hello! @#$%^&*()_+-={}[]|\\:;\"'<>?,./";
            var rule = new MessageContentWithinLimitsRule(contentWithSpecialChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid content containing special characters");
        }

        [Test]
        public void IsBroken_WithUnicodeCharacters_ShouldReturnFalse()
        {
            // Arrange
            var unicodeContent = "Hello 世界! 🌍🚀 Ñoël";
            var rule = new MessageContentWithinLimitsRule(unicodeContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid Unicode content");
        }

        [Test]
        public void IsBroken_WithNewlinesAndTabs_ShouldReturnFalse()
        {
            // Arrange
            var contentWithNewlines = "Line 1\nLine 2\tTabbed content\r\nWindows newline";
            var rule = new MessageContentWithinLimitsRule(contentWithNewlines);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid content containing newlines and tabs");
        }

        [Test]
        public void IsBroken_WithMixedSpacesAndContent_ShouldBehaveLikeMessageContent()
        {
            // Test that the rule behaves exactly like MessageContent.TryParse
            var testCases = new[]
            {
                "  valid content  ", // Should be valid (spaces around content)
                "valid", // Should be valid (simple content)
                " ", // Should be invalid (single space)
                "\t", // Should be invalid (tab only)
                "\n", // Should be invalid (newline only)
                "  \t\n  " // Should be invalid (whitespace only)
            };

            foreach (var testContent in testCases)
            {
                // Arrange
                var rule = new MessageContentWithinLimitsRule(testContent);
                var canParseDirectly = MessageContent.TryParse(testContent, provider: null, out _);

                // Act
                var ruleResult = rule.IsBroken();

                // Assert
                ruleResult.ShouldBe(!canParseDirectly, 
                    $"Rule result should match MessageContent.TryParse for content: '{testContent}'");
            }
        }
    }

    [TestFixture]
    public class ConsistencyTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new MessageContentWithinLimitsRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data based on MessageContent validation rules
            var validCases = new[]
            {
                "a", // Single character
                TestConstants.Messages.DefaultUserMessage,
                TestConstants.EdgeCases.OneBelowMaxMessage,
                TestConstants.EdgeCases.ExactMaxMessage
            };

            var invalidCases = new[]
            {
                null,
                string.Empty,
                "   ", // Whitespace only
                TestConstants.EdgeCases.OneOverMaxMessage
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new MessageContentWithinLimitsRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid content: '{validCase?.Substring(0, Math.Min(validCase.Length, 50))}...'");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new MessageContentWithinLimitsRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid content: '{invalidCase ?? "null"}'");
            }
        }
    }

    [TestFixture]
    public class IntegrationWithMessageContentTests : MessageContentWithinLimitsRuleTests
    {
        [Test]
        public void Rule_ShouldMirrorMessageContentValidation()
        {
            // This test ensures the rule perfectly mirrors MessageContent.TryParse behavior
            var testContents = new[]
            {
                null,
                string.Empty,
                "   ",
                "a",
                "valid content",
                TestConstants.Messages.DefaultUserMessage,
                TestConstants.EdgeCases.ExactMaxMessage,
                TestConstants.EdgeCases.OneOverMaxMessage,
                "content with\nnewlines",
                "content with\ttabs"
            };

            foreach (var content in testContents)
            {
                // Arrange
                var rule = new MessageContentWithinLimitsRule(content);
                var canCreateMessageContent = MessageContent.TryParse(content, provider: null, out _);

                // Act
                var ruleIsBroken = rule.IsBroken();

                // Assert
                ruleIsBroken.ShouldBe(!canCreateMessageContent, 
                    $"Rule broken state should be opposite of MessageContent.TryParse success for: '{content ?? "null"}'");
            }
        }
    }
}