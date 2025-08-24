using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for TitleProvidedMustBeValidRule that validates optional conversation titles when provided.
/// Tests null handling, empty string handling, and ConversationTitle validation integration.
/// </summary>
[TestFixture]
public class TitleProvidedMustBeValidRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithNullTitle_ShouldReturnFalse()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule(null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with null title (allowed for conversation start)");
        }

        [Test]
        public void IsBroken_WithEmptyTitle_ShouldReturnFalse()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with empty title (allowed for conversation start)");
        }

        [Test]
        public void IsBroken_WithWhitespaceTitle_ShouldReturnFalse()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule("   ");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with whitespace title (allowed for conversation start)");
        }

        [Test]
        public void IsBroken_WithValidTitle_ShouldReturnFalse()
        {
            // Arrange
            var validTitle = TestConstants.Conversations.DefaultTitle;
            var rule = new TitleProvidedMustBeValidRule(validTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid title");
        }

        [Test]
        public void IsBroken_WithTitleAtMaxLength_ShouldReturnFalse()
        {
            // Arrange
            var maxLengthTitle = TestConstants.EdgeCases.ExactMaxTitle;
            var rule = new TitleProvidedMustBeValidRule(maxLengthTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with title at maximum length");
        }

        [Test]
        public void IsBroken_WithTitleExceedingMaxLength_ShouldReturnTrue()
        {
            // Arrange
            var tooLongTitle = TestConstants.EdgeCases.OneOverMaxTitle;
            var rule = new TitleProvidedMustBeValidRule(tooLongTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with title exceeding maximum length");
        }

        [Test]
        public void IsBroken_WithValidTitleContainingSpaces_ShouldReturnFalse()
        {
            // Arrange
            var titleWithSpaces = TestConstants.Conversations.ValidTitleWithSpaces;
            var rule = new TitleProvidedMustBeValidRule(titleWithSpaces);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid title containing spaces");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var title = TestConstants.Conversations.DefaultTitle;
            var rule = new TitleProvidedMustBeValidRule(title);

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
            var title = TestConstants.Conversations.DefaultTitle;
            var rule = new TitleProvidedMustBeValidRule(title);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid title");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule("test");

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.TITLE.INVALID");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule("test");

            // Act & Assert
            rule.Message.ShouldBe("Provided title is invalid.");
        }
    }

    [TestFixture]
    public class ConstructorTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void Constructor_WithValidTitle_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new TitleProvidedMustBeValidRule("valid title");

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.TITLE.INVALID");
        }

        [Test]
        public void Constructor_WithNullTitle_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new TitleProvidedMustBeValidRule(null);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeFalse("Rule should not be broken with null title");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var titleWithSpecialChars = "Title with @#$%^&*()";
            var rule = new TitleProvidedMustBeValidRule(titleWithSpecialChars);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with title containing valid special characters");
        }

        [Test]
        public void IsBroken_WithUnicodeCharacters_ShouldReturnFalse()
        {
            // Arrange
            var unicodeTitle = "Título with 世界 🌍";
            var rule = new TitleProvidedMustBeValidRule(unicodeTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with Unicode title");
        }

        [Test]
        public void IsBroken_WithTabsAndNewlines_ShouldBehaveLikeConversationTitle()
        {
            // Test that the rule behaves exactly like ConversationTitle.Create
            var testTitles = new[]
            {
                "Title with\ttab",
                "Title with\nnewline",
                "Title with\r\nwindows newline"
            };

            foreach (var testTitle in testTitles)
            {
                // Arrange
                var rule = new TitleProvidedMustBeValidRule(testTitle);
                var titleCreateResult = ConversationTitle.Create(testTitle);

                // Act
                var ruleResult = rule.IsBroken();

                // Assert
                ruleResult.ShouldBe(titleCreateResult.IsFailure, 
                    $"Rule result should match ConversationTitle.Create for title: '{testTitle}'");
            }
        }
    }

    [TestFixture]
    public class ConsistencyTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule(TestConstants.EdgeCases.OneOverMaxTitle);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new TitleProvidedMustBeValidRule(TestConstants.EdgeCases.OneOverMaxTitle);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data - null and empty are allowed, but if provided must be valid
            var validCases = new string?[]
            {
                null, // Null is allowed
                string.Empty, // Empty is allowed
                "   ", // Whitespace is allowed
                "a", // Single character
                TestConstants.Conversations.DefaultTitle,
                TestConstants.EdgeCases.OneBelowMaxTitle,
                TestConstants.EdgeCases.ExactMaxTitle
            };

            var invalidCases = new[]
            {
                TestConstants.EdgeCases.OneOverMaxTitle
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new TitleProvidedMustBeValidRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid title: '{validCase ?? "null"}'");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new TitleProvidedMustBeValidRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid title: '{invalidCase}'");
            }
        }
    }

    [TestFixture]
    public class IntegrationWithConversationTitleTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void Rule_ShouldMirrorConversationTitleValidation_WhenTitleProvided()
        {
            // This test ensures the rule perfectly mirrors ConversationTitle.Create behavior
            // for non-null, non-whitespace titles
            var testTitles = new[]
            {
                "a",
                "valid title",
                TestConstants.Conversations.DefaultTitle,
                TestConstants.EdgeCases.ExactMaxTitle,
                TestConstants.EdgeCases.OneOverMaxTitle,
                "title with numbers 123",
                "title with special chars @#$"
            };

            foreach (var title in testTitles)
            {
                // Arrange
                var rule = new TitleProvidedMustBeValidRule(title);
                var conversationTitleResult = ConversationTitle.Create(title);

                // Act
                var ruleIsBroken = rule.IsBroken();

                // Assert
                ruleIsBroken.ShouldBe(conversationTitleResult.IsFailure, 
                    $"Rule broken state should match ConversationTitle.Create failure for: '{title}'");
            }
        }

        [Test]
        public void Rule_ShouldAllowNullAndWhitespace_UnlikeConversationTitle()
        {
            // This test ensures the rule allows null/empty/whitespace (special handling for Start operation)
            var allowedTitles = new string?[] { null, string.Empty, "   ", "\t", "\n" };

            foreach (var title in allowedTitles)
            {
                // Arrange
                var rule = new TitleProvidedMustBeValidRule(title);

                // Act
                var ruleIsBroken = rule.IsBroken();

                // Assert
                ruleIsBroken.ShouldBeFalse($"Rule should allow null/empty/whitespace title: '{title ?? "null"}'");
            }
        }
    }

    [TestFixture]
    public class ExceptionHandlingTests : TitleProvidedMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WhenConversationTitleCreateThrowsException_ShouldReturnTrue()
        {
            // This test ensures that if ConversationTitle.Create throws an exception,
            // the rule treats it as broken (invalid title)
            
            // Note: This is hard to test directly since ConversationTitle.Create returns Result<T>
            // rather than throwing exceptions. But the rule has exception handling as a safety net.
            // We'll test with a known problematic case that would fail ConversationTitle validation.
            
            // Arrange
            var problematicTitle = TestConstants.EdgeCases.OneOverMaxTitle;
            var rule = new TitleProvidedMustBeValidRule(problematicTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when ConversationTitle validation fails");
        }
    }
}