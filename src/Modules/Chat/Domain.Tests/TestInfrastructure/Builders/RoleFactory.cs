using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Helpers to build valid and invalid role sequences for turn-taking tests.
/// Ensures consistent generation of test scenarios for property-based testing.
/// </summary>
public static class RoleFactory
{
    /// <summary>
    /// Generates a valid turn-taking sequence of the specified length.
    /// Never produces two consecutive Assistant roles.
    /// Pattern: User, Assistant, User, Assistant, etc.
    /// </summary>
    public static MessageRole[] ValidTurnTaking(int length)
    {
        if (length <= 0) return Array.Empty<MessageRole>();

        var roles = new MessageRole[length];
        
        // Start with User to ensure valid turn-taking
        for (int i = 0; i < length; i++)
        {
            roles[i] = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
        }
        
        return roles;
    }

    /// <summary>
    /// Generates a sequence that starts with Assistant (valid edge case).
    /// Assistant can be the first message, then alternates.
    /// Pattern: Assistant, User, Assistant, User, etc.
    /// </summary>
    public static MessageRole[] ValidStartingWithAssistant(int length)
    {
        if (length <= 0) return Array.Empty<MessageRole>();

        var roles = new MessageRole[length];
        
        // Start with Assistant
        for (int i = 0; i < length; i++)
        {
            roles[i] = i % 2 == 0 ? MessageRole.Assistant : MessageRole.User;
        }
        
        return roles;
    }

    /// <summary>
    /// Generates a sequence with exactly one Assistant-Assistant violation.
    /// Places the violation at the specified position (0-based).
    /// If position is out of bounds, places it at the end.
    /// </summary>
    public static MessageRole[] WithAssistantViolation(int length, int violationPosition = -1)
    {
        if (length < 2) throw new ArgumentException("Need at least 2 roles to create a violation", nameof(length));

        // Start with valid sequence
        var roles = ValidTurnTaking(length);
        
        // Determine where to place the violation
        int pos = violationPosition >= 0 && violationPosition < length - 1 
            ? violationPosition 
            : length - 2;

        // Create AA violation: make both positions Assistant
        roles[pos] = MessageRole.Assistant;
        roles[pos + 1] = MessageRole.Assistant;
        
        return roles;
    }

    /// <summary>
    /// Generates a sequence of all User messages (valid - no turn-taking restriction).
    /// </summary>
    public static MessageRole[] AllUser(int length)
    {
        return Enumerable.Repeat(MessageRole.User, length).ToArray();
    }

    /// <summary>
    /// Generates a sequence of all Assistant messages (invalid after first).
    /// Only the first Assistant message should succeed.
    /// </summary>
    public static MessageRole[] AllAssistant(int length)
    {
        return Enumerable.Repeat(MessageRole.Assistant, length).ToArray();
    }

    /// <summary>
    /// Creates a random but deterministic role sequence for property testing.
    /// Uses seed for reproducible results.
    /// </summary>
    public static MessageRole[] Random(int length, int seed, bool ensureValid = true)
    {
        if (length <= 0) return Array.Empty<MessageRole>();

        var random = new Random(seed);
        var roles = new MessageRole[length];
        
        if (ensureValid)
        {
            // Start with a valid sequence and then add some randomness
            var baseSequence = ValidTurnTaking(length);
            
            // Add some random user messages (which are always valid)
            for (int i = 0; i < length; i++)
            {
                if (random.NextDouble() < 0.3 && baseSequence[i] == MessageRole.Assistant)
                {
                    // Randomly convert some assistant messages to user (maintains validity)
                    roles[i] = MessageRole.User;
                }
                else
                {
                    roles[i] = baseSequence[i];
                }
            }
        }
        else
        {
            // Truly random sequence (likely invalid)
            for (int i = 0; i < length; i++)
            {
                roles[i] = random.NextDouble() < 0.5 ? MessageRole.User : MessageRole.Assistant;
            }
        }
        
        return roles;
    }
}