using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for CompletionRequiresAtLeastOneMessageRule that ensures conversations have at least one message before completion.
/// Tests empty conversation validation and completion requirements.
/// </summary>
[TestFixture]
public class CompletionRequiresAtLeastOneMessageRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void IsBroken_WithZeroMessages_ShouldReturnTrue()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when trying to complete conversation with zero messages");
        }

        [Test]
        public void IsBroken_WithOneMessage_ShouldReturnFalse()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 1);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken when conversation has at least one message");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public void IsBroken_WithPositiveMessageCount_ShouldReturnFalse(int messageCount)
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse($"Rule should not be broken when conversation has {messageCount} messages");
        }

        [Test]
        public void IsBroken_WithNegativeMessageCount_ShouldReturnFalse()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: -1);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should handle negative message count gracefully (not be broken)");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

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
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 1);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with positive message count");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.CONVERSATION.EMPTY.ON.COMPLETE");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

            // Act & Assert
            rule.Message.ShouldBe("Cannot complete an empty conversation.");
        }

        [Test]
        public void Metadata_ShouldContainMessageCount()
        {
            // Arrange
            const int messageCount = 5;
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount);

            // Act & Assert
            rule.Metadata.ShouldNotBeNull();
            rule.Metadata.ShouldContainKeyAndValue("messageCount", messageCount);
        }
    }

    [TestFixture]
    public class ConstructorTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void Constructor_WithValidMessageCount_ShouldCreateRule()
        {
            // Arrange & Act
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 1);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.CONVERSATION.EMPTY.ON.COMPLETE");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(-1)]
        public void Constructor_WithVariousMessageCounts_ShouldCreateRule(int messageCount)
        {
            // Act
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount);

            // Assert
            rule.ShouldNotBeNull();
            if (rule.Metadata != null) rule.Metadata.ShouldContainKeyAndValue("messageCount", messageCount);
        }
    }

    [TestFixture]
    public class EdgeCaseTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void IsBroken_WithMaxIntMessageCount_ShouldReturnFalse()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(int.MaxValue);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with maximum integer message count");
        }

        [Test]
        public void IsBroken_WithMinIntMessageCount_ShouldReturnFalse()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(int.MinValue);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with minimum integer message count (handles negative gracefully)");
        }
    }

    [TestFixture]
    public class ConsistencyTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test data - only zero should be invalid
            var validCases = new[] { 1, 2, 10, 100, 1000, int.MaxValue };
            var invalidCases = new[] { 0 };

            // Test valid cases
            foreach (var validCase in validCases)
            {
                var rule = new CompletionRequiresAtLeastOneMessageRule(validCase);
                BusinessRuleTestHelpers.AssertRuleIsNotBroken(rule, 
                    $"Rule should not be broken for valid message count: {validCase}");
            }

            // Test invalid cases
            foreach (var invalidCase in invalidCases)
            {
                var rule = new CompletionRequiresAtLeastOneMessageRule(invalidCase);
                BusinessRuleTestHelpers.AssertRuleIsBroken(rule, 
                    $"Rule should be broken for invalid message count: {invalidCase}");
            }
        }
    }

    [TestFixture]
    public class MetadataTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void Metadata_ShouldAlwaysContainMessageCount()
        {
            // Arrange
            var messageCounts = new[] { -1, 0, 1, 5, 100 };

            foreach (var count in messageCounts)
            {
                // Act
                var rule = new CompletionRequiresAtLeastOneMessageRule(count);

                // Assert
                rule.Metadata.ShouldNotBeNull($"Metadata should not be null for message count {count}");
                rule.Metadata.ShouldContainKey("messageCount", $"Metadata should contain messageCount key for count {count}");
                rule.Metadata["messageCount"].ShouldBe(count, $"Metadata messageCount should match constructor parameter {count}");
            }
        }

        [Test]
        public void Metadata_ShouldBeReadOnly()
        {
            // Arrange
            var rule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 5);

            // Act & Assert
            rule.Metadata.ShouldNotBeNull();
            rule.Metadata.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
        }
    }

    [TestFixture]
    public class UseCaseTests : CompletionRequiresAtLeastOneMessageRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessRequirement_CannotCompleteEmptyConversation()
        {
            // This test validates the core business requirement:
            // A conversation cannot be completed if it has no messages

            // Arrange - Simulate trying to complete an empty conversation
            var emptyConversationRule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 0);
            
            // Act & Assert - Should prevent completion
            emptyConversationRule.IsBroken().ShouldBeTrue(
                "Business rule should prevent completing a conversation with no messages");

            // Arrange - Simulate completing a conversation with messages
            var validConversationRule = new CompletionRequiresAtLeastOneMessageRule(messageCount: 2);
            
            // Act & Assert - Should allow completion
            validConversationRule.IsBroken().ShouldBeFalse(
                "Business rule should allow completing a conversation with messages");
        }
    }
}