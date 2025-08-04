using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents the role of a message sender in a conversation
/// </summary>
public readonly record struct MessageRole
{
    public string Value { get; }

    private MessageRole(string value) => Value = value;

    /// <summary>
    /// Creates a MessageRole directly from a valid value (for EF Core use only)
    /// WARNING: This bypasses validation and should only be used by infrastructure code
    /// </summary>
    /// <summary>
    /// Creates a MessageRole directly from a valid value (for EF Core use only)
    /// WARNING: This bypasses validation and should only be used by infrastructure code
    /// </summary>
    public static MessageRole FromValue(string value) => new(value);

    public static readonly MessageRole User = new("user");
    public static readonly MessageRole Assistant = new("assistant");
    public static readonly MessageRole System = new("system");
    public static readonly MessageRole Tool = new("tool");

    /// <summary>
    /// Creates a MessageRole from a string value with validation
    /// </summary>
    /// <param name="value">The role string value</param>
    /// <returns>Result containing MessageRole or validation error</returns>
    public static Result<MessageRole> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("MessageRole cannot be null or empty");

        var normalizedValue = value.ToLowerInvariant().Trim();

        return normalizedValue switch
        {
            "user" => User,
            "assistant" => Assistant,
            "system" => System,
            "tool" => Tool,
            _ => Error.Validation($"Invalid message role: {value}. Valid roles are: user, assistant, system, tool")
        };
    }

    /// <summary>
    /// Determines if this is a user role
    /// </summary>
    public bool IsUser => Value == User.Value;

    /// <summary>
    /// Determines if this is an assistant role
    /// </summary>
    public bool IsAssistant => Value == Assistant.Value;

    /// <summary>
    /// Determines if this is a system role
    /// </summary>
    public bool IsSystem => Value == System.Value;

    /// <summary>
    /// Determines if this is a tool role
    /// </summary>
    public bool IsTool => Value == Tool.Value;

    public override string ToString() => Value;

    public static implicit operator string(MessageRole role) => role.Value;
}