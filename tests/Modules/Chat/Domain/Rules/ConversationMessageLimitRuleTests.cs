using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for ConversationMessageLimitRule that enforces a hard limit on messages in a conversation.
/// Tests boundary conditions, edge cases, and business rule behavior.
/// </summary>
[TestFixture]
public class ConversationMessageLimitRuleTests : DomainTestBase
{
    private const int DefaultMaxMessages = 10_000;
    
    [TestFixture]
    public class IsBrokenTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public void IsBroken_WhenCurrentCountIsZero_ShouldReturnFalse()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(currentCount: 0);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation has no messages");
        }

        [Test]
        public void IsBroken_WhenCurrentCountIsBelowLimit_ShouldReturnFalse()
        {
            // Arrange
            var currentCount = DefaultMaxMessages - 1;
            var rule = new ConversationMessageLimitRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when count ({currentCount}) is below limit ({DefaultMaxMessages})");
        }

        [TestCase(10_000)]
        [TestCase(10_001)]
        [TestCase(15_000)]
        [TestCase(int.MaxValue)]
        public void IsBroken_WhenCurrentCountEqualsOrExceedsLimit_ShouldReturnTrue(int currentCount)
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken when count ({currentCount}) equals or exceeds limit ({DefaultMaxMessages})");
        }

        [TestCase(5_000)]
        [TestCase(7_500)]
        [TestCase(9_999)]
        public void IsBroken_WithCustomMaxMessages_WhenBelowCustomLimit_ShouldReturnFalse(int currentCount)
        {
            // Arrange
            const int customMax = 10_000;
            var rule = new ConversationMessageLimitRule(currentCount, customMax);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when count ({currentCount}) is below custom limit ({customMax})");
        }

        [TestCase(5_000)]
        [TestCase(5_001)]
        [TestCase(10_000)]
        public void IsBroken_WithCustomMaxMessages_WhenEqualsOrExceedsCustomLimit_ShouldReturnTrue(int currentCount)
        {
            // Arrange
            const int customMax = 5_000;
            var rule = new ConversationMessageLimitRule(currentCount, customMax);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken when count ({currentCount}) equals or exceeds custom limit ({customMax})");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(DefaultMaxMessages);

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
            var rule = new ConversationMessageLimitRule(0);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with zero messages");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(0);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.MESSAGE.LIMIT.EXCEEDED");
        }

        [Test]
        public void Message_WithDefaultMaxMessages_ShouldContainDefaultLimit()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(0);

            // Act & Assert
            rule.Message.ShouldBe($"Conversation cannot exceed {DefaultMaxMessages} messages.");
        }

        [Test]
        public void Message_WithCustomMaxMessages_ShouldContainCustomLimit()
        {
            // Arrange
            const int customMax = 5_000;
            var rule = new ConversationMessageLimitRule(0, customMax);

            // Act & Assert
            rule.Message.ShouldBe($"Conversation cannot exceed {customMax} messages.");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public void Constructor_WithNegativeCurrentCount_ShouldNotBeBroken()
        {
            // Arrange & Act
            var rule = new ConversationMessageLimitRule(-1);

            // Assert
            rule.IsBroken().ShouldBeFalse("Rule should handle negative current count gracefully");
        }

        [Test]
        public void Constructor_WithZeroMaxMessages_ShouldAlwaysBeBroken()
        {
            // Arrange & Act
            var rule = new ConversationMessageLimitRule(0, maxMessages: 0);

            // Assert
            rule.IsBroken().ShouldBeTrue("Rule should be broken when max messages is zero");
        }

        [Test]
        public void Constructor_WithNegativeMaxMessages_ShouldBeBrokenWithAnyPositiveCount()
        {
            // Arrange & Act
            var rule = new ConversationMessageLimitRule(1, maxMessages: -1);

            // Assert
            rule.IsBroken().ShouldBeTrue("Rule should be broken when max messages is negative");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(DefaultMaxMessages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new ConversationMessageLimitRule(DefaultMaxMessages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : ConversationMessageLimitRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data with valid and invalid boundary cases
            var validCases = new[] { 0, 1, 9999, DefaultMaxMessages - 1 };
            var invalidCases = new[] { DefaultMaxMessages, DefaultMaxMessages + 1, int.MaxValue };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new ConversationMessageLimitRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid case: {validCase}");
            }

            // Test invalid cases  
            foreach (var invalidCase in invalidCases)
            {
                var rule = new ConversationMessageLimitRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid case: {invalidCase}");
            }
        }
    }
}