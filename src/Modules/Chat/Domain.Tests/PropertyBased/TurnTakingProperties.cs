using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using FsCheck;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for turn-taking rules in conversations.
/// Tests that no consecutive assistant messages are allowed.
/// </summary>
public class TurnTakingProperties
{
    [Property(Arbitrary = new[] { typeof(TurnTakingGenerators) })]
    public Property ValidTurnSequences_ShouldAllSucceed(ValidTurnSequence validSequence)
    {
        return Prop.ForAll(Gen.Constant(validSequence), sequence =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(sequence.Roles);
            
            // Act
            var result = script.ExecuteOn(builder);
            
            // Assert
            return result.AllSucceeded.Label($"Valid sequence {string.Join("->", sequence.Roles)} should succeed")
                .And(builder.MessageCount == sequence.Roles.Count).Label("All messages should be added");
        });
    }

    [Property(Arbitrary = new[] { typeof(TurnTakingGenerators) })]
    public Property InvalidTurnSequences_ShouldFailAtViolation(InvalidTurnSequence invalidSequence)
    {
        return Prop.ForAll(Gen.Constant(invalidSequence), sequence =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(sequence.Roles);
            
            // Act
            var result = script.ExecuteOn(builder);
            
            // Assert
            var violationIndex = FindFirstAssistantViolationIndex(sequence.Roles);
            var expectedSuccessCount = violationIndex >= 0 ? violationIndex : sequence.Roles.Count;
            
            return (!result.AllSucceeded).Label("Invalid sequence should fail")
                .And(result.FirstFailure?.Error.Code == "CHAT_CONVERSATION_CONSECUTIVE_ASSISTANT_MESSAGES")
                    .Label("Should fail with consecutive assistant error")
                .And(builder.MessageCount == expectedSuccessCount)
                    .Label($"Should have {expectedSuccessCount} messages before failure");
        });
    }

    [Property]
    public Property UserFollowedByAssistant_ShouldAlwaysSucceed()
    {
        return Prop.ForAll(TurnTakingGenerators.UserAssistantPairs(), roles =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(roles);
            
            // Act
            var result = script.ExecuteOn(builder);
            
            // Assert
            return result.AllSucceeded.Label("User->Assistant pairs should always succeed")
                .And(builder.MessageCount == roles.Count).Label("All messages should be added");
        });
    }

    [Property]
    public Property ConsecutiveAssistantMessages_ShouldAlwaysFail()
    {
        return Prop.ForAll(TurnTakingGenerators.ConsecutiveAssistantSequences(), roles =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(roles);
            
            // Act
            var result = script.ExecuteOn(builder);
            
            // Assert
            return (!result.AllSucceeded).Label("Consecutive assistants should fail")
                .And(result.FirstFailure?.Error.Code == "CHAT_CONVERSATION_CONSECUTIVE_ASSISTANT_MESSAGES")
                    .Label("Should have consecutive assistant error");
        });
    }

    [Property]
    public Property AlternatingPattern_ShouldAlwaysSucceed()
    {
        return Prop.ForAll(TurnTakingGenerators.AlternatingPatterns(), roles =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(roles);
            
            // Act
            var result = script.ExecuteOn(builder);
            
            // Assert
            return result.AllSucceeded.Label($"Alternating pattern should succeed: {string.Join("->", roles)}")
                .And(builder.MessageCount == roles.Count).Label("All messages should be added");
        });
    }

    [Property]
    public Property LastMessageRole_ShouldMatchExpected()
    {
        return Prop.ForAll(TurnTakingGenerators.ValidEndingSequences(), roles =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(roles);
            var expectedLastRole = roles.Last();
            
            // Act
            script.ExecuteOn(builder);
            var actualLastRole = builder.LastMessage?.Role;
            
            // Assert
            return (actualLastRole == expectedLastRole).Label($"Last role should be {expectedLastRole}");
        });
    }

    private static int FindFirstAssistantViolationIndex(List<MessageRole> roles)
    {
        for (int i = 1; i < roles.Count; i++)
        {
            if (roles[i] == MessageRole.Assistant && roles[i - 1] == MessageRole.Assistant)
                return i;
        }
        return -1;
    }
}

/// <summary>
/// Custom generators for turn-taking property-based testing.
/// </summary>
public static class TurnTakingGenerators
{
    public static Arbitrary<ValidTurnSequence> ValidTurnSequences() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new ValidTurnSequence(RoleFactory.ValidTurnTaking(3))),
            Gen.Constant(new ValidTurnSequence(RoleFactory.ValidTurnTaking(5))),
            Gen.Constant(new ValidTurnSequence(RoleFactory.AllUser(4))),
            Gen.Constant(new ValidTurnSequence(RoleFactory.SingleUserAssistant())),
            Gen.Constant(new ValidTurnSequence(RoleFactory.AlternatingPattern(6)))
        ));

    public static Arbitrary<InvalidTurnSequence> InvalidTurnSequences() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new InvalidTurnSequence(RoleFactory.WithAssistantViolation())),
            Gen.Constant(new InvalidTurnSequence(RoleFactory.ConsecutiveAssistants(3))),
            Gen.Constant(new InvalidTurnSequence([MessageRole.User, MessageRole.Assistant, MessageRole.Assistant])),
            Gen.Constant(new InvalidTurnSequence([MessageRole.User, MessageRole.User, MessageRole.Assistant, MessageRole.Assistant]))
        ));

    public static Gen<List<MessageRole>> UserAssistantPairs() =>
        Gen.OneOf(
            Gen.Constant([MessageRole.User, MessageRole.Assistant]),
            Gen.Constant([MessageRole.User, MessageRole.Assistant, MessageRole.User, MessageRole.Assistant]),
            Gen.Constant([MessageRole.User, MessageRole.User, MessageRole.Assistant, MessageRole.User, MessageRole.Assistant])
        );

    public static Gen<List<MessageRole>> ConsecutiveAssistantSequences() =>
        Gen.OneOf(
            Gen.Constant([MessageRole.User, MessageRole.Assistant, MessageRole.Assistant]),
            Gen.Constant([MessageRole.User, MessageRole.Assistant, MessageRole.Assistant, MessageRole.Assistant]),
            Gen.Constant([MessageRole.User, MessageRole.User, MessageRole.Assistant, MessageRole.Assistant])
        );

    public static Gen<List<MessageRole>> AlternatingPatterns() =>
        Gen.OneOf(
            Gen.Constant(RoleFactory.AlternatingPattern(4)),
            Gen.Constant(RoleFactory.AlternatingPattern(6)),
            Gen.Constant(RoleFactory.AlternatingPattern(8))
        );

    public static Gen<List<MessageRole>> ValidEndingSequences() =>
        Gen.OneOf(
            Gen.Constant([MessageRole.User]),
            Gen.Constant([MessageRole.User, MessageRole.Assistant]),
            Gen.Constant([MessageRole.User, MessageRole.User, MessageRole.Assistant]),
            Gen.Constant(RoleFactory.AlternatingPattern(5))
        );
}

/// <summary>
/// Wrapper for valid turn sequences in property tests.
/// </summary>
public record ValidTurnSequence(List<MessageRole> Roles);

/// <summary>
/// Wrapper for invalid turn sequences in property tests.
/// </summary>
public record InvalidTurnSequence(List<MessageRole> Roles);