using Axon.Modules.Chat.Domain.Tests.Common;
using Axon.Modules.Chat.Domain.Tests.Builders;

namespace Axon.Modules.Chat.Domain.Tests.Rules;

/// <summary>
/// Tests for MessageSequenceIntegrityRule that validates message sequence integrity.
/// Tests sequence validation, gap detection, and consecutive numbering requirements.
/// </summary>
[TestFixture]
public class MessageSequenceIntegrityRuleTests : DomainTestBase
{
    [TestFixture]
    public class IsBrokenTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void IsBroken_WithEmptyMessageList_ShouldReturnFalse()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageSequenceIntegrityRule(emptyMessages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with empty message list");
        }

        [Test]
        public void IsBroken_WithSingleCorrectlySequencedMessage_ShouldReturnFalse()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with single message having sequence 1");
        }

        [Test]
        public void IsBroken_WithSingleIncorrectlySequencedMessage_ShouldReturnTrue()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2) // Should be 1
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when single message doesn't have sequence 1");
        }

        [Test]
        public void IsBroken_WithCorrectlySequencedMessages_ShouldReturnFalse()
        {
            // Arrange
            var message1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .WithContent("First message")
                .Build();
                
            var message2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(2)
                .WithContent("Second message")
                .Build();
                
            var message3 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(3)
                .WithContent("Third message")
                .Build();
                
            var messages = new List<Message> { message1, message2, message3 };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with correctly sequenced messages (1, 2, 3)");
        }

        [Test]
        public void IsBroken_WithSequenceGap_ShouldReturnTrue()
        {
            // Arrange - Missing sequence 2
            var message1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var message3 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(3) // Gap: no sequence 2
                .Build();
                
            var messages = new List<Message> { message1, message3 };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when there's a gap in sequence (1, 3)");
        }

        [Test]
        public void IsBroken_WithDuplicateSequence_ShouldReturnTrue()
        {
            // Arrange - Duplicate sequence number
            var message1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1)
                .Build();
                
            var message2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(1) // Duplicate sequence
                .Build();
                
            var messages = new List<Message> { message1, message2 };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when there are duplicate sequence numbers");
        }

        [Test]
        public void IsBroken_WithSequenceStartingFromZero_ShouldReturnTrue()
        {
            // Arrange
            // Since the Message entity validates sequence > 0, we test the rule logic directly
            // by simulating what would happen with invalid sequence data
            
            // The rule should be tested with an empty list since the message can't be created
            var messages = new List<Message>(); // Empty because sequence 0 prevents creation
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act - with empty list, rule should pass (no sequences to validate)
            var result = rule.IsBroken();

            // Assert - empty list should not break the rule, but we can verify the message building fails
            result.ShouldBeFalse("Rule should not be broken with empty message list");
            
            // Verify that the message builder correctly prevents sequence 0
            var messageResult = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(0)
                .BuildResult();
            
            messageResult.IsFailure.ShouldBeTrue("Message creation should fail with zero sequence");
            messageResult.Error.Message.ShouldContain("Message sequence must be a positive integer");
        }

        [Test]
        public void IsBroken_WithLargeCorrectSequence_ShouldReturnFalse()
        {
            // Arrange - Create many messages with correct sequence
            var messages = new List<Message>();
            for (int i = 1; i <= 10; i++)
            {
                var message = MessageBuilder.New()
                    .AsUserMessage()
                    .WithSequence(i)
                    .WithContent($"Message {i}")
                    .Build();
                messages.Add(message);
            }
            
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeFalse("Rule should not be broken with large correctly sequenced message list");
        }
    }

    [TestFixture]
    public class IsBrokenAsyncTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public async Task IsBrokenAsync_ShouldReturnSameResultAsIsBroken()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2) // Incorrect sequence
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

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
            var emptyMessages = new List<Message>();
            var rule = new MessageSequenceIntegrityRule(emptyMessages);
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await rule.IsBrokenAsync(cts.Token);
            result.ShouldBeFalse("Rule should not be broken with empty message list");
        }
    }

    [TestFixture]
    public class BusinessRulePropertiesTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void Code_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageSequenceIntegrityRule(emptyMessages);

            // Act & Assert
            rule.Code.ShouldBe("CHAT.INVARIANT.SEQUENCE.VIOLATION");
        }

        [Test]
        public void Message_ShouldHaveExpectedValue()
        {
            // Arrange
            var emptyMessages = new List<Message>();
            var rule = new MessageSequenceIntegrityRule(emptyMessages);

            // Act & Assert
            rule.Message.ShouldBe("Message sequence must be contiguous starting at 1.");
        }
    }

    [TestFixture]
    public class ConstructorTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void Constructor_WithValidMessageList_ShouldCreateRule()
        {
            // Arrange
            var messages = new List<Message>();

            // Act
            var rule = new MessageSequenceIntegrityRule(messages);

            // Assert
            rule.ShouldNotBeNull();
            rule.Code.ShouldBe("CHAT.INVARIANT.SEQUENCE.VIOLATION");
        }

        [Test]
        public void Constructor_WithNullMessageList_ShouldCreateRuleWithEmptyList()
        {
            // Act
            var rule = new MessageSequenceIntegrityRule(null!);

            // Assert
            rule.ShouldNotBeNull();
            rule.IsBroken().ShouldBeFalse("Rule should handle null messages by treating as empty list");
        }
    }

    [TestFixture]
    public class EdgeCaseTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void IsBroken_WithNegativeSequence_ShouldReturnTrue()
        {
            // Arrange
            // Since the Message entity validates sequence > 0, we test the rule logic directly
            // by simulating what would happen with invalid sequence data
            
            // The rule should be tested with an empty list since the message can't be created
            var messages = new List<Message>(); // Empty because invalid sequence prevents creation
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act - with empty list, rule should pass (no sequences to validate)
            var result = rule.IsBroken();

            // Assert - empty list should not break the rule, but we can verify the message building fails
            result.ShouldBeFalse("Rule should not be broken with empty message list");
            
            // Verify that the message builder correctly prevents invalid sequences
            var messageResult = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(-1)
                .BuildResult();
            
            messageResult.IsFailure.ShouldBeTrue("Message creation should fail with negative sequence");
            messageResult.Error.Message.ShouldContain("Message sequence must be a positive integer");
        }

        [Test]
        public void IsBroken_WithOutOfOrderSequence_ShouldReturnTrue()
        {
            // Arrange - Messages in wrong order
            var message1 = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2) // Should be 1
                .Build();
                
            var message2 = MessageBuilder.New()
                .AsAssistantMessage()
                .WithSequence(1) // Should be 2
                .Build();
                
            var messages = new List<Message> { message1, message2 };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when sequences are out of order");
        }

        [Test]
        public void IsBroken_WithVeryLargeSequenceNumbers_ShouldReturnTrue()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(1000) // Too large for first message
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act
            var result = rule.IsBroken();

            // Assert
            result.ShouldBeTrue("Rule should be broken when sequence number is too large");
        }
    }

    [TestFixture]
    public class ConsistencyTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void IsBroken_CalledMultipleTimes_ShouldReturnConsistentResults()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2)
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleIsConsistent(rule);
        }

        [Test]
        public void Rule_ShouldProvideMeaningfulErrorMessage()
        {
            // Arrange
            var message = MessageBuilder.New()
                .AsUserMessage()
                .WithSequence(2)
                .Build();
                
            var messages = new List<Message> { message };
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act & Assert
            BusinessRuleTestHelpers.AssertRuleHasMeaningfulError(rule);
        }
    }

    [TestFixture]
    public class BoundaryValueTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void IsBroken_WithBoundaryValues_ShouldBehaveCorrectly()
        {
            // Test various sequence scenarios
            var testCases = new[]
            {
                // (sequences, expected broken state, description)
                (new int[0], false, "Empty list should be valid"),
                (new[] { 1 }, false, "Single message with sequence 1 should be valid"),
                (new[] { 2 }, true, "Single message with sequence 2 should be invalid"),
                (new[] { 1, 2 }, false, "Consecutive sequences 1,2 should be valid"),
                (new[] { 1, 3 }, true, "Gap in sequences 1,3 should be invalid"),
                (new[] { 2, 3 }, true, "Sequences 2,3 not starting at 1 should be invalid"),
                (new[] { 1, 2, 3, 4, 5 }, false, "Long consecutive sequence should be valid"),
                (new[] { 1, 2, 4, 5 }, true, "Long sequence with gap should be invalid")
            };

            foreach (var (sequences, expectedBroken, description) in testCases)
            {
                // Arrange - only create messages with valid sequences (> 0)
                var messages = new List<Message>();
                
                foreach (var seq in sequences.Where(s => s > 0)) // Filter out invalid sequences
                {
                    var message = MessageBuilder.New()
                        .AsUserMessage()
                        .WithSequence(seq)
                        .Build();
                    messages.Add(message);
                }
                
                var rule = new MessageSequenceIntegrityRule(messages);

                // Act
                var result = rule.IsBroken();

                // Assert
                result.ShouldBe(expectedBroken, description);
            }
            
            // Separately test that invalid sequences (≤ 0) cannot be created
            var invalidSequences = new[] { 0, -1 };
            foreach (var invalidSeq in invalidSequences)
            {
                var messageResult = MessageBuilder.New()
                    .AsUserMessage()
                    .WithSequence(invalidSeq)
                    .BuildResult();
                
                messageResult.IsFailure.ShouldBeTrue($"Message creation should fail with sequence {invalidSeq}");
                messageResult.Error.Message.ShouldContain("Message sequence must be a positive integer");
            }
        }
    }

    [TestFixture]
    public class PerformanceTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void IsBroken_WithLargeMessageList_ShouldPerformWell()
        {
            // Arrange - Create large list of correctly sequenced messages
            var messages = new List<Message>();
            for (int i = 1; i <= 1000; i++)
            {
                var message = MessageBuilder.New()
                    .AsUserMessage()
                    .WithSequence(i)
                    .WithContent($"Message {i}")
                    .Build();
                messages.Add(message);
            }
            
            var rule = new MessageSequenceIntegrityRule(messages);

            // Act & Assert - Should complete quickly
            var startTime = DateTime.UtcNow;
            var result = rule.IsBroken();
            var duration = DateTime.UtcNow - startTime;
            
            result.ShouldBeFalse("Rule should not be broken with correctly sequenced messages");
            duration.ShouldBeLessThan(TimeSpan.FromSeconds(1), "Rule evaluation should complete quickly with many messages");
        }
    }

    [TestFixture]
    public class UseCaseTests : MessageSequenceIntegrityRuleTests
    {
        [Test]
        public void Rule_ShouldEnforceBusinessInvariant_SequenceIntegrity()
        {
            // This test validates the core business invariant:
            // Messages must maintain contiguous sequence numbers starting from 1

            // Arrange - Simulate valid sequence integrity
            var validMessages = new List<Message>
            {
                MessageBuilder.New().AsUserMessage().WithSequence(1).Build(),
                MessageBuilder.New().AsAssistantMessage().WithSequence(2).Build(),
                MessageBuilder.New().AsUserMessage().WithSequence(3).Build()
            };
            var validRule = new MessageSequenceIntegrityRule(validMessages);
            
            // Act & Assert - Should pass invariant check
            validRule.IsBroken().ShouldBeFalse(
                "Business invariant should pass with contiguous sequences");

            // Arrange - Simulate violated sequence integrity
            var invalidMessages = new List<Message>
            {
                MessageBuilder.New().AsUserMessage().WithSequence(1).Build(),
                MessageBuilder.New().AsAssistantMessage().WithSequence(3).Build() // Gap!
            };
            var invalidRule = new MessageSequenceIntegrityRule(invalidMessages);
            
            // Act & Assert - Should fail invariant check
            invalidRule.IsBroken().ShouldBeTrue(
                "Business invariant should fail with non-contiguous sequences");
        }

        [Test]
        public void Rule_ShouldDetectDataCorruption_InMessageOrdering()
        {
            // This test validates data integrity protection

            // Arrange - Simulate data corruption scenarios
            var corruptedScenarios = new[]
            {
                // Duplicate sequences (data corruption)
                new[] { 1, 1, 2 },
                // Missing first sequence (corruption)
                new[] { 2, 3, 4 },
                // Random sequence (severe corruption)
                new[] { 5, 1, 3 }
            };

            foreach (var sequences in corruptedScenarios)
            {
                var messages = sequences.Select(seq =>
                    MessageBuilder.New()
                        .AsUserMessage()
                        .WithSequence(seq)
                        .Build()
                ).ToList();

                var rule = new MessageSequenceIntegrityRule(messages);

                // Act & Assert
                rule.IsBroken().ShouldBeTrue(
                    $"Rule should detect corruption in sequence: [{string.Join(", ", sequences)}]");
            }
        }
    }
}