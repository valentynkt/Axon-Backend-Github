using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Shouldly;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for message sequence numbering integrity.
/// Tests that sequences are 1-based and consecutive.
/// </summary>
public class SequenceProperties
{
    [Property(Arbitrary = new[] { typeof(SequenceGenerators) })]
    public Property MessageSequences_ShouldBe1BasedAndConsecutive(MessageCount messageCount)
    {
        return Prop.ForAll(Gen.Constant(messageCount), count =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var roles = RoleFactory.ValidTurnTaking(count.Value);
            
            // Act
            var script = ConversationScript.Create(roles);
            script.ExecuteOn(builder);
            
            // Assert
            var sequences = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            var expectedSequences = Enumerable.Range(1, count.Value).ToList();
            
            return sequences.SequenceEqual(expectedSequences)
                .Label($"Sequences should be 1-based consecutive: Expected {string.Join(",", expectedSequences)}, Got {string.Join(",", sequences)}")
                .And(sequences.Count == count.Value).Label($"Should have {count.Value} sequences")
                .And(sequences.First() == 1).Label("First sequence should be 1")
                .And(sequences.Last() == count.Value).Label($"Last sequence should be {count.Value}");
        });
    }

    [Property]
    public Property SingleMessage_ShouldHaveSequence1()
    {
        return Prop.ForAll(Gen.Constant("Test content"), content =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            builder.AppendUser(content);
            
            // Assert
            var sequence = builder.LastMessage?.Sequence.Value;
            return (sequence == 1).Label("Single message should have sequence 1");
        });
    }

    [Property]
    public Property ConsecutiveAdditions_ShouldIncrementSequence()
    {
        return Prop.ForAll(SequenceGenerators.ConsecutiveContentList(), contentList =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var expectedSequence = 1;
            
            // Act & Assert
            foreach (var content in contentList)
            {
                builder.AppendUser(content);
                var actualSequence = builder.LastMessage?.Sequence.Value;
                
                if (actualSequence != expectedSequence)
                    return false.Label($"Expected sequence {expectedSequence}, got {actualSequence}");
                    
                expectedSequence++;
            }
            
            return true.Label($"All {contentList.Count} messages should have consecutive sequences");
        });
    }

    [Property(Arbitrary = new[] { typeof(SequenceGenerators) })]
    public Property AlternatingRoles_ShouldMaintainSequenceIntegrity(AlternatingRoleSequence roleSequence)
    {
        return Prop.ForAll(Gen.Constant(roleSequence), sequence =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(sequence.Roles);
            
            // Act
            script.ExecuteOn(builder);
            
            // Assert
            var sequences = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            var expectedNext = MessageExpectations.ExpectedNextSequence(0);
            var isConsecutive = true;
            
            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i] != expectedNext + i)
                {
                    isConsecutive = false;
                    break;
                }
            }
            
            return isConsecutive.Label($"Alternating roles should maintain sequence integrity: {string.Join(",", sequences)}")
                .And(sequences.Count == sequence.Roles.Count).Label("Sequence count should match role count");
        });
    }

    [Property]
    public Property FailedMessageAttempts_ShouldNotAffectSequence()
    {
        return Prop.ForAll(Gen.Constant(StringFactory.Len100kPlus1()), invalidContent =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            builder.AppendUser("Valid message 1");
            
            // Act - attempt invalid message
            var failResult = builder.TryAppendUser(invalidContent);
            
            // Then add valid message
            builder.AppendUser("Valid message 2");
            
            // Assert
            var sequences = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            var expectedSequences = new[] { 1, 2 };
            
            return failResult.IsFailure.Label("Invalid message should fail")
                .And(sequences.SequenceEqual(expectedSequences)).Label("Valid messages should have consecutive sequences [1,2]")
                .And(builder.MessageCount == 2).Label("Should only have 2 messages");
        });
    }

    [Property]
    public Property LargeMessageCounts_ShouldMaintainIntegrity()
    {
        return Prop.ForAll(SequenceGenerators.LargeMessageCount(), count =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var roles = RoleFactory.AllUser(count);
            
            // Act
            var script = ConversationScript.Create(roles);
            script.ExecuteOn(builder);
            
            // Assert
            var sequences = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            var hasGaps = false;
            var hasDuplicates = sequences.Count != sequences.Distinct().Count();
            
            for (int i = 1; i < sequences.Count; i++)
            {
                if (sequences[i] != sequences[i - 1] + 1)
                {
                    hasGaps = true;
                    break;
                }
            }
            
            return (!hasGaps).Label($"Large sequence ({count} messages) should have no gaps")
                .And(!hasDuplicates).Label("Large sequence should have no duplicates")
                .And(sequences.First() == 1).Label("Should start at 1")
                .And(sequences.Last() == count).Label($"Should end at {count}");
        });
    }

    [Property]
    public Property SequenceAfterCompletion_ShouldBeReadOnly()
    {
        return Prop.ForAll(Gen.Constant(2), messageCount =>
        {
            // Arrange
            var builder = ConversationBuilder.Started()
                .AppendUser("Message 1")
                .AppendAssistant("Message 2")
                .Complete();
            
            var sequencesBeforeAttempt = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            
            // Act
            var attemptResult = builder.TryAppendUser("Should fail - completed");
            var sequencesAfterAttempt = builder.AllMessages.Select(m => m.Sequence.Value).ToList();
            
            // Assert
            return attemptResult.IsFailure.Label("Adding to completed conversation should fail")
                .And(sequencesBeforeAttempt.SequenceEqual(sequencesAfterAttempt))
                    .Label("Sequences should remain unchanged after failed attempt")
                .And(sequencesBeforeAttempt.SequenceEqual(new[] { 1, 2 }))
                    .Label("Original sequences should be [1,2]");
        });
    }
}

/// <summary>
/// Custom generators for sequence property-based testing.
/// </summary>
public static class SequenceGenerators
{
    public static Arbitrary<MessageCount> MessageCounts() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new MessageCount(1)),
            Gen.Constant(new MessageCount(2)),
            Gen.Constant(new MessageCount(3)),
            Gen.Constant(new MessageCount(5)),
            Gen.Constant(new MessageCount(10)),
            Gen.Constant(new MessageCount(15))
        ));

    public static Arbitrary<AlternatingRoleSequence> AlternatingRoleSequences() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new AlternatingRoleSequence(RoleFactory.AlternatingPattern(4))),
            Gen.Constant(new AlternatingRoleSequence(RoleFactory.AlternatingPattern(6))),
            Gen.Constant(new AlternatingRoleSequence(RoleFactory.AlternatingPattern(8))),
            Gen.Constant(new AlternatingRoleSequence(RoleFactory.AlternatingPattern(10)))
        ));

    public static Gen<List<string>> ConsecutiveContentList() =>
        Gen.OneOf(
            Gen.Constant(["Message 1", "Message 2"]),
            Gen.Constant(["A", "B", "C"]),
            Gen.Constant(["First", "Second", "Third", "Fourth"]),
            Gen.Constant(["One", "Two", "Three", "Four", "Five"])
        );

    public static Gen<int> LargeMessageCount() =>
        Gen.OneOf(
            Gen.Constant(50),
            Gen.Constant(100),
            Gen.Constant(200)
        );
}

/// <summary>
/// Wrapper for message count in property tests.
/// </summary>
public record MessageCount(int Value);

/// <summary>
/// Wrapper for alternating role sequences in property tests.
/// </summary>
public record AlternatingRoleSequence(List<ValueObjects.MessageRole> Roles);