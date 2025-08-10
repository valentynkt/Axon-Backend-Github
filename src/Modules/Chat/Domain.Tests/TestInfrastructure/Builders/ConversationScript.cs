using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Primitives;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;

/// <summary>
/// Represents a single step in a conversation script.
/// </summary>
public readonly record struct ScriptStep(MessageRole Role, int ContentLength)
{
    public static ScriptStep User(int contentLength) => new(MessageRole.User, contentLength);
    public static ScriptStep Assistant(int contentLength) => new(MessageRole.Assistant, contentLength);
}

/// <summary>
/// Result of executing a conversation script.
/// </summary>
public sealed record ScriptResult(
    int Successes,
    int Failures, 
    int FinalSequence,
    int FinalMessageCount,
    IReadOnlyList<IDomainEvent> Events,
    IReadOnlyList<Error> Errors)
{
    public bool AllSucceeded => Failures == 0;
    public int TotalAttempts => Successes + Failures;
}

/// <summary>
/// Utility to execute scripted conversation sequences for property-based testing.
/// Builds content of exact lengths and executes via ConversationBuilder.
/// </summary>
public static class ConversationScript
{
    /// <summary>
    /// Executes a script of conversation steps, tracking successes and failures.
    /// Content is generated deterministically based on step index and length.
    /// </summary>
    public static ScriptResult Execute(IEnumerable<ScriptStep> steps, ConversationBuilder? builder = null)
    {
        builder ??= ConversationBuilder.Started();
        
        var errors = new List<Error>();
        int successes = 0;
        int failures = 0;
        
        var stepArray = steps.ToArray();
        
        for (int i = 0; i < stepArray.Length; i++)
        {
            var step = stepArray[i];
            var content = GenerateContent(i, step.ContentLength);
            
            try
            {
                if (step.Role.IsUser)
                {
                    var result = builder.TryAppendUser(content);
                    if (result.IsSuccess)
                        successes++;
                    else
                    {
                        failures++;
                        errors.Add(result.Error);
                    }
                }
                else
                {
                    var result = builder.TryAppendAssistant(content);
                    if (result.IsSuccess)
                        successes++;
                    else
                    {
                        failures++;
                        errors.Add(result.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                errors.Add(Error.Unexpected(ex.Message, "SCRIPT_EXECUTION_ERROR"));
            }
        }
        
        return new ScriptResult(
            Successes: successes,
            Failures: failures,
            FinalSequence: builder.MessageCount > 0 ? builder.MessageCount : 0,
            FinalMessageCount: builder.MessageCount,
            Events: builder.CapturedEvents,
            Errors: errors);
    }    /// <summary>
    /// Executes a script with specific role sequence and content lengths.
    /// </summary>
    public static ScriptResult ExecuteRoles(MessageRole[] roles, int contentLength = 50)
    {
        var steps = roles.Select(role => new ScriptStep(role, contentLength));
        return Execute(steps);
    }

    /// <summary>
    /// Executes a script with alternating User/Assistant messages.
    /// </summary>
    public static ScriptResult ExecuteAlternating(int count, int contentLength = 50)
    {
        var steps = new ScriptStep[count];
        for (int i = 0; i < count; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            steps[i] = new ScriptStep(role, contentLength);
        }
        return Execute(steps);
    }

    /// <summary>
    /// Executes a script with all User messages (should all succeed).
    /// </summary>
    public static ScriptResult ExecuteAllUser(int count, int contentLength = 50)
    {
        var steps = Enumerable.Repeat(ScriptStep.User(contentLength), count);
        return Execute(steps);
    }

    /// <summary>
    /// Executes a script with all Assistant messages (first succeeds, rest fail).
    /// </summary>
    public static ScriptResult ExecuteAllAssistant(int count, int contentLength = 50)
    {
        var steps = Enumerable.Repeat(ScriptStep.Assistant(contentLength), count);
        return Execute(steps);
    }

    /// <summary>
    /// Generates deterministic content for a script step.
    /// Content is unique per step but deterministic for testing.
    /// </summary>
    private static string GenerateContent(int stepIndex, int length)
    {
        if (length <= 0) return string.Empty;
        if (length > 100_000) return StringFactory.AlphaNum(100_000); // Cap at domain limit
        
        // Create content that varies by step to avoid cache issues
        var prefix = $"Step{stepIndex:D3}_";
        var remainingLength = Math.Max(0, length - prefix.Length);
        
        if (remainingLength == 0)
        {
            return prefix[..length];
        }
        
        return prefix + StringFactory.AlphaNum(remainingLength);
    }

    /// <summary>
    /// Creates a script that tests the message count limit boundary.
    /// </summary>
    public static IEnumerable<ScriptStep> MessageLimitBoundary(int aroundLimit = 10_000, int extraSteps = 5)
    {
        // Generate steps up to and beyond the limit
        for (int i = 0; i < aroundLimit + extraSteps; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            yield return new ScriptStep(role, 20); // Short content for performance
        }
    }

    /// <summary>
    /// Creates a script with mixed valid and invalid content lengths.
    /// </summary>
    public static IEnumerable<ScriptStep> MixedContentLengths()
    {
        var lengths = new[] { 1, 50, 100, 1000, 10_000, 50_000, 100_000, 100_001 }; // Last one invalid
        
        for (int i = 0; i < lengths.Length; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            yield return new ScriptStep(role, lengths[i]);
        }
    }
}