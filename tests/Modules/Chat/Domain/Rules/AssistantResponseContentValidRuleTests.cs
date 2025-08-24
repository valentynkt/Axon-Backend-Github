using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for AssistantResponseContentValidRule that validates assistant response content standards.
/// Tests content validation, control character limits, and assistant-specific requirements.
/// </summary>
[TestFixture]
public class AssistantResponseContentValidRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void IsBroken_WithValidContent_ShouldReturnFalse()
        {
            // Arrange
            var validContent = TestConstants.Messages.DefaultAssistantMessage;
            var rule = new AssistantResponseContentValidRule(validContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid assistant response content");
        }

        [Test]
        public void IsBroken_WithEmptyContent_ShouldReturnTrue()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty assistant response content");
        }

        [Test]
        public void IsBroken_WithWhitespaceOnlyContent_ShouldReturnTrue()
        {
            // Arrange
            var whitespaceContent = "   \t\n   ";
            var rule = new AssistantResponseContentValidRule(whitespaceContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace-only assistant response content");
        }

        [Test]
        public void IsBroken_WithSingleCharacterContent_ShouldReturnFalse()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule("A");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with single character content (after trimming)");
        }

        [Test]
        public void IsBroken_WithContentContainingNormalNewlines_ShouldReturnFalse()
        {
            // Arrange
            var contentWithNewlines = "Line 1\nLine 2\r\nWindows newline";
            var rule = new AssistantResponseContentValidRule(contentWithNewlines);

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
            var rule = new AssistantResponseContentValidRule(contentWithTabs);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with content containing tab characters");
        }

        [Test]
        public void IsBroken_WithExcessiveControlCharacters_ShouldReturnTrue()
        {
            // Arrange - Create content with more than 5 control characters (excluding \n, \r, \t)
            var controlChars = new char[] { '\x01', '\x02', '\x03', '\x04', '\x05', '\x06' }; // 6 control chars
            var contentWithControlChars = "Valid content" + new string(controlChars);
            var rule = new AssistantResponseContentValidRule(contentWithControlChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with excessive control characters");
        }

        [Test]
        public void IsBroken_WithAcceptableControlCharacters_ShouldReturnFalse()
        {
            // Arrange - Create content with exactly 5 control characters (at the limit)
            var controlChars = new char[] { '\x01', '\x02', '\x03', '\x04', '\x05' }; // 5 control chars
            var contentWithControlChars = "Valid content" + new string(controlChars);
            var rule = new AssistantResponseContentValidRule(contentWithControlChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with acceptable number of control characters");
        }

        [Test]
        public void IsBroken_WithContentContainingLeadingAndTrailingSpaces_ShouldReturnFalse()
        {
            // Arrange
            var contentWithSpaces = "   Valid assistant response   ";
            var rule = new AssistantResponseContentValidRule(contentWithSpaces);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with content having leading/trailing spaces");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var content = TestConstants.Messages.DefaultAssistantMessage;
            var rule = new AssistantResponseContentValidRule(content);

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
            var content = TestConstants.Messages.DefaultAssistantMessage;
            var rule = new AssistantResponseContentValidRule(content);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid content");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule("test");

            // Act & Assert
            rule.Code.ShouldBe("CHAT.MESSAGE.ASSISTANT.CONTENT.INVALID");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule("test");

            // Act & Assert
            rule.Message.ShouldBe("Assistant response content does not meet validation standards.");
        }
    }

    [TestFixture]
    public class ConstructorTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void Constructor_WithValidContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new AssistantResponseContentValidRule("valid content");

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.MESSAGE.ASSISTANT.CONTENT.INVALID");
        }

        [Test]
        public void Constructor_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Should.Throw<ArgumentNullException>(() => new AssistantResponseContentValidRule(null!))
                .ParamName.ShouldBe("content");
        }

        [Test]
        public void Constructor_WithEmptyContent_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new AssistantResponseContentValidRule(string.Empty);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with empty content");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void IsBroken_WithUnicodeCharacters_ShouldReturnFalse()
        {
            // Arrange
            var unicodeContent = "Assistant response with 世界 🌍 and Ñoël";
            var rule = new AssistantResponseContentValidRule(unicodeContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with Unicode content");
        }

        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var contentWithSpecialChars = "Response with @#$%^&*()_+-={}[]|\\:;\"'<>?,./";
            var rule = new AssistantResponseContentValidRule(contentWithSpecialChars);

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
            var rule = new AssistantResponseContentValidRule(longContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with long valid content");
        }

        [Test]
        public void IsBroken_WithOnlyNormalWhitespace_ShouldReturnTrue()
        {
            // Arrange - Content with only normal whitespace characters
            var whitespaceContent = " \t\n\r ";
            var rule = new AssistantResponseContentValidRule(whitespaceContent);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with only whitespace content");
        }
    }

    [TestFixture]
    public class ConsistencyTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new AssistantResponseContentValidRule(string.Empty);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data for assistant content validation
            var validCases = new[]
            {
                "A", // Single character
                "Valid assistant response",
                TestConstants.Messages.DefaultAssistantMessage,
                "Content with\ttabs\nand\r\nnewlines", // Normal formatting
                "Content" + new string('\x01', 5) // Exactly 5 control chars (at limit)
            };

            var invalidCases = new[]
            {
                string.Empty,
                "   ", // Whitespace only
                "\t\n\r ", // Only formatting chars
                "Content" + new string('\x01', 6) // 6 control chars (over limit)
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new AssistantResponseContentValidRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid content");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new AssistantResponseContentValidRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid content");
            }
        }
    }

    [TestFixture]
    public class ControlCharacterValidationTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void IsBroken_WithNormalFormattingCharacters_ShouldReturnFalse()
        {
            // Test that normal formatting characters (\n, \r, \t) are not counted as problematic
            var contentWithFormatting = "Line 1\nLine 2\r\nLine 3\tTabbed content";
            var rule = new AssistantResponseContentValidRule(contentWithFormatting);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with normal formatting characters");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void IsBroken_WithAcceptableControlCharacterCount_ShouldReturnFalse(int controlCharCount)
        {
            // Arrange
            var content = "Valid content" + new string('\x01', controlCharCount);
            var rule = new AssistantResponseContentValidRule(content);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken with {controlCharCount} control characters (≤5)");
        }

        [TestCase(6)]
        [TestCase(10)]
        [TestCase(20)]
        public void IsBroken_WithExcessiveControlCharacterCount_ShouldReturnTrue(int controlCharCount)
        {
            // Arrange
            var content = "Valid content" + new string('\x01', controlCharCount);
            var rule = new AssistantResponseContentValidRule(content);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken with {controlCharCount} control characters (>5)");
        }
    }

    [TestFixture]
    public class UseCaseTests : AssistantResponseContentValidRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_AssistantResponsesMustBeSubstantive()
        {
            // This test validates the core business requirement:
            // Assistant responses must contain meaningful content with reasonable formatting

            // Arrange - Simulate empty assistant response
            var emptyResponseRule = new AssistantResponseContentValidRule(string.Empty);
            var whitespaceResponseRule = new AssistantResponseContentValidRule("   ");
            
            // Act & Assert - Should prevent empty responses
            emptyResponseRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent empty assistant responses");
            whitespaceResponseRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent whitespace-only assistant responses");

            // Arrange - Simulate valid assistant response
            var validResponseRule = new AssistantResponseContentValidRule("I understand your question. Here's my response.");
            
            // Act & Assert - Should allow valid responses
            validResponseRule.IsBroken().ShouldBeFalse(
                "Business rule should allow substantive assistant responses");
        }

        [Test]
        public void Rule_ShouldPreventMalformedContent_WithExcessiveControlCharacters()
        {
            // This test validates content quality requirements

            // Arrange - Simulate assistant response with excessive control characters
            var malformedContent = "Response" + new string('\x01', 10); // 10 control chars
            var malformedRule = new AssistantResponseContentValidRule(malformedContent);
            
            // Act & Assert - Should prevent malformed content
            malformedRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent responses with excessive control characters");

            // Arrange - Simulate well-formatted response
            var wellFormattedContent = "Here's a well-formatted response\nwith normal line breaks\tand tabs.";
            var wellFormattedRule = new AssistantResponseContentValidRule(wellFormattedContent);
            
            // Act & Assert - Should allow well-formatted content
            wellFormattedRule.IsBroken().ShouldBeFalse(
                "Business rule should allow well-formatted responses");
        }
    }
}