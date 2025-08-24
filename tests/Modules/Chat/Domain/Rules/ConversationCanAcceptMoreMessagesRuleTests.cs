using System.Globalization;
using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for ConversationCanAcceptMoreMessagesRule that validates conversation message capacity.
/// Tests capacity limits, boundary conditions, and message acceptance validation.
/// </summary>
[TestFixture]
public class ConversationCanAcceptMoreMessagesRuleTests : DomainTestBase
{
    private const int DefaultMaxMessages = 10_000;
    
    [TestFixture]
    public class IsBrokenTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void IsBroken_WithZeroMessages_ShouldReturnFalse()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount: 0);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation has capacity for more messages");
        }

        [Test]
        public void IsBroken_WithMessagesUnderCapacity_ShouldReturnFalse()
        {
            // Arrange
            var currentCount = DefaultMaxMessages / 2;
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when conversation ({currentCount}) is under capacity ({DefaultMaxMessages})");
        }

        [Test]
        public void IsBroken_WithMessagesJustBelowCapacity_ShouldReturnFalse()
        {
            // Arrange
            var currentCount = DefaultMaxMessages - 1;
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when conversation ({currentCount}) is just below capacity ({DefaultMaxMessages})");
        }

        [Test]
        public void IsBroken_WithMessagesAtCapacity_ShouldReturnTrue()
        {
            // Arrange
            var currentCount = DefaultMaxMessages;
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken when conversation ({currentCount}) is at capacity ({DefaultMaxMessages})");
        }

        [TestCase(10_000)]
        [TestCase(10_001)]
        [TestCase(15_000)]
        [TestCase(int.MaxValue)]
        public void IsBroken_WithMessagesAtOrOverCapacity_ShouldReturnTrue(int currentCount)
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken when conversation ({currentCount}) is at or over capacity ({DefaultMaxMessages})");
        }

        [TestCase(2_500, 5_000)]
        [TestCase(4_999, 5_000)]
        public void IsBroken_WithCustomCapacity_WhenUnderLimit_ShouldReturnFalse(int currentCount, int maxMessages)
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount, maxMessages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when conversation ({currentCount}) is under custom capacity ({maxMessages})");
        }

        [TestCase(5_000, 5_000)]
        [TestCase(5_001, 5_000)]
        [TestCase(10_000, 5_000)]
        public void IsBroken_WithCustomCapacity_WhenAtOrOverLimit_ShouldReturnTrue(int currentCount, int maxMessages)
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount, maxMessages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue($"Rule should be broken when conversation ({currentCount}) is at or over custom capacity ({maxMessages})");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(DefaultMaxMessages);

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
            var rule = new ConversationCanAcceptMoreMessagesRule(0);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with zero messages");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(0);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.MESSAGE.CAPACITY.EXCEEDED");
        }

        [Test]
        public void Message_WithDefaultMaxMessages_ShouldContainDefaultCapacity()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(0);

            // Act & Assert
            rule.Message.ShouldBe($"Conversation has reached its maximum capacity of {DefaultMaxMessages} messages and cannot accept more.");
        }

        [Test]
        public void Message_WithCustomMaxMessages_ShouldContainCustomCapacity()
        {
            // Arrange
            const int customMax = 5_000;
            var rule = new ConversationCanAcceptMoreMessagesRule(0, customMax);

            // Act & Assert
            rule.Message.ShouldBe($"Conversation has reached its maximum capacity of {customMax} messages and cannot accept more.");
        }
    }

    [TestFixture]
    public class ConstructorTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new ConversationCanAcceptMoreMessagesRule(100);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.MESSAGE.CAPACITY.EXCEEDED");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(10000)]
        public void Constructor_WithVariousCurrentCounts_ShouldCreateRule(int currentCount)
        {
            // Act
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.MESSAGE.CAPACITY.EXCEEDED");
        }

        [TestCase(1, 1)]
        [TestCase(100, 1000)]
        [TestCase(5000, 10000)]
        public void Constructor_WithCustomCapacity_ShouldCreateRule(int currentCount, int maxMessages)
        {
            // Act
            var rule = new ConversationCanAcceptMoreMessagesRule(currentCount, maxMessages);

            // Assert
            rule.ShouldNotBeNull();
            rule.Message.ShouldContain(maxMessages.ToString(CultureInfo.InvariantCulture));
        }
    }

    [TestFixture]
    public class EdgeCaseTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void Constructor_WithNegativeCurrentCount_ShouldNotBeBroken()
        {
            // Arrange & Act
            var rule = new ConversationCanAcceptMoreMessagesRule(-1);

            // Assert
            rule.IsBroken().ShouldBeFalse("Rule should handle negative current count gracefully (not be broken)");
        }

        [Test]
        public void Constructor_WithZeroMaxMessages_ShouldAlwaysBeBroken()
        {
            // Arrange & Act
            var rule = new ConversationCanAcceptMoreMessagesRule(0, maxMessages: 0);

            // Assert
            rule.IsBroken().ShouldBeTrue("Rule should be broken when max messages is zero (no capacity)");
        }

        [Test]
        public void Constructor_WithNegativeMaxMessages_ShouldBeBrokenWithAnyPositiveCount()
        {
            // Arrange & Act
            var rule = new ConversationCanAcceptMoreMessagesRule(1, maxMessages: -1);

            // Assert
            rule.IsBroken().ShouldBeTrue("Rule should be broken when max messages is negative");
        }

        [Test]
        public void IsBroken_WithMaxIntCurrentCount_ShouldReturnTrue()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(int.MaxValue);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken with maximum integer current count");
        }
    }

    [TestFixture]
    public class ConsistencyTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(DefaultMaxMessages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new ConversationCanAcceptMoreMessagesRule(DefaultMaxMessages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data with valid and invalid boundary cases
            var validCases = new[] { 0, 1, 9999, DefaultMaxMessages - 1 };
            var invalidCases = new[] { DefaultMaxMessages, DefaultMaxMessages + 1, int.MaxValue };

            // Test valid cases (can accept more messages)
            foreach (var validCase in validCases)
            {
                var rule = new ConversationCanAcceptMoreMessagesRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid message count: {validCase}");
            }

            // Test invalid cases (cannot accept more messages)
            foreach (var invalidCase in invalidCases)
            {
                var rule = new ConversationCanAcceptMoreMessagesRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid message count: {invalidCase}");
            }
        }
    }

    [TestFixture]
    public class UseCaseTests : ConversationCanAcceptMoreMessagesRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_ConversationCapacityLimits()
        {
            // This test validates the core business requirement:
            // Conversations have a maximum message capacity to prevent resource abuse

            // Arrange - Simulate conversation with room for more messages
            var roomForMoreRule = new ConversationCanAcceptMoreMessagesRule(currentCount: 100);
            
            // Act & Assert - Should allow more messages
            roomForMoreRule.IsBroken().ShouldBeFalse(
                "Business rule should allow adding messages when under capacity");

            // Arrange - Simulate conversation at full capacity
            var fullCapacityRule = new ConversationCanAcceptMoreMessagesRule(currentCount: DefaultMaxMessages);
            
            // Act & Assert - Should prevent more messages
            fullCapacityRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent adding messages when at full capacity");
        }

        [Test]
        public void Rule_ShouldSupportCustomCapacityLimits()
        {
            // This test validates that different conversation types can have different limits
            
            // Arrange - Simulate premium conversation with higher limit
            const int premiumLimit = 20_000;
            var premiumRule = new ConversationCanAcceptMoreMessagesRule(
                currentCount: 15_000, 
                maxMessages: premiumLimit);
            
            // Act & Assert
            premiumRule.IsBroken().ShouldBeFalse(
                "Premium conversations should support higher message limits");

            // Arrange - Simulate basic conversation with lower limit
            const int basicLimit = 1_000;
            var basicRule = new ConversationCanAcceptMoreMessagesRule(
                currentCount: 1_000, 
                maxMessages: basicLimit);
            
            // Act & Assert
            basicRule.IsBroken().ShouldBeTrue(
                "Basic conversations should be limited to lower message counts");
        }
    }
}