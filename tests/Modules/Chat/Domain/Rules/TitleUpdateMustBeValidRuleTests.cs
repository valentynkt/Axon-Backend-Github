using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for TitleUpdateMustBeValidRule that validates title updates (non-null, non-empty required).
/// Tests title update validation and ConversationTitle integration.
/// </summary>
[TestFixture]
public class TitleUpdateMustBeValidRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithValidTitle_ShouldReturnFalse()
        {
            // Arrange
            var validTitle = TestConstants.Conversations.DefaultTitle;
            var rule = new TitleUpdateMustBeValidRule(validTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid title for update");
        }

        [Test]
        public void IsBroken_WithNullTitle_ShouldReturnTrue()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule(null);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with null title (updates require valid title)");
        }

        [Test]
        public void IsBroken_WithEmptyTitle_ShouldReturnTrue()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule(string.Empty);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with empty title (updates require valid title)");
        }

        [Test]
        public void IsBroken_WithWhitespaceTitle_ShouldReturnTrue()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule("   ");

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with whitespace title (updates require valid title)");
        }

        [Test]
        public void IsBroken_WithTitleAtMaxLength_ShouldReturnFalse()
        {
            // Arrange
            var maxLengthTitle = TestConstants.EdgeCases.ExactMaxTitle;
            var rule = new TitleUpdateMustBeValidRule(maxLengthTitle);

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
            var rule = new TitleUpdateMustBeValidRule(tooLongTitle);

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
            var rule = new TitleUpdateMustBeValidRule(titleWithSpaces);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with valid title containing spaces");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var title = TestConstants.Conversations.DefaultTitle;
            var rule = new TitleUpdateMustBeValidRule(title);

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
            var rule = new TitleUpdateMustBeValidRule(title);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with valid title");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule("test");

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.TITLE_VALID");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule("test");

            // Act & Assert
            rule.Message.ShouldBe("Title is valid.");
        }
    }

    [TestFixture]
    public class ConstructorTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void Constructor_WithValidTitle_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new TitleUpdateMustBeValidRule("valid title");

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.TITLE_VALID");
        }

        [Test]
        public void Constructor_WithNullTitle_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new TitleUpdateMustBeValidRule(null);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeTrue("Rule should be broken with null title for update");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var titleWithSpecialChars = "Title with @#$%^&*()";
            var rule = new TitleUpdateMustBeValidRule(titleWithSpecialChars);

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
            var rule = new TitleUpdateMustBeValidRule(unicodeTitle);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with Unicode title");
        }

        [Test]
        public void IsBroken_WithTabsAndNewlines_ShouldBehaveLikeConversationTitle()
        {
            // Test that the rule behaves like ConversationTitle.Create for non-empty titles
            var testTitles = new[]
            {
                "Title with\ttab",
                "Title with\nnewline", 
                "Title with\r\nwindows newline"
            };

            foreach (var testTitle in testTitles)
            {
                // Arrange
                var rule = new TitleUpdateMustBeValidRule(testTitle);
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
    public class ConsistencyTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule(null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new TitleUpdateMustBeValidRule(null);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data - null, empty, and whitespace are NOT allowed for updates
            var validCases = new[]
            {
                "a", // Single character
                TestConstants.Conversations.DefaultTitle,
                TestConstants.EdgeCases.OneBelowMaxTitle,
                TestConstants.EdgeCases.ExactMaxTitle
            };

            var invalidCases = new string?[]
            {
                null, // Null not allowed for updates
                string.Empty, // Empty not allowed for updates
                "   ", // Whitespace not allowed for updates
                TestConstants.EdgeCases.OneOverMaxTitle
            };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new TitleUpdateMustBeValidRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid update title: '{validCase}'");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new TitleUpdateMustBeValidRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid update title: '{invalidCase ?? "null"}'");
            }
        }
    }

    [TestFixture]
    public class IntegrationWithConversationTitleTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void Rule_ShouldMirrorConversationTitleValidation_ForValidTitles()
        {
            // For non-null, non-empty titles, rule should mirror ConversationTitle validation
            var testTitles = new[]
            {
                "a",
                "valid title",
                TestConstants.Conversations.DefaultTitle,
                TestConstants.EdgeCases.ExactMaxTitle
                // Removed OneOverMaxTitle since it's invalid and throws exception
            };

            foreach (var title in testTitles)
            {
                // Arrange
                var rule = new TitleUpdateMustBeValidRule(title);
                var conversationTitleResult = ConversationTitle.Create(title);

                // Act
                var ruleIsBroken = rule.IsBroken();

                // Assert
                ruleIsBroken.ShouldBe(conversationTitleResult.IsFailure, 
                    $"Rule broken state should match ConversationTitle.Create failure for: '{title}'");
            }
        }

        [Test]
        public void Rule_ShouldRejectNullAndWhitespace_UnlikeTitleProvidedRule()
        {
            // This test ensures the rule rejects null/empty/whitespace (different from TitleProvidedRule)
            var rejectedTitles = new string?[] { null, string.Empty, "   ", "\t", "\n" };

            foreach (var title in rejectedTitles)
            {
                // Arrange
                var rule = new TitleUpdateMustBeValidRule(title);

                // Act
                var ruleIsBroken = rule.IsBroken();

                // Assert
                ruleIsBroken.ShouldBeTrue($"Rule should reject null/empty/whitespace title for update: '{title ?? "null"}'");
            }
        }
    }

    [TestFixture]
    public class UseCaseTests : TitleUpdateMustBeValidRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_UpdatesRequireValidTitle()
        {
            // This test validates the core business requirement:
            // Title updates must provide a valid, non-empty title (unlike conversation start)

            // Arrange - Simulate updating with empty/null title
            var emptyTitleRule = new TitleUpdateMustBeValidRule(null);
            var whitespaceRule = new TitleUpdateMustBeValidRule("   ");
            
            // Act & Assert - Should prevent update
            emptyTitleRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent updating title with null value");
            whitespaceRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent updating title with whitespace value");

            // Arrange - Simulate updating with valid title
            var validTitleRule = new TitleUpdateMustBeValidRule(TestConstants.Conversations.DefaultTitle);
            
            // Act & Assert - Should allow update
            validTitleRule.IsBroken().ShouldBeFalse(
                "Business rule should allow updating title with valid value");
        }
    }
}