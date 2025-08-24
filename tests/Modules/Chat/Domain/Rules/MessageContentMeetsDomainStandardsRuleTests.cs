using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for MessageContentMeetsDomainStandardsRule that validates message content meets domain standards.
/// Tests content validation, control character detection, and domain-specific requirements.
/// </summary>
[TestFixture]
public class MessageContentMeetsDomainStandardsRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void IsBroken_WithValidContent_ShouldReturnFalse()
        {
            // Arrange
            var validContent = TestConstants.Messages.DefaultUserMessage;
            var rule = new MessageContentMeetsDomainStandardsRule(validContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid message content");
        }

        [Test]
        public void IsBroken_WithEmptyContent_ShouldReturnTrue()
        {
            // Arrange
            var rule = new MessageContentMeetsDomainStandardsRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty message content");
        }

        [Test]
        public void IsBroken_WithWhitespaceOnlyContent_ShouldReturnTrue()
        {
            // Arrange
            var whitespaceContent = "   \t\n   ";
            var rule = new MessageContentMeetsDomainStandardsRule(whitespaceContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace-only content");
        }

        [Test]
        public void IsBroken_WithContentContainingNormalNewlines_ShouldReturnFalse()
        {
            // Arrange
            var contentWithNewlines = "Line 1\nLine 2\r\nWindows newline";
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithNewlines);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with content containing normal newlines");
        }

        [Test]
        public void IsBroken_WithContentContainingTabs_ShouldReturnFalse()
        {
            // Arrange
            var contentWithTabs = "Content with\ttab characters";
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithTabs);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with content containing tab characters");
        }

        [Test]
        public void IsBroken_WithControlCharacters_ShouldReturnTrue()
        {
            // Arrange - Content with control characters other than \n, \r, \t
            var controlChar = '\x01'; // Control character
            var contentWithControlChars = "Valid content" + controlChar;
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithControlChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with content containing control characters");
        }

        [Test]
        public void IsBroken_WithMultipleControlCharacters_ShouldReturnTrue()
        {
            // Arrange - Content with multiple control characters
            var controlChars = new char[] { '\x01', '\x02', '\x03' };
            var contentWithControlChars = "Valid content" + new string(controlChars);
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithControlChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with content containing multiple control characters");
        }

        [Test]
        public void IsBroken_WithValidContentContainingSpaces_ShouldReturnFalse()
        {
            // Arrange
            var validContent = TestConstants.Messages.ValidMessageWithSpaces;
            var rule = new MessageContentMeetsDomainStandardsRule(validContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid content containing spaces");
        }

        [Test]
        public void IsBroken_WithShortValidContent_ShouldReturnFalse()
        {
            // Arrange
            var shortContent = "Hi";
            var rule = new MessageContentMeetsDomainStandardsRule(shortContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with short valid content");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var content = TestConstants.Messages.DefaultUserMessage;
            var rule = new MessageContentMeetsDomainStandardsRule(content);

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
            var rule = new MessageContentMeetsDomainStandardsRule(content);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid content");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new MessageContentMeetsDomainStandardsRule("test");

            // Act & Assert
            rule.Code.ShouldBe("CHAT.MESSAGE.CONTENT.DOMAIN.STANDARDS.VIOLATION");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new MessageContentMeetsDomainStandardsRule("test");

            // Act & Assert
            rule.Message.ShouldBe("Message content does not meet domain standards for user messages.");
        }
    }

    [TestFixture]
    public class ConstructorTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void Constructor_WithValidContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new MessageContentMeetsDomainStandardsRule("valid content");

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.MESSAGE.CONTENT.DOMAIN.STANDARDS.VIOLATION");
        }

        [Test]
        public void Constructor_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Should.Throw<ArgumentNullException>(() => new MessageContentMeetsDomainStandardsRule(null!))
                .ParamName.ShouldBe("content");
        }

        [Test]
        public void Constructor_WithEmptyContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new MessageContentMeetsDomainStandardsRule(string.Empty);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with empty content");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void IsBroken_WithUnicodeCharacters_ShouldReturnFalse()
        {
            // Arrange
            var unicodeContent = "Message with 世界 🌍 and Ñoël";
            var rule = new MessageContentMeetsDomainStandardsRule(unicodeContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with Unicode content");
        }

        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var contentWithSpecialChars = "Message with @#$%^&*()_+-={}[]|\\:;\"'<>?,./";
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithSpecialChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with special characters");
        }

        [Test]
        public void IsBroken_WithLongContent_ShouldReturnFalse()
        {
            // Arrange
            var longContent = new string('A', 1000);
            var rule = new MessageContentMeetsDomainStandardsRule(longContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with long valid content");
        }

        [Test]
        public void IsBroken_WithSingleControlCharacter_ShouldReturnTrue()
        {
            // Arrange - Even a single control character should violate domain standards
            var contentWithSingleControlChar = "Valid content\x01";
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithSingleControlChar);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with even a single control character");
        }

        [Test]
        public void IsBroken_WithOnlyNormalWhitespace_ShouldReturnTrue()
        {
            // Arrange - Content with only normal whitespace characters
            var whitespaceContent = " \t\n\r ";
            var rule = new MessageContentMeetsDomainStandardsRule(whitespaceContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with only whitespace content");
        }
    }

    [TestFixture]
    public class ConsistencyTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new MessageContentMeetsDomainStandardsRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new MessageContentMeetsDomainStandardsRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data for domain standards validation
            var validCases = new[]
            {
                "A", // Single character
                "Valid user message",
                TestConstants.Messages.DefaultUserMessage,
                "Message with\ttabs\nand\r\nnewlines", // Normal formatting
                "Message with Unicode 世界 🌍", // Unicode content
                "Message with special chars @#$%"
            };

            var invalidCases = new[]
            {
                string.Empty,
                "   ", // Whitespace only
                "\t\n\r ", // Only formatting chars
                "Message\x01", // Single control char
                "Message\x01\x02\x03" // Multiple control chars
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new MessageContentMeetsDomainStandardsRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid content: '{validCase}'");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new MessageContentMeetsDomainStandardsRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid content: '{invalidCase}'");
            }
        }
    }

    [TestFixture]
    public class ControlCharacterValidationTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void IsBroken_WithNormalFormattingCharacters_ShouldReturnFalse()
        {
            // Test that normal formatting characters (\n, \r, \t) are allowed
            var contentWithFormatting = "Line 1\nLine 2\r\nLine 3\tTabbed content";
            var rule = new MessageContentMeetsDomainStandardsRule(contentWithFormatting);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with normal formatting characters");
        }

        [TestCase('\x01')] // Start of heading
        [TestCase('\x02')] // Start of text
        [TestCase('\x03')] // End of text
        [TestCase('\x04')] // End of transmission
        [TestCase('\x05')] // Enquiry
        [TestCase('\x06')] // Acknowledge
        [TestCase('\x07')] // Bell
        [TestCase('\x08')] // Backspace
        [TestCase('\x0B')] // Vertical tab
        [TestCase('\x0C')] // Form feed
        [TestCase('\x0E')] // Shift out
        [TestCase('\x0F')] // Shift in
        [TestCase('\x7F')] // Delete
        public void IsBroken_WithSpecificControlCharacters_ShouldReturnTrue(char controlChar)
        {
            // Arrange
            var content = "Valid content" + controlChar;
            var rule = new MessageContentMeetsDomainStandardsRule(content);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken with control character: {((int)controlChar):X2}");
        }

        [TestCase('\n')] // Newline
        [TestCase('\r')] // Carriage return
        [TestCase('\t')] // Tab
        public void IsBroken_WithAllowedFormattingCharacters_ShouldReturnFalse(char formattingChar)
        {
            // Arrange
            var content = "Valid content" + formattingChar + "more content";
            var rule = new MessageContentMeetsDomainStandardsRule(content);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken with allowed formatting character: {((int)formattingChar):X2}");
        }
    }

    [TestFixture]
    public class UseCaseTests : MessageContentMeetsDomainStandardsRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_CleanUserMessages()
        {
            // This test validates the core business requirement:
            // User messages must contain clean, readable content without control characters

            // Arrange - Simulate clean user message
            var cleanMessageRule = new MessageContentMeetsDomainStandardsRule("Hello, this is a clean user message.");
            
            // Act & Assert - Should allow clean messages
            cleanMessageRule.IsBroken().ShouldBeFalse(
                "Business rule should allow clean user messages");

            // Arrange - Simulate message with problematic content
            var problematicMessageRule = new MessageContentMeetsDomainStandardsRule("Message with control\x01char");
            
            // Act & Assert - Should prevent problematic messages
            problematicMessageRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent messages with control characters");
        }

        [Test]
        public void Rule_ShouldDistinguishBetween_NormalFormattingAndControlChars()
        {
            // This test validates that normal text formatting is allowed while control chars are not

            // Arrange - Simulate message with normal formatting
            var formattedMessage = "Multi-line message:\n1. First point\n2. Second point\tWith tab";
            var formattedRule = new MessageContentMeetsDomainStandardsRule(formattedMessage);
            
            // Act & Assert - Should allow normal formatting
            formattedRule.IsBroken().ShouldBeFalse(
                "Business rule should allow normal text formatting (newlines, tabs)");

            // Arrange - Simulate message with problematic control characters
            var controlCharMessage = "Message\x07with\x08control\x1Bsequences";
            var controlCharRule = new MessageContentMeetsDomainStandardsRule(controlCharMessage);
            
            // Act & Assert - Should prevent control characters
            controlCharRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent messages with control character sequences");
        }

        [Test]
        public void Rule_ShouldMaintainDataIntegrity_ByRejectingEmptyOrWhitespaceContent()
        {
            // This test validates that meaningful content is required

            // Arrange - Test empty and whitespace scenarios
            var emptyCases = new[] { string.Empty, "   ", "\t\t", "\n\n", " \t\n\r " };

            foreach (var emptyCase in emptyCases)
            {
                var rule = new MessageContentMeetsDomainStandardsRule(emptyCase);

                // Act & Assert
                rule.IsBroken().ShouldBeTrue(
                    $"Business rule should prevent empty/whitespace-only messages: '{emptyCase}'");
            }

            // Arrange - Test meaningful content
            var meaningfulRule = new MessageContentMeetsDomainStandardsRule("  Meaningful content  ");
            
            // Act & Assert
            meaningfulRule.IsBroken().ShouldBeFalse(
                "Business rule should allow messages with meaningful content");
        }
    }
}